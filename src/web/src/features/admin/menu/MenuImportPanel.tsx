import { useEffect, useMemo, useState } from 'react';
import type { RefObject } from 'react';

import { getSafeApiErrorMessage, isApiError } from '../../../api/apiErrors';
import { Badge, Button, Card, EmptyState, Input, ResponsiveDataList, Select, SummaryCard } from '../../../components/ui';
import { useAuth } from '../../auth/useAuth';
import { useRestaurantCurrency } from '../../auth/useRestaurantCurrency';
import { useLanguage } from '../../../i18n/LanguageProvider';
import { confirmMenuImport, previewMenuImport } from '../adminApi';
import type { MenuImportResponse } from '../adminTypes';
import { formatMenuPrice } from './menuDisplay';

interface MenuImportPanelProps {
  onClose: () => void;
  onImported: () => Promise<void> | void;
  textareaRef?: RefObject<HTMLTextAreaElement | null>;
}

const SAMPLE_CSV = [
  'Category,ItemName,Description,EatInPrice,Available,BranchName',
  'Breakfast,Idli,Steamed rice cakes,2.50,Yes,',
  'Breakfast,Dosa,Crisp rice crepe,3.50,Yes,',
].join('\n');

const resolveSafeMessage = (error: unknown, fallback: string) => getSafeApiErrorMessage(error, fallback);

const getStatusTone = (status: string) => {
  switch (status) {
    case 'Imported':
    case 'Updated':
      return 'success';
    case 'Duplicate':
    case 'Warning':
      return 'warning';
    case 'Invalid':
    case 'Failed':
      return 'danger';
    case 'Skipped':
      return 'neutral';
    default:
      return 'accent';
  }
};

const formatAvailable = (value: boolean | null, yesLabel: string, noLabel: string, notSpecifiedLabel: string) => {
  if (value === null) {
    return notSpecifiedLabel;
  }

  return value ? yesLabel : noLabel;
};

