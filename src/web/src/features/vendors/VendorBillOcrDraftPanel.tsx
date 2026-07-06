import { useEffect, useMemo, useState, type FormEvent } from 'react';

import { isApiError } from '../../api/apiErrors';
import { Badge, Button, Card, Checkbox, EmptyState, Input, ResponsiveDataList, Select, StatusBadge } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatCurrency } from '../finance/currencyDisplay';
import type { InventoryItemListItem } from '../inventory/inventoryTypes';
import type { VendorDetail, VendorBillDetail } from './vendorTypes';
import {
  confirmVendorBillOcrDraft,
  getVendorBillOcrDraft,
  listVendorBillOcrDrafts,
  updateVendorBillOcrDraft,
  uploadVendorBillOcrDraft,
} from './vendorBillOcrApi';
import type {
  VendorBillOcrDraftLineCreateRequest,
  VendorBillOcrDraftDetail,
  VendorBillOcrDraftListItem,
  VendorBillOcrDraftUpdateRequest,
} from './vendorBillOcrTypes';
import { getSafeVendorErrorMessage } from './vendorErrorDisplay';

const allowedTypes = new Set(['image/jpeg', 'image/png', 'application/pdf']);
const maxUploadBytes = 10 * 1024 * 1024;
const lowConfidenceThreshold = 0.85;

const formatDateInput = (value: string | null | undefined) => (value ? value.slice(0, 10) : '');

const resolveSafeMessage = (error: unknown, fallback: string) =>
  getSafeVendorErrorMessage(error, fallback);

const createLineId = () => {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
};

interface ReviewLineState {
  vendorBillOcrDraftLineId: string;
  reviewedDescription: string;
  reviewedQuantity: string;
  reviewedUnitCost: string;
  reviewedLineTotal: string;
  selectedInventoryItemId: string;
  isIgnored: boolean;
  isNew: boolean;
}

const createLineDraft = (line: VendorBillOcrDraftDetail['lines'][number]): ReviewLineState => ({
  vendorBillOcrDraftLineId: line.vendorBillOcrDraftLineId,
  reviewedDescription: line.reviewedDescription ?? line.extractedDescription,
  reviewedQuantity: String(line.reviewedQuantity ?? line.extractedQuantity ?? 1),
  reviewedUnitCost: String(line.reviewedUnitCost ?? line.extractedUnitCost ?? 0),
  reviewedLineTotal: String(line.reviewedLineTotal ?? line.extractedLineTotal ?? 0),
  selectedInventoryItemId: line.selectedInventoryItemId ?? '',
  isIgnored: line.isIgnored,
  isNew: false,
});

const createBlankLineDraft = (): ReviewLineState => ({
  vendorBillOcrDraftLineId: createLineId(),
  reviewedDescription: '',
  reviewedQuantity: '1',
  reviewedUnitCost: '',
  reviewedLineTotal: '',
  selectedInventoryItemId: '',
  isIgnored: false,
  isNew: true,
});

const createDraftForm = (draft: VendorBillOcrDraftDetail | null) => ({
  reviewedVendorId: draft?.reviewedVendorId ?? '',
  reviewedBillNumber: draft?.reviewedBillNumber ?? draft?.extractedBillNumber ?? '',
  reviewedBillDate: formatDateInput(draft?.reviewedBillDate ?? draft?.extractedBillDate),
  removedLineIds: [] as string[],
  lines: draft?.lines.map(createLineDraft) ?? [],
});

interface VendorBillOcrDraftPanelProps {
  branchId: string;
  vendors: VendorDetail[];
  inventoryItems: InventoryItemListItem[];
  canAccess: boolean;
  onVendorBillCreated?: (vendorBillId: string) => void;
}