export const MenuImportPanel = ({ onClose, onImported, textareaRef }: MenuImportPanelProps) => {
  const { t } = useLanguage();
  const auth = useAuth();
  const { currencyCode, locale } = useRestaurantCurrency();
  const [importName, setImportName] = useState('Pasted CSV');
  const [csvText, setCsvText] = useState(SAMPLE_CSV);
  const [preview, setPreview] = useState<MenuImportResponse | null>(null);
  const [previewLoading, setPreviewLoading] = useState(false);
  const [submitLoading, setSubmitLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [decisionByRow, setDecisionByRow] = useState<Record<number, 'Import' | 'Update' | 'Skip'>>({});

  useEffect(() => {
    setPreview(null);
    setPreviewLoading(false);
    setSubmitLoading(false);
    setError(null);
    setDecisionByRow({});
    setImportName('Pasted CSV');
    setCsvText(SAMPLE_CSV);
  }, []);

  const duplicateRows = useMemo(
    () => preview?.rows.filter(row => row.isDuplicate) ?? [],
    [preview]
  );

  const blockingErrors = useMemo(
    () => preview?.rows.some(row => row.errors.length > 0) ?? false,
    [preview]
  );

  const summary = preview?.summary ?? null;

  const handlePreview = async () => {
    setPreviewLoading(true);
    setError(null);

    try {
      const response = await previewMenuImport({
        csvText,
        importName: importName.trim() || null,
      });
      setPreview(response);
      setDecisionByRow(prev => {
        const next: Record<number, 'Import' | 'Update' | 'Skip'> = {};
        for (const row of response.rows) {
          if (row.isDuplicate) {
            next[row.rowNumber] = prev[row.rowNumber] ?? 'Skip';
          }
        }
        return next;
      });
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not preview the CSV right now.');
      setError(message);
      setPreview(null);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setPreviewLoading(false);
    }
  };

  const handleConfirm = async () => {
    if (!preview) {
      return;
    }

    setSubmitLoading(true);
    setError(null);

    try {
      const decisions = duplicateRows.map(row => ({
        rowNumber: row.rowNumber,
        action: decisionByRow[row.rowNumber] ?? 'Skip',
      }));

      const response = await confirmMenuImport({
        csvText,
        importName: importName.trim() || null,
        decisions,
      });

      setPreview(response);
      await Promise.resolve(onImported()).catch(() => undefined);
      setError(null);
    } catch (caughtError) {
      const message = resolveSafeMessage(caughtError, 'Could not import the CSV right now.');
      setError(message);
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setSubmitLoading(false);
    }
  };

  const hasPreview = Boolean(preview);
  const canConfirm = Boolean(preview && !blockingErrors);

  return (
    <Card
      title={t('menu.importCardTitle')}
      description={t('menu.importCardDescription')}
      tone="admin"
      actions={<Button variant="secondary" onClick={onClose}>{t('menu.importCloseButton')}</Button>}
    >
      <div className="admin-controls">
        <div className="admin-form-grid">
          <Input
            label={t('menu.importNameLabel')}
            value={importName}
            onChange={event => setImportName(event.target.value)}
            helperText={t('menu.importNameHelper')}
          />
        </div>

        <div className="admin-form-section">
          <div className="admin-form-section__heading">{t('menu.csvSourceHeading')}</div>
          <div className="admin-form-note">
            {t('menu.csvSourceNote')}
          </div>
          <textarea
            className="ui-input vendor-textarea"
            ref={textareaRef}
            value={csvText}
            onChange={event => setCsvText(event.target.value)}
            rows={10}
            aria-label={t('menu.csvAriaLabel')}
          />
          <div className="admin-controls">
            <Button variant="secondary" onClick={() => setCsvText(SAMPLE_CSV)}>
              {t('menu.importLoadSampleButton')}
            </Button>
            <Button variant="secondary" onClick={() => setCsvText('')}>
              {t('menu.importClearButton')}
            </Button>
            <Button onClick={() => void handlePreview()} disabled={previewLoading || submitLoading}>
              {previewLoading ? t('menu.importPreviewingButton') : t('menu.importPreviewCsvButton')}
            </Button>
          </div>
        </div>

        {error ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {error}
          </div>
        ) : null}

        {summary ? (
          <div className="summary-grid">
            <SummaryCard label={t('menu.importSummaryRows')} value={summary.totalRows.toString()} tone="admin" detail={t('menu.importSummaryRowsDetail')} />
            <SummaryCard label={t('menu.importSummaryReady')} value={summary.readyRows.toString()} tone="accent" detail={t('menu.importSummaryReadyDetail')} />
            <SummaryCard label={t('menu.importSummaryDuplicates')} value={summary.duplicateRows.toString()} tone="inventory" detail={t('menu.importSummaryDuplicatesDetail')} />
            <SummaryCard label={t('menu.importSummaryInvalid')} value={summary.invalidRows.toString()} tone="orders" detail={t('menu.importSummaryInvalidDetail')} />
          </div>
        ) : null}

        {hasPreview ? (
          <>
            <div className="summary-grid">
              <SummaryCard label={t('menu.importSummaryImported')} value={summary?.importedRows.toString() ?? '0'} tone="accent" detail={t('menu.importSummaryImportedDetail')} />
              <SummaryCard label={t('menu.importSummaryUpdated')} value={summary?.updatedRows.toString() ?? '0'} tone="admin" detail={t('menu.importSummaryUpdatedDetail')} />
              <SummaryCard label={t('menu.importSummarySkipped')} value={summary?.skippedRows.toString() ?? '0'} tone="inventory" detail={t('menu.importSummarySkippedDetail')} />
              <SummaryCard label={t('menu.importSummaryFailed')} value={summary?.failedRows.toString() ?? '0'} tone="orders" detail={t('menu.importSummaryFailedDetail')} />
            </div>

            <ResponsiveDataList
              rows={(preview?.rows ?? []).map(row => ({
                id: String(row.rowNumber),
                rowNumber: row.rowNumber,
                category: row.category,
                itemName: row.itemName,
                description: row.description ?? t('menu.importNotProvided'),
                eatInPrice: row.eatInPrice,
                available: row.available,
                branchName: row.branchName ?? t('menu.importAllBranches'),
                status: row.status,
                message: row.message,
                isDuplicate: row.isDuplicate,
                action: decisionByRow[row.rowNumber] ?? row.suggestedAction,
              }))}
              columns={[
                { key: 'rowNumber', label: t('menu.importColumnRow') },
                { key: 'category', label: t('menu.importColumnCategory') },
                { key: 'itemName', label: t('menu.importColumnItem') },
                {
                  key: 'eatInPrice',
                  label: t('menu.importColumnEatInPrice'),
                  align: 'right',
                  render: row => (row.eatInPrice === null ? t('menu.importNotProvided') : formatMenuPrice(row.eatInPrice, currencyCode, locale)),
                },
                { key: 'available', label: t('menu.importColumnAvailable'), render: row => formatAvailable(row.available, t('menu.importYes'), t('menu.importNo'), t('menu.importNotSpecified')) },
                { key: 'branchName', label: t('menu.importColumnBranchName') },
                {
                  key: 'status',
                  label: t('menu.importColumnStatus'),
                  render: row => <Badge tone={getStatusTone(row.status) as 'accent' | 'neutral' | 'success' | 'warning' | 'danger'} label={row.status} />,
                },
                { key: 'message', label: t('menu.importColumnMessage') },
                {
                  key: 'action',
                  label: t('menu.importColumnAction'),
                  render: row =>
                    row.isDuplicate ? (
                      <Select
                        label={t('menu.importDuplicateActionLabel')}
                        value={row.action}
                        onChange={event =>
                          setDecisionByRow(current => ({
                            ...current,
                            [row.rowNumber]: event.target.value as 'Import' | 'Update' | 'Skip',
                          }))
                        }
                        helperText={t('menu.importDuplicateActionHelper')}
                      >
                        <option value="Skip">{t('menu.importSkip')}</option>
                        <option value="Update">{t('menu.importUpdate')}</option>
                      </Select>
                    ) : (
                      <span>{row.action}</span>
                    ),
                },
              ]}
              mobileTitle={row => `${row.rowNumber}. ${row.itemName}`}
              mobileDescription={row => `${row.category} · ${row.status} · ${row.message}`}
              emptyTitle={t('menu.importEmptyTitle')}
              emptyDescription={t('menu.importEmptyDescription')}
            />

            {duplicateRows.length > 0 ? (
              <div className="admin-form-note">
                {t('menu.importDuplicateNote')}
              </div>
            ) : null}

            {blockingErrors ? (
              <div className="admin-notice admin-notice--warning" role="status">
                {t('menu.importBlockingErrorsWarning')}
              </div>
            ) : null}

            <div className="admin-controls">
              <Button variant="secondary" onClick={() => void handlePreview()} disabled={previewLoading || submitLoading}>
                {t('menu.importRefreshPreviewButton')}
              </Button>
              <Button onClick={() => void handleConfirm()} disabled={!canConfirm || submitLoading}>
                {submitLoading ? t('menu.importImportingButton') : t('menu.importConfirmImportButton')}
              </Button>
            </div>
          </>
        ) : (
          <EmptyState
            title={t('menu.importPreviewEmptyTitle')}
            description={t('menu.importPreviewEmptyDescription')}
            tone="admin"
          />
        )}
      </div>
    </Card>
  );
};

export default MenuImportPanel;