export const VendorBillOcrDraftPanel = ({
  branchId,
  vendors,
  inventoryItems,
  canAccess,
  onVendorBillCreated,
}: VendorBillOcrDraftPanelProps) => {
  const { t } = useLanguage();
  const { currencyCode, locale } = useRestaurantCurrency();
  const [drafts, setDrafts] = useState<VendorBillOcrDraftListItem[]>([]);
  const [draftsLoading, setDraftsLoading] = useState(false);
  const [draftsError, setDraftsError] = useState<string | null>(null);
  const [selectedDraftId, setSelectedDraftId] = useState<string | null>(null);
  const [selectedDraft, setSelectedDraft] = useState<VendorBillOcrDraftDetail | null>(null);
  const [selectedDraftLoading, setSelectedDraftLoading] = useState(false);
  const [selectedDraftError, setSelectedDraftError] = useState<string | null>(null);
  const [uploadFile, setUploadFile] = useState<File | null>(null);
  const [uploadSubmitting, setUploadSubmitting] = useState(false);
  const [uploadError, setUploadError] = useState<string | null>(null);
  const [reviewForm, setReviewForm] = useState(createDraftForm(null));
  const [reviewSubmitting, setReviewSubmitting] = useState(false);
  const [reviewError, setReviewError] = useState<string | null>(null);
  const [notice, setNotice] = useState<string | null>(null);

  const vendorOptions = useMemo(
    () =>
      vendors.map(vendor => (
        <option key={vendor.vendorId} value={vendor.vendorId}>
          {vendor.name}
        </option>
      )),
    [vendors]
  );

  const inventoryOptions = useMemo(
    () =>
      inventoryItems.map(item => (
        <option key={item.inventoryItemId} value={item.inventoryItemId}>
          {item.name}
        </option>
      )),
    [inventoryItems]
  );

  const formatMoney = (value: number) => formatCurrency(value, currencyCode, locale);

  const reviewedTotalAmount = useMemo(
    () =>
      reviewForm.lines.reduce((sum, line) => {
        const quantity = Number(line.reviewedQuantity);
        const unitCost = Number(line.reviewedUnitCost);
        const explicitLineTotal = line.reviewedLineTotal.trim() === '' ? Number.NaN : Number(line.reviewedLineTotal);
        const lineTotal = Number.isFinite(explicitLineTotal) && explicitLineTotal >= 0
          ? explicitLineTotal
          : quantity * unitCost;
        return sum + (Number.isFinite(lineTotal) ? lineTotal : 0);
      }, 0),
    [reviewForm.lines]
  );

  const loadDrafts = async () => {
    if (!canAccess) {
      return;
    }

    setDraftsLoading(true);
    setDraftsError(null);

    try {
      const response = await listVendorBillOcrDrafts({ branchId: branchId || undefined });
      setDrafts(response.items);
      setSelectedDraftId(current => current ?? response.items[0]?.vendorBillOcrDraftId ?? null);
      if (response.items.length === 0) {
        setSelectedDraft(null);
        setReviewForm(createDraftForm(null));
      }
    } catch (error) {
      setDrafts([]);
      setDraftsError(resolveSafeMessage(error, t('vendorOcr.couldNotLoadDraftsError')));
    } finally {
      setDraftsLoading(false);
    }
  };

  const loadDraft = async (draftId: string) => {
    setSelectedDraftLoading(true);
    setSelectedDraftError(null);

    try {
      const draft = await getVendorBillOcrDraft(draftId);
      setSelectedDraft(draft);
      setReviewForm(createDraftForm(draft));
    } catch (error) {
      setSelectedDraft(null);
      setSelectedDraftError(resolveSafeMessage(error, t('vendorOcr.couldNotLoadDraft')));
    } finally {
      setSelectedDraftLoading(false);
    }
  };

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadDrafts();
  }, [branchId, canAccess]);

  useEffect(() => {
    if (!selectedDraftId) {
      setSelectedDraft(null);
      setReviewForm(createDraftForm(null));
      return;
    }

    void loadDraft(selectedDraftId);
  }, [selectedDraftId]);

  useEffect(() => {
    if (selectedDraftId && drafts.some(item => item.vendorBillOcrDraftId === selectedDraftId)) {
      return;
    }

    setSelectedDraftId(drafts[0]?.vendorBillOcrDraftId ?? null);
  }, [drafts, selectedDraftId]);

  if (!canAccess) {
    return null;
  }

  const updateLine = (lineId: string, updater: (current: ReviewLineState) => ReviewLineState) => {
    setReviewForm(current => ({
      ...current,
      lines: current.lines.map(line => (line.vendorBillOcrDraftLineId === lineId ? updater(line) : line)),
    }));
  };

  const addLine = () => {
    setReviewForm(current => ({
      ...current,
      lines: [...current.lines, createBlankLineDraft()],
    }));
  };

  const removeLine = (lineId: string) => {
    setReviewForm(current => {
      const line = current.lines.find(item => item.vendorBillOcrDraftLineId === lineId);
      if (!line) {
        return current;
      }

      return {
        ...current,
        lines: current.lines.filter(item => item.vendorBillOcrDraftLineId !== lineId),
        removedLineIds: line.isNew ? current.removedLineIds : [...current.removedLineIds, lineId],
      };
    });
  };

  const buildReviewRequest = (): VendorBillOcrDraftUpdateRequest => {
    const parseLineTotal = (line: ReviewLineState) => {
      const explicitLineTotal = line.reviewedLineTotal.trim() === '' ? Number.NaN : Number(line.reviewedLineTotal);
      if (Number.isFinite(explicitLineTotal) && explicitLineTotal >= 0) {
        return explicitLineTotal;
      }

      const quantity = Number(line.reviewedQuantity);
      const unitCost = Number(line.reviewedUnitCost);
      return Number.isFinite(quantity) && Number.isFinite(unitCost) ? quantity * unitCost : 0;
    };

    const updatedLines = reviewForm.lines
      .filter(line => !line.isNew)
      .map(line => ({
        vendorBillOcrDraftLineId: line.vendorBillOcrDraftLineId,
        reviewedDescription: line.reviewedDescription.trim(),
        reviewedQuantity: Number(line.reviewedQuantity),
        reviewedUnitCost: Number(line.reviewedUnitCost),
        reviewedLineTotal: line.reviewedLineTotal.trim() === '' ? null : parseLineTotal(line),
        selectedInventoryItemId: line.selectedInventoryItemId || null,
        isIgnored: line.isIgnored,
      }));

    const addedLines: VendorBillOcrDraftLineCreateRequest[] = reviewForm.lines
      .filter(line => line.isNew)
      .map(line => ({
        reviewedDescription: line.reviewedDescription.trim(),
        reviewedQuantity: Number(line.reviewedQuantity),
        reviewedUnitCost: Number(line.reviewedUnitCost),
        reviewedLineTotal: parseLineTotal(line),
        selectedInventoryItemId: line.selectedInventoryItemId || null,
        isIgnored: line.isIgnored,
      }));

    return {
      reviewedVendorId: reviewForm.reviewedVendorId || null,
      reviewedBillNumber: reviewForm.reviewedBillNumber || null,
      reviewedBillDate: reviewForm.reviewedBillDate || null,
      reviewedTotalAmount,
      lines: updatedLines,
      addedLines,
      removedLineIds: reviewForm.removedLineIds,
    };
  };

  const hasBlockingErrors = useMemo(() => {
    if (!selectedDraft) {
      return false;
    }

    if (!reviewForm.reviewedVendorId.trim()) {
      return true;
    }

    if (reviewForm.lines.length === 0) {
      return true;
    }

    if (selectedDraft.hasDuplicateReceipt && !selectedDraft.canOverrideDuplicateReceipt) {
      return true;
    }

    return reviewForm.lines.some(line => {
      const description = line.reviewedDescription.trim();
      const quantity = Number(line.reviewedQuantity);
      const unitCost = Number(line.reviewedUnitCost);
      const lineTotal = Number(line.reviewedLineTotal);
      const quantityValid = Number.isFinite(quantity) && quantity > 0;
      const unitCostValid = Number.isFinite(unitCost) && unitCost >= 0;
      const lineTotalValid = line.reviewedLineTotal.trim() === '' || (Number.isFinite(lineTotal) && lineTotal >= 0);
      const mappingValid = line.isIgnored || Boolean(line.selectedInventoryItemId.trim());

      return !description || !quantityValid || !unitCostValid || !lineTotalValid || !mappingValid;
    });
  }, [reviewForm.lines, reviewForm.reviewedVendorId, selectedDraft]);

  const handleUploadSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setNotice(null);
    setUploadError(null);

    if (!uploadFile) {
      setUploadError(t('vendorOcr.fileRequired'));
      return;
    }

    if (!allowedTypes.has(uploadFile.type)) {
      setUploadError(t('vendorOcr.invalidFileType'));
      return;
    }

    if (uploadFile.size > maxUploadBytes) {
      setUploadError(t('vendorOcr.fileTooLarge'));
      return;
    }

    setUploadSubmitting(true);

    try {
      const draft = await uploadVendorBillOcrDraft(branchId, uploadFile);
      setNotice(t('vendorOcr.reviewAfterUpload'));
      setUploadFile(null);
      await loadDrafts();
      setSelectedDraft(draft);
      setSelectedDraftId(draft.vendorBillOcrDraftId);
      setReviewForm(createDraftForm(draft));
    } catch (error) {
      setUploadError(resolveSafeMessage(error, t('vendorOcr.couldNotUpload')));
    } finally {
      setUploadSubmitting(false);
    }
  };

  const handleSaveReview = async () => {
    if (!selectedDraft) {
      return;
    }

    setReviewSubmitting(true);
    setReviewError(null);

    try {
      const request = buildReviewRequest();
      const updated = await updateVendorBillOcrDraft(selectedDraft.vendorBillOcrDraftId, request);
      setSelectedDraft(updated);
      setReviewForm(createDraftForm(updated));
      await loadDrafts();
      setNotice(t('vendorOcr.reviewSaved'));
    } catch (error) {
      setReviewError(resolveSafeMessage(error, t('vendorOcr.couldNotSaveReview')));
    } finally {
      setReviewSubmitting(false);
    }
  };

  const handleConfirm = async () => {
    if (!selectedDraft) {
      return;
    }

    setReviewSubmitting(true);
    setReviewError(null);

    try {
      const request = buildReviewRequest();
      const updated = await updateVendorBillOcrDraft(selectedDraft.vendorBillOcrDraftId, request);
      setSelectedDraft(updated);
      setReviewForm(createDraftForm(updated));

      const bill = await confirmVendorBillOcrDraft(selectedDraft.vendorBillOcrDraftId);
      setNotice(t('vendorOcr.billCreatedNotice', { id: bill.billNumber?.trim() || '-' }));
      onVendorBillCreated?.(bill.vendorBillId);
      await loadDrafts();
      await loadDraft(selectedDraft.vendorBillOcrDraftId);
    } catch (error) {
      setReviewError(resolveSafeMessage(error, t('vendorOcr.couldNotConfirm')));
    } finally {
      setReviewSubmitting(false);
    }
  };

  const isLowConfidence =
    selectedDraft?.extractedConfidenceScore !== null &&
    selectedDraft?.extractedConfidenceScore !== undefined &&
    selectedDraft.extractedConfidenceScore < lowConfidenceThreshold;

  const lineStatus = (line: ReviewLineState) =>
    line.isIgnored ? t('vendorOcr.lineStatusIgnored') : line.selectedInventoryItemId ? t('vendorOcr.lineStatusMapped') : t('vendorOcr.lineStatusNeedsMapping');

  return (
    <div className="admin-workspace-stack">
      <Card
        title={t('vendorOcr.uploadTitle')}
        description={t('vendorOcr.uploadDescription')}
        tone="admin"
      >
        <form className="admin-form" onSubmit={handleUploadSubmit} noValidate>
          <div className="admin-form-grid">
            <Input
              label={t('vendorOcr.fileLabel')}
              type="file"
              accept=".jpg,.jpeg,.png,.pdf,image/jpeg,image/png,application/pdf"
              onChange={event => {
                const nextFile = event.target.files?.[0] ?? null;
                setUploadFile(nextFile);
                if (nextFile && !allowedTypes.has(nextFile.type)) {
                  setUploadError(t('vendorOcr.invalidFileType'));
                } else {
                  setUploadError(null);
                }
              }}
              helperText={t('vendorOcr.fileHelper', { maxMb: Math.round(maxUploadBytes / (1024 * 1024)) })}
              error={uploadError ?? undefined}
            />
            <Input
              label={t('vendorOcr.selectedFileLabel')}
              value={uploadFile?.name ?? ''}
              onChange={() => undefined}
              readOnly
              helperText={t('vendorOcr.selectedFileHelper')}
            />
          </div>
          {uploadError ? (
            <div className="admin-form-error" role="alert">
              {uploadError}
            </div>
          ) : null}
          <div className="admin-form-actions">
            <Button type="submit" disabled={uploadSubmitting}>
              {t('vendorOcr.reviewExtractedButton')}
            </Button>
          </div>
        </form>
      </Card>

      <Card
        title={t('vendorOcr.draftsTitle')}
        description={t('vendorOcr.draftsDescription')}
        tone="admin"
      >
        {draftsLoading ? (
          <EmptyState title={t('vendorOcr.loadingDraftsTitle')} description={t('vendorOcr.loadingDraftsDescription')} tone="admin" />
        ) : draftsError ? (
          <EmptyState title={t('vendorOcr.couldNotLoadDraftsTitle')} description={draftsError} tone="admin" actionLabel={t('vendor.tryAgain')} onAction={() => void loadDrafts()} />
        ) : drafts.length === 0 ? (
          <EmptyState title={t('vendorOcr.noDraftsTitle')} description={t('vendorOcr.noDraftsDescription')} tone="admin" />
        ) : (
          <div className="admin-workspace-stack">
            <Select
              label={t('vendorOcr.draftSelectLabel')}
              value={selectedDraftId ?? ''}
              onChange={event => setSelectedDraftId(event.target.value)}
            >
              {drafts.map(draft => (
                <option key={draft.vendorBillOcrDraftId} value={draft.vendorBillOcrDraftId}>
                  {draft.originalFileName} - {draft.status}
                </option>
              ))}
            </Select>
            <ResponsiveDataList
              rows={drafts.map(draft => ({
                id: draft.vendorBillOcrDraftId,
                file: draft.originalFileName,
                status: draft.status,
                createdAt: draft.createdAtUtc.slice(0, 10),
                updatedAt: draft.updatedAtUtc.slice(0, 10),
              }))}
              columns={[
                { key: 'file', label: t('vendorOcr.colFile') },
                {
                  key: 'status',
                  label: t('vendorOcr.colStatus'),
                  render: row => <StatusBadge status={row.status} label={row.status} />,
                },
                { key: 'createdAt', label: t('vendorOcr.colCreated') },
                { key: 'updatedAt', label: t('vendorOcr.colUpdated') },
              ]}
              mobileTitle={row => row.file}
              mobileDescription={row => row.status}
              emptyTitle={t('vendorOcr.noDraftsTitle')}
              emptyDescription={t('vendorOcr.noDraftsDescription')}
            />
          </div>
        )}
      </Card>

      <Card
        title={selectedDraft ? t('vendorOcr.reviewTitle', { fileName: selectedDraft.originalFileName }) : t('vendorOcr.reviewTitleGeneric')}
        description={t('vendorOcr.reviewDescription')}
        tone="admin"
      >
        {selectedDraftLoading ? (
          <EmptyState title={t('vendorOcr.loadingDraftTitle')} description={t('vendorOcr.loadingDraftDescription')} tone="admin" />
        ) : selectedDraftError ? (
          <EmptyState title={t('vendorOcr.couldNotLoadDraftTitle')} description={selectedDraftError} tone="admin" />
        ) : selectedDraft ? (
          <div className="admin-workspace-stack">
            {selectedDraft.safeErrorMessage ? <Badge tone="warning" label={selectedDraft.safeErrorMessage} /> : null}
            {selectedDraft.hasDuplicateReceipt ? (
              <div className="admin-workspace-stack" aria-label="duplicate receipt warning">
                <Badge
                  tone={selectedDraft.canOverrideDuplicateReceipt ? 'warning' : 'danger'}
                  label={selectedDraft.duplicateReceiptWarning ?? 'Potential duplicate receipt detected.'}
                />
                {selectedDraft.canOverrideDuplicateReceipt ? (
                  <Badge tone="warning" label={t('vendorOcr.duplicateOverrideBadge')} />
                ) : (
                  <Badge tone="danger" label={t('vendorOcr.duplicateBlockedBadge')} />
                )}
              </div>
            ) : null}
            {selectedDraft.providerWarnings.length > 0 ? (
              <div className="admin-workspace-stack" aria-label="OCR warnings">
                <Badge tone="warning" label={t('vendorOcr.reviewRequiredBadge')} />
                {isLowConfidence ? <Badge tone="warning" label={t('vendorOcr.lowConfidenceBadge')} /> : null}
                {selectedDraft.providerWarnings.map(warning => (
                  <Badge key={warning} tone="warning" label={warning} />
                ))}
              </div>
            ) : isLowConfidence ? (
              <Badge tone="warning" label={t('vendorOcr.lowConfidenceBadge')} />
            ) : null}
            {notice ? <Badge tone="success" label={notice} /> : null}
            {reviewError ? <div className="admin-form-error">{reviewError}</div> : null}

            <div className="admin-form-grid">
              <Select
                label={t('vendorOcr.vendorLabel')}
                value={reviewForm.reviewedVendorId}
                onChange={event => setReviewForm(current => ({ ...current, reviewedVendorId: event.target.value }))}
              >
                <option value="">{t('vendorOcr.vendorSelectPlaceholder')}</option>
                {vendorOptions}
              </Select>
              <Input
                label={t('vendorOcr.billNumberLabel')}
                value={reviewForm.reviewedBillNumber}
                onChange={event => setReviewForm(current => ({ ...current, reviewedBillNumber: event.target.value }))}
              />
              <Input
                label={t('vendorOcr.billDateLabel')}
                type="date"
                value={reviewForm.reviewedBillDate}
                onChange={event => setReviewForm(current => ({ ...current, reviewedBillDate: event.target.value }))}
              />
              <Input
                label={t('vendorOcr.totalAmountLabel')}
                value={formatMoney(reviewedTotalAmount)}
                readOnly
                helperText={t('vendorOcr.totalAmountHelper')}
              />
            </div>

            <ResponsiveDataList
              rows={reviewForm.lines.map(line => ({
                id: line.vendorBillOcrDraftLineId,
                description: line.reviewedDescription,
                quantity: String(line.reviewedQuantity),
                unitCost: formatMoney(Number(line.reviewedUnitCost) || 0),
                lineTotal: formatMoney(
                  (() => {
                    const explicitLineTotal = line.reviewedLineTotal.trim() === '' ? Number.NaN : Number(line.reviewedLineTotal);
                    return Number.isFinite(explicitLineTotal) && explicitLineTotal >= 0
                      ? explicitLineTotal
                      : (Number(line.reviewedQuantity) || 0) * (Number(line.reviewedUnitCost) || 0);
                  })()
                ),
                inventoryItemId: line.selectedInventoryItemId,
                status: lineStatus(line),
              }))}
              columns={[
                { key: 'description', label: t('vendorOcr.colDescription') },
                { key: 'quantity', label: t('vendorOcr.colQuantity'), align: 'right' },
                { key: 'unitCost', label: t('vendorOcr.colUnitCost'), align: 'right' },
                { key: 'lineTotal', label: t('vendorOcr.colLineTotal'), align: 'right' },
                {
                  key: 'inventoryItemId',
                  label: t('vendorOcr.colInventoryItem'),
                  render: row => (
                    <Badge
                      tone={row.status === t('vendorOcr.lineStatusIgnored') ? 'warning' : row.inventoryItemId ? 'success' : 'neutral'}
                      label={row.status}
                    />
                  ),
                },
              ]}
              mobileTitle={row => row.description}
              mobileDescription={row => t('vendorOcr.lineMobileDescription', { qty: row.quantity, cost: row.unitCost, status: row.status })}
              emptyTitle={t('vendorOcr.noDraftsTitle')}
              emptyDescription={t('vendorOcr.noDraftsDescription')}
            />

            <div className="admin-form-actions">
              <Button type="button" variant="secondary" onClick={addLine}>
                {t('vendorOcr.addLineButton')}
              </Button>
            </div>

            <div className="vendor-bill-lines">
              {reviewForm.lines.map(line => (
                <div key={line.vendorBillOcrDraftLineId} className="vendor-bill-line">
                  <div className="vendor-bill-line__header">
                    <div className="vendor-bill-line__badges">
                      <Badge tone={line.isIgnored ? 'warning' : line.selectedInventoryItemId ? 'success' : 'neutral'} label={lineStatus(line)} />
                      {line.isNew ? <Badge tone="accent" label={t('vendorOcr.newLineBadge')} /> : null}
                    </div>
                    <Button type="button" variant="secondary" size="sm" onClick={() => removeLine(line.vendorBillOcrDraftLineId)}>
                      {t('vendorOcr.removeLineButton')}
                    </Button>
                  </div>
                  <div className="admin-form-grid">
                    <Input
                      label={t('vendorOcr.lineDescriptionLabel')}
                      value={line.reviewedDescription}
                      onChange={event =>
                        updateLine(line.vendorBillOcrDraftLineId, current => ({ ...current, reviewedDescription: event.target.value }))
                      }
                    />
                    <Input
                      label={t('vendorOcr.lineQuantityLabel')}
                      type="number"
                      min="0.01"
                      step="0.01"
                      value={line.reviewedQuantity}
                      onChange={event =>
                        updateLine(line.vendorBillOcrDraftLineId, current => ({ ...current, reviewedQuantity: event.target.value }))
                      }
                    />
                    <Input
                      label={t('vendorOcr.lineUnitCostLabel')}
                      type="number"
                      min="0"
                      step="0.01"
                      value={line.reviewedUnitCost}
                      onChange={event =>
                        updateLine(line.vendorBillOcrDraftLineId, current => ({ ...current, reviewedUnitCost: event.target.value }))
                      }
                    />
                    <Input
                      label={t('vendorOcr.lineLineTotalLabel')}
                      type="number"
                      min="0"
                      step="0.01"
                      value={line.reviewedLineTotal}
                      onChange={event =>
                        updateLine(line.vendorBillOcrDraftLineId, current => ({ ...current, reviewedLineTotal: event.target.value }))
                      }
                    />
                    <Checkbox
                      label={t('vendorOcr.lineIgnoreLabel')}
                      checked={line.isIgnored}
                      onChange={event =>
                        updateLine(line.vendorBillOcrDraftLineId, current => ({
                          ...current,
                          isIgnored: event.target.checked,
                          selectedInventoryItemId: event.target.checked ? '' : current.selectedInventoryItemId,
                        }))
                      }
                      helperText={t('vendorOcr.lineIgnoreHelper')}
                    />
                    <Select
                      label={t('vendorOcr.lineInventoryItemLabel')}
                      value={line.selectedInventoryItemId}
                      disabled={line.isIgnored}
                      onChange={event =>
                        updateLine(line.vendorBillOcrDraftLineId, current => ({ ...current, selectedInventoryItemId: event.target.value }))
                      }
                      helperText={line.isIgnored ? t('vendorOcr.lineInventoryItemIgnoredHelper') : t('vendorOcr.lineInventoryItemHelper')}
                    >
                      <option value="">{t('vendorOcr.lineNoInventoryItem')}</option>
                      {inventoryOptions}
                    </Select>
                  </div>
                </div>
              ))}
            </div>

            <div className="admin-form-actions">
              <Button type="button" variant="secondary" onClick={() => void handleSaveReview()} disabled={reviewSubmitting}>
                {t('vendorOcr.saveReviewButton')}
              </Button>
              <Button type="button" onClick={() => void handleConfirm()} disabled={reviewSubmitting || hasBlockingErrors}>
                {t('vendorOcr.createVendorBillButton')}
              </Button>
            </div>

            {hasBlockingErrors ? (
              <Badge tone="warning" label={t('vendorOcr.blockingErrorsBadge')} />
            ) : null}

            {selectedDraft.confirmedVendorBillId ? (
              <EmptyState
                title={t('vendorOcr.billCreatedTitle')}
                description={t('vendorOcr.billCreatedDescription', { id: selectedDraft.confirmedVendorBillId })}
                tone="admin"
              />
            ) : null}
          </div>
        ) : (
          <EmptyState title={t('vendorOcr.chooseDraftTitle')} description={t('vendorOcr.chooseDraftDescription')} tone="admin" />
        )}
      </Card>
    </div>
  );
};

export default VendorBillOcrDraftPanel;
