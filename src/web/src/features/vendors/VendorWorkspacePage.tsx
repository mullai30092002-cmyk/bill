import { useCallback, useEffect, useId, useMemo, useRef, useState, type FormEvent } from 'react';
import { useNavigate } from 'react-router-dom';

import { useLanguage } from '../../i18n/LanguageProvider';

import { isApiError } from '../../api/apiErrors';
import { AdminLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { Badge, Button, Card, Checkbox, EmptyState, Input, ResponsiveDataList, Select, StatusBadge, SummaryCard } from '../../components/ui';
import { useAuth } from '../auth/useAuth';
import { useRestaurantCurrency } from '../auth/useRestaurantCurrency';
import { formatCurrency } from '../finance/currencyDisplay';
import { listAdminBranches } from '../admin/adminApi';
import type { AdminBranchListItem } from '../admin/adminTypes';
import { sortBranches } from '../admin/branches/branchDisplay';
import { listInventoryItems } from '../inventory/inventoryApi';
import type { InventoryItemListItem } from '../inventory/inventoryTypes';
import { getVendorPayablesReport } from '../reports/vendorPayablesReportApi';
import { formatVendorPayablesCurrency, formatVendorPayablesDateInput } from '../reports/vendorPayablesReportDisplay';
import type { VendorPayablesReportResponse } from '../reports/vendorPayablesReportTypes';
import { getVendorStatement } from './vendorStatementApi';
import type { VendorStatementResponse } from './vendorStatementTypes';
import { getSafeVendorErrorMessage } from './vendorErrorDisplay';
import VendorBillOcrDraftPanel from './VendorBillOcrDraftPanel';
import VendorSettlementDialog, {
  type VendorSettlementFormErrors,
  type VendorSettlementFormState,
} from './VendorSettlementDialog';
import {
  createVendor,
  createVendorBill,
  getVendorBill,
  listVendorBills,
  listVendors,
  recordVendorSettlement,
  updateVendor,
} from './vendorApi';
import type {
  CreateVendorBillLineRequest,
  CreateVendorBillRequest,
  CreateVendorRequest,
  RecordVendorSettlementRequest,
  UpdateVendorRequest,
  VendorBillDetail,
  VendorBillListItem,
  VendorDetail,
} from './vendorTypes';

export interface VendorWorkspacePageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

type NoticeTone = 'success' | 'info' | 'warning' | 'danger';

interface Notice {
  tone: NoticeTone;
  message: string;
}

interface VendorFormState {
  name: string;
  vendorType: string;
  contactName: string;
  mobileNumber: string;
  address: string;
  notes: string;
  isActive: boolean;
}

interface VendorFormErrors {
  name?: string;
  vendorType?: string;
  mobileNumber?: string;
}

interface VendorBillLineFormState {
  lineId: string;
  inventoryItemId: string;
  description: string;
  quantity: string;
  unitCost: string;
}

interface VendorBillFormState {
  vendorId: string;
  billNumber: string;
  billDate: string;
  dueDate: string;
  notes: string;
  lines: VendorBillLineFormState[];
}

interface VendorBillFormErrors {
  vendorId?: string;
  billDate?: string;
  lines?: string;
}

const vendorTypeOptions = ['Groceries', 'Wood', 'Gas', 'Water', 'Snacks', 'Juice', 'Fruits', 'Other'];
const settlementModeOptions = ['Cash', 'UPI', 'Card', 'BankTransfer', 'Other'];

const emptyVendorForm = (): VendorFormState => ({
  name: '',
  vendorType: 'Groceries',
  contactName: '',
  mobileNumber: '',
  address: '',
  notes: '',
  isActive: true,
});

const emptyVendorBillLine = (): VendorBillLineFormState => ({
  lineId: createLineId(),
  inventoryItemId: '',
  description: '',
  quantity: '1',
  unitCost: '',
});

const emptyVendorBillForm = (vendorId = '', branchId = '', billDate = todayInputValue()): VendorBillFormState => ({
  vendorId,
  billNumber: '',
  billDate,
  dueDate: '',
  notes: '',
  lines: [emptyVendorBillLine()],
});

const emptySettlementForm = (): VendorSettlementFormState => ({
  paymentMode: 'Cash',
  amount: '',
  referenceNumber: '',
  notes: '',
  paidAtUtc: new Date().toISOString(),
});

const todayInputValue = () => new Date().toISOString().slice(0, 10);

const createLineId = () => {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) {
    return crypto.randomUUID();
  }

  return `${Date.now()}-${Math.random().toString(36).slice(2)}`;
};

const normalizeOptionalText = (value: string) => {
  const trimmed = value.trim();
  return trimmed ? trimmed : null;
};

const normalizeSafeDate = (value: string) => value.trim().slice(0, 10);

const formatDate = (value: string | null | undefined) => (value ? value.slice(0, 10) : '-');
const formatBillNumber = (value: string | null | undefined) => value?.trim() || '-';

const resolveSafeMessage = (error: unknown, fallback: string, t?: TFn) => {
  return getSafeVendorErrorMessage(
    error,
    fallback,
    t
      ? {
          sessionExpired: t('vendor.sessionExpired'),
          unauthorized: t('vendor.notAuthorizedChange'),
        }
      : undefined
  );
};

type TFn = (key: Parameters<ReturnType<typeof useLanguage>['t']>[0]) => string;

const buildVendorErrors = (form: VendorFormState, t: TFn): VendorFormErrors => {
  const errors: VendorFormErrors = {};

  if (!form.name.trim()) {
    errors.name = t('vendor.validationVendorNameRequired');
  }

  if (!form.vendorType.trim()) {
    errors.vendorType = t('vendor.validationVendorTypeRequired');
  }

  if (!form.mobileNumber.trim()) {
    errors.mobileNumber = t('vendor.validationVendorMobileRequired');
  }

  return errors;
};

const buildBillErrors = (form: VendorBillFormState, t: TFn): VendorBillFormErrors => {
  const errors: VendorBillFormErrors = {};

  if (!form.vendorId) {
    errors.vendorId = t('vendor.validationBillVendorRequired');
  }

  if (!form.billDate.trim()) {
    errors.billDate = t('vendor.validationBillDateRequired');
  }

  const lineErrors = form.lines.some(line => {
    const quantity = Number(line.quantity);
    const unitCost = Number(line.unitCost);
    return !line.description.trim() || Number.isNaN(quantity) || quantity <= 0 || Number.isNaN(unitCost) || unitCost < 0;
  });

  if (lineErrors) {
    errors.lines = t('vendor.validationBillLinesInvalid');
  }

  return errors;
};

const buildSettlementErrors = (form: VendorSettlementFormState, balanceAmount: number, t: TFn): VendorSettlementFormErrors => {
  const errors: VendorSettlementFormErrors = {};

  if (!form.paymentMode.trim()) {
    errors.paymentMode = t('vendorSettlement.validationPaymentModeRequired');
  }

  if (form.amount.trim() === '' || Number.isNaN(Number(form.amount)) || Number(form.amount) <= 0) {
    errors.amount = t('vendorSettlement.validationAmountRequired');
  } else if (Number(form.amount) > balanceAmount) {
    errors.amount = t('vendorSettlement.validationAmountExceedsBalance');
  }

  if (
    ['UPI', 'Card', 'BankTransfer'].includes(form.paymentMode) &&
    !form.referenceNumber.trim()
  ) {
    errors.referenceNumber = t('vendorSettlement.validationReferenceRequired');
  }

  return errors;
};

const toVendorRequest = (form: VendorFormState, branchId: string): CreateVendorRequest | UpdateVendorRequest => ({
  branchId,
  name: form.name.trim(),
  vendorType: form.vendorType,
  contactName: normalizeOptionalText(form.contactName),
  mobileNumber: normalizeOptionalText(form.mobileNumber),
  address: normalizeOptionalText(form.address),
  notes: normalizeOptionalText(form.notes),
  isActive: form.isActive,
});

const toBillRequest = (form: VendorBillFormState, branchId: string): CreateVendorBillRequest => ({
  vendorId: form.vendorId,
  branchId,
  billNumber: normalizeOptionalText(form.billNumber),
  billDate: normalizeSafeDate(form.billDate),
  dueDate: form.dueDate.trim() ? normalizeSafeDate(form.dueDate) : null,
  notes: normalizeOptionalText(form.notes),
  lines: form.lines.map<CreateVendorBillLineRequest>(line => ({
    inventoryItemId: line.inventoryItemId || null,
    description: line.description.trim(),
    quantity: Number(line.quantity),
    unitCost: Number(line.unitCost),
  })),
});

const toSettlementRequest = (form: VendorSettlementFormState): RecordVendorSettlementRequest => ({
  paymentMode: form.paymentMode,
  amount: Number(form.amount),
  referenceNumber: normalizeOptionalText(form.referenceNumber),
  paidAtUtc: form.paidAtUtc ? new Date(form.paidAtUtc).toISOString() : new Date().toISOString(),
  notes: normalizeOptionalText(form.notes),
});

const vendorBillStatusTone = (status: string) => {
  switch (status.toLowerCase()) {
    case 'paid':
      return 'success';
    case 'partiallypaid':
      return 'warning';
    case 'cancelled':
      return 'danger';
    default:
      return 'neutral';
  }
};

const vendorSettlementStatusTone = (status: string) => (status.toLowerCase() === 'cancelled' ? 'danger' : 'success');

const TextAreaField = ({
  label,
  value,
  onChange,
  helperText,
  error,
  placeholder,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  helperText?: string;
  error?: string;
  placeholder?: string;
}) => {
  const generatedId = useId();
  const id = `${label.toLowerCase().replace(/[^a-z0-9]+/g, '-')}-${generatedId}`;
  const helperId = helperText || error ? `${id}-hint` : undefined;

  return (
    <label className="ui-field" htmlFor={id}>
      <span className="ui-field__label">{label}</span>
      <textarea
        id={id}
        className={['ui-input', error && 'ui-input--error', 'vendor-textarea'].filter(Boolean).join(' ')}
        value={value}
        onChange={event => onChange(event.target.value)}
        placeholder={placeholder}
        aria-invalid={Boolean(error)}
        aria-describedby={helperId}
        rows={3}
      />
      {error ? (
        <span id={helperId} className="ui-field__helper ui-field__helper--error">
          {error}
        </span>
      ) : helperText ? (
        <span id={helperId} className="ui-field__helper">
          {helperText}
        </span>
      ) : null}
    </label>
  );
};

export const VendorWorkspacePage = ({ navItems, restaurantName, branchName, operatorLabel }: VendorWorkspacePageProps) => {
  const auth = useAuth();
  const { currencyCode: restaurantCurrencyCode, locale } = useRestaurantCurrency();
  const navigate = useNavigate();
  const { t } = useLanguage();
  const canAccess =
    auth.hasPermission('VendorBill.Upload') ||
    auth.hasPermission('VendorBill.ReviewOcr') ||
    auth.hasPermission('VendorBill.OverrideOcr') ||
    auth.hasPermission('VendorBill.Confirm') ||
    auth.hasPermission('VendorPayment.Create');
  const canSwitchBranch = auth.hasPermission('Branch.Manage') || auth.hasPermission('User.Manage');
  const canViewInventory = auth.hasPermission('Inventory.View') || auth.hasPermission('Inventory.Adjust');
  const canViewReport = auth.hasPermission('Report.View');
  const canSettleVendorBills = auth.hasPermission('VendorPayment.Create') || auth.hasPermission('VendorBill.Confirm');

  const [branches, setBranches] = useState<AdminBranchListItem[]>([]);
  const [branchesLoading, setBranchesLoading] = useState(canSwitchBranch);
  const [branchesError, setBranchesError] = useState<string | null>(null);
  const [selectedBranchId, setSelectedBranchId] = useState(auth.session?.branchId ?? '');
  const [vendors, setVendors] = useState<VendorDetail[]>([]);
  const [vendorsLoading, setVendorsLoading] = useState(canAccess);
  const [vendorsError, setVendorsError] = useState<string | null>(null);
  const [vendorMode, setVendorMode] = useState<'create' | 'edit'>('create');
  const [selectedVendorId, setSelectedVendorId] = useState<string | null>(null);
  const [vendorForm, setVendorForm] = useState<VendorFormState>(() => emptyVendorForm());
  const [vendorErrors, setVendorErrors] = useState<VendorFormErrors>({});
  const [vendorSubmitting, setVendorSubmitting] = useState(false);
  const [bills, setBills] = useState<VendorBillListItem[]>([]);
  const [billsLoading, setBillsLoading] = useState(canAccess);
  const [billsError, setBillsError] = useState<string | null>(null);
  const [selectedBillId, setSelectedBillId] = useState<string | null>(null);
  const [selectedBill, setSelectedBill] = useState<VendorBillDetail | null>(null);
  const [selectedBillLoading, setSelectedBillLoading] = useState(false);
  const [selectedBillError, setSelectedBillError] = useState<string | null>(null);
  const [billDetailsScrollToken, setBillDetailsScrollToken] = useState(0);
  const [inventoryItems, setInventoryItems] = useState<InventoryItemListItem[]>([]);
  const [inventoryLoading, setInventoryLoading] = useState(canViewInventory);
  const [inventoryError, setInventoryError] = useState<string | null>(null);
  const [payablesReport, setPayablesReport] = useState<VendorPayablesReportResponse | null>(null);
  const [payablesReportLoading, setPayablesReportLoading] = useState(canViewReport);
  const [payablesReportError, setPayablesReportError] = useState<string | null>(null);
  const [billForm, setBillForm] = useState<VendorBillFormState>(() => emptyVendorBillForm());
  const [billErrors, setBillErrors] = useState<VendorBillFormErrors>({});
  const [billSubmitting, setBillSubmitting] = useState(false);
  const [settlementForm, setSettlementForm] = useState<VendorSettlementFormState>(() => emptySettlementForm());
  const [settlementErrors, setSettlementErrors] = useState<VendorSettlementFormErrors>({});
  const [settlementSubmitting, setSettlementSubmitting] = useState(false);
  const [settlementDialogOpen, setSettlementDialogOpen] = useState(false);
  const [vendorStatement, setVendorStatement] = useState<VendorStatementResponse | null>(null);
  const [vendorStatementLoading, setVendorStatementLoading] = useState(canAccess);
  const [vendorStatementError, setVendorStatementError] = useState<string | null>(null);
  const defaultStatementRange = useMemo(() => {
    const today = new Date();
    const firstOfMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));
    return {
      fromDate: formatVendorPayablesDateInput(firstOfMonth),
      toDate: formatVendorPayablesDateInput(today),
    };
  }, []);
  const [statementFromDate, setStatementFromDate] = useState(defaultStatementRange.fromDate);
  const [statementToDate, setStatementToDate] = useState(defaultStatementRange.toDate);
  const [notice, setNotice] = useState<Notice | null>(null);
  const billDetailsSectionRef = useRef<HTMLDivElement | null>(null);

  const selectedBranch = useMemo(
    () => branches.find(branch => branch.branchId === selectedBranchId) ?? null,
    [branches, selectedBranchId]
  );

  const selectedVendor = useMemo(
    () => vendors.find(vendor => vendor.vendorId === selectedVendorId) ?? null,
    [selectedVendorId, vendors]
  );

  const outstandingBills = useMemo(
    () => bills.filter(bill => bill.status !== 'Cancelled' && bill.balanceAmount > 0),
    [bills]
  );
  const billCount = outstandingBills.length;
  const openBalance = useMemo(
    () => outstandingBills.reduce((sum, bill) => sum + bill.balanceAmount, 0),
    [outstandingBills]
  );
  const activeVendorCount = useMemo(() => vendors.filter(vendor => vendor.isActive).length, [vendors]);
  const paidBillCount = useMemo(() => bills.filter(bill => bill.status === 'Paid').length, [bills]);
  const partialBillCount = useMemo(() => bills.filter(bill => bill.status === 'PartiallyPaid').length, [bills]);
  const reportMonthRange = useMemo(() => {
    const today = new Date();
    const firstOfMonth = new Date(Date.UTC(today.getUTCFullYear(), today.getUTCMonth(), 1));
    return {
      fromDate: formatVendorPayablesDateInput(firstOfMonth),
      toDate: formatVendorPayablesDateInput(today),
    };
  }, []);

  const formatMoney = useCallback(
    (value: number, currencyCode?: string | null) => formatCurrency(value, currencyCode ?? payablesReport?.currencyCode ?? restaurantCurrencyCode, locale),
    [locale, payablesReport?.currencyCode, restaurantCurrencyCode]
  );

  const loadBranches = useCallback(async () => {
    if (!canSwitchBranch) {
      return;
    }

    setBranchesLoading(true);
    setBranchesError(null);

    try {
      const response = await listAdminBranches();
      const activeBranches = sortBranches(response.items.filter(branch => branch.status === 'Active'));
      setBranches(activeBranches);

      const activeBranchIds = new Set(activeBranches.map(branch => branch.branchId));
      const fallbackBranchId = auth.session?.branchId && activeBranchIds.has(auth.session.branchId)
        ? auth.session.branchId
        : activeBranches.length === 1
          ? activeBranches[0].branchId
          : '';

      setSelectedBranchId(current => (current && activeBranchIds.has(current) ? current : fallbackBranchId));
    } catch (caughtError) {
      setBranchesError(resolveSafeMessage(caughtError, t('vendor.couldNotLoadBranches'), t));
      if (isApiError(caughtError) && caughtError.status === 401) {
        void auth.logout();
      }
    } finally {
      setBranchesLoading(false);
    }
  }, [auth, canSwitchBranch, t]);

  const loadPayablesReport = useCallback(
    async (branchId: string) => {
      if (!canViewReport) {
        return;
      }

      setPayablesReportLoading(true);
      setPayablesReportError(null);

      try {
        const response = await getVendorPayablesReport({
          branchId: branchId || undefined,
          fromDate: defaultStatementRange.fromDate,
          toDate: defaultStatementRange.toDate,
        });
        setPayablesReport(response);
      } catch (caughtError) {
        setPayablesReport(null);
        setPayablesReportError(resolveSafeMessage(caughtError, t('vendor.couldNotLoadPayablesSnapshot'), t));
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setPayablesReportLoading(false);
      }
    },
    [auth, canViewReport, defaultStatementRange.fromDate, defaultStatementRange.toDate, t]
  );

  const loadVendorStatement = useCallback(
    async (vendorId: string, branchId: string, fromDate: string, toDate: string) => {
      if (!vendorId) {
        setVendorStatement(null);
        setVendorStatementError(null);
        return;
      }

      setVendorStatementLoading(true);
      setVendorStatementError(null);

      try {
        const response = await getVendorStatement({
          vendorId,
          branchId: branchId || undefined,
          fromDate,
          toDate,
        });
        setVendorStatement(response);
      } catch (caughtError) {
        setVendorStatement(null);
        setVendorStatementError(resolveSafeMessage(caughtError, t('vendor.couldNotLoadVendorStatement'), t));
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setVendorStatementLoading(false);
      }
    },
    [auth, t]
  );

  const loadWorkspace = useCallback(
    async (branchId: string) => {
      if (!canAccess) {
        return;
      }

      setVendorsLoading(true);
      setBillsLoading(true);
      setInventoryLoading(canViewInventory);
      setVendorsError(null);
      setBillsError(null);
      setInventoryError(null);

      try {
        const [vendorsResponse, billsResponse, inventoryResponse] = await Promise.all([
          listVendors({ branchId: branchId || undefined }),
          listVendorBills({ branchId: branchId || undefined }),
          canViewInventory ? listInventoryItems({ branchId: branchId || undefined }) : Promise.resolve({ items: [] }),
        ]);

        setVendors(vendorsResponse.items);
        setBills(billsResponse.items);
        setInventoryItems(inventoryResponse.items);

        setSelectedVendorId(current =>
          current && vendorsResponse.items.some(vendor => vendor.vendorId === current)
            ? current
            : vendorsResponse.items[0]?.vendorId ?? null
        );

        setSelectedBillId(current =>
          current && billsResponse.items.some(bill => bill.vendorBillId === current)
            ? current
            : billsResponse.items[0]?.vendorBillId ?? null
        );
      } catch (caughtError) {
        const message = resolveSafeMessage(caughtError, t('vendor.couldNotLoadWorkspace'), t);
        setVendors([]);
        setBills([]);
        setInventoryItems([]);
        setVendorsError(message);
        setBillsError(message);
        setInventoryError(message);
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setVendorsLoading(false);
        setBillsLoading(false);
        setInventoryLoading(false);
      }
    },
    [auth, canAccess, canViewInventory, t]
  );

  const loadSelectedBill = useCallback(
    async (billId: string) => {
      if (!billId) {
        setSelectedBill(null);
        setSelectedBillError(null);
        return;
      }

      setSelectedBillLoading(true);
      setSelectedBillError(null);

      try {
        const detail = await getVendorBill(billId);
        setSelectedBill(detail);
        setSettlementForm(current => ({
          ...current,
          amount: detail.balanceAmount > 0 ? detail.balanceAmount.toFixed(2) : '',
        }));
      } catch (caughtError) {
        setSelectedBill(null);
        setSelectedBillError(resolveSafeMessage(caughtError, t('vendor.couldNotLoadSelectedBill'), t));
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setSelectedBillLoading(false);
      }
    },
    [auth, t]
  );

  useEffect(() => {
    if (!canSwitchBranch) {
      return;
    }

    void loadBranches();
  }, [canSwitchBranch, loadBranches]);

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadWorkspace(selectedBranchId);
  }, [canAccess, loadWorkspace, selectedBranchId]);

  useEffect(() => {
    if (!canViewReport) {
      return;
    }

    void loadPayablesReport(selectedBranchId);
  }, [canViewReport, loadPayablesReport, selectedBranchId]);

  useEffect(() => {
    if (!canAccess) {
      return;
    }

    void loadVendorStatement(
      selectedVendorId ?? '',
      selectedBranchId,
      statementFromDate,
      statementToDate
    );
  }, [canAccess, loadVendorStatement, selectedBranchId, selectedVendorId, statementFromDate, statementToDate]);

  useEffect(() => {
    if (!selectedBillId) {
      setSelectedBill(null);
      setSelectedBillError(null);
      return;
    }

    void loadSelectedBill(selectedBillId);
  }, [loadSelectedBill, selectedBillId]);

  useEffect(() => {
    if (!selectedBill || billDetailsScrollToken === 0) {
      return;
    }

    const target = billDetailsSectionRef.current;
    if (!target) {
      return;
    }

    if (typeof target.scrollIntoView === 'function') {
      target.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }

    if (typeof target.focus === 'function') {
      target.focus();
    }
  }, [billDetailsScrollToken, selectedBill]);

  useEffect(() => {
    if (vendorMode === 'edit' && selectedVendor) {
      setVendorForm({
        name: selectedVendor.name,
        vendorType: selectedVendor.vendorType,
        contactName: selectedVendor.contactName ?? '',
        mobileNumber: selectedVendor.mobileNumber ?? '',
        address: selectedVendor.address ?? '',
        notes: selectedVendor.notes ?? '',
        isActive: selectedVendor.isActive,
      });
    }
  }, [selectedVendor, vendorMode]);

  useEffect(() => {
    if (vendorMode === 'create') {
      setVendorForm(emptyVendorForm());
    }
  }, [vendorMode]);

  const openCreateVendorForm = useCallback(() => {
    setVendorMode('create');
    setSelectedVendorId(null);
    setVendorErrors({});
    setNotice(null);
  }, []);

  const openEditVendorForm = useCallback((vendor: VendorDetail) => {
    setVendorMode('edit');
    setSelectedVendorId(vendor.vendorId);
    setVendorForm({
      name: vendor.name,
      vendorType: vendor.vendorType,
      contactName: vendor.contactName ?? '',
      mobileNumber: vendor.mobileNumber ?? '',
      address: vendor.address ?? '',
      notes: vendor.notes ?? '',
      isActive: vendor.isActive,
    });
    setVendorErrors({});
    setNotice(null);
  }, []);

  const addBillLine = useCallback(() => {
    setBillForm(current => ({
      ...current,
      lines: [...current.lines, emptyVendorBillLine()],
    }));
  }, []);

  const removeBillLine = useCallback((lineId: string) => {
    setBillForm(current => {
      const nextLines = current.lines.filter(line => line.lineId !== lineId);
      return {
        ...current,
        lines: nextLines.length > 0 ? nextLines : [emptyVendorBillLine()],
      };
    });
  }, []);

  const updateBillLine = useCallback((lineId: string, updater: (line: VendorBillLineFormState) => VendorBillLineFormState) => {
    setBillForm(current => ({
      ...current,
      lines: current.lines.map(line => (line.lineId === lineId ? updater(line) : line)),
    }));
  }, []);

  const handleVendorSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();

      if (!selectedBranchId) {
        setNotice({
          tone: 'warning',
          message: t('vendor.noBranchBeforeSaveVendor'),
        });
        return;
      }

      const nextErrors = buildVendorErrors(vendorForm, t);
      setVendorErrors(nextErrors);

      if (Object.keys(nextErrors).length > 0) {
        setNotice({
          tone: 'warning',
          message: t('vendor.fixVendorFormNotice'),
        });
        return;
      }

      setVendorSubmitting(true);
      setNotice(null);

      try {
        const request = toVendorRequest(vendorForm, selectedBranchId);
        if (vendorMode === 'edit' && !selectedVendor?.vendorId) {
          throw new Error('Select a vendor to edit.');
        }

        const saved = vendorMode === 'create'
          ? await createVendor(request)
          : await updateVendor(selectedVendor!.vendorId, request);

        setVendorMode('edit');
        setSelectedVendorId(saved.vendorId);
        setVendorForm({
          name: saved.name,
          vendorType: saved.vendorType,
          contactName: saved.contactName ?? '',
          mobileNumber: saved.mobileNumber ?? '',
          address: saved.address ?? '',
          notes: saved.notes ?? '',
          isActive: saved.isActive,
        });
        await loadWorkspace(selectedBranchId);
        setNotice({
          tone: 'success',
          message: vendorMode === 'create' ? t('vendor.vendorSavedCreated', { name: saved.name }) : t('vendor.vendorSavedUpdated', { name: saved.name }),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: resolveSafeMessage(caughtError, t('vendor.couldNotSaveVendor'), t),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setVendorSubmitting(false);
      }
    },
    [auth, loadWorkspace, selectedBranchId, selectedVendor?.vendorId, t, vendorForm, vendorMode]
  );

  const handleBillSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();

      if (!selectedBranchId) {
        setNotice({
          tone: 'warning',
          message: t('vendor.noBranchBeforeCreateBill'),
        });
        return;
      }

      const nextErrors = buildBillErrors(billForm, t);
      setBillErrors(nextErrors);

      if (Object.keys(nextErrors).length > 0) {
        setNotice({
          tone: 'warning',
          message: t('vendor.fixBillFormNotice'),
        });
        return;
      }

      setBillSubmitting(true);
      setNotice(null);

      try {
        const request = toBillRequest(billForm, selectedBranchId);
        const saved = await createVendorBill(request);
        await loadWorkspace(selectedBranchId);
        setSelectedBillId(saved.vendorBillId);
        setSelectedBill(saved);
        setSettlementForm(emptySettlementForm());
        setBillForm(emptyVendorBillForm(billForm.vendorId, selectedBranchId));
        setNotice({
          tone: 'success',
          message: t('vendor.billCreatedNotice', { id: formatBillNumber(saved.billNumber) }),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: resolveSafeMessage(caughtError, t('vendor.couldNotSaveBill'), t),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setBillSubmitting(false);
      }
    },
    [auth, billForm, loadWorkspace, selectedBranchId, t]
  );

  const handleSettlementSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();

      if (!selectedBill) {
        return;
      }

      const nextErrors = buildSettlementErrors(settlementForm, selectedBill.balanceAmount, t);
      setSettlementErrors(nextErrors);

      if (Object.keys(nextErrors).length > 0) {
        setNotice({
          tone: 'warning',
          message: t('vendorSettlement.fixFormNotice'),
        });
        return;
      }

      setSettlementSubmitting(true);
      setNotice(null);

      try {
        const updated = await recordVendorSettlement(selectedBill.vendorBillId, toSettlementRequest(settlementForm));
        setSelectedBill(updated);
        setSettlementForm(emptySettlementForm());
        setSettlementDialogOpen(false);
        await Promise.all([
          loadWorkspace(selectedBranchId),
          loadVendorStatement(selectedBill.vendorId, selectedBranchId, statementFromDate, statementToDate),
        ]);
        setNotice({
          tone: 'success',
          message: t('vendorSettlement.settlementRecorded'),
        });
      } catch (caughtError) {
        setNotice({
          tone: 'danger',
          message: resolveSafeMessage(caughtError, t('vendorSettlement.couldNotRecord'), t),
        });
        if (isApiError(caughtError) && caughtError.status === 401) {
          void auth.logout();
        }
      } finally {
        setSettlementSubmitting(false);
      }
    },
    [auth, loadVendorStatement, loadWorkspace, selectedBill, selectedBranchId, settlementForm, statementFromDate, statementToDate, t]
  );

  const openSettlementDialog = useCallback((billId: string) => {
    setSelectedBillId(billId);
    setSettlementDialogOpen(true);
    setSettlementErrors({});
    setSettlementForm(emptySettlementForm());
    setNotice(null);
  }, []);

  const handleViewBill = useCallback((billId: string) => {
    setSelectedBillId(billId);
    setSelectedBillError(null);
    setBillDetailsScrollToken(token => token + 1);
  }, []);

  const closeSettlementDialog = useCallback(() => {
    setSettlementDialogOpen(false);
    setSettlementErrors({});
    setSettlementForm(emptySettlementForm());
  }, []);

  const renderVendorList = () => {
    if (vendorsError) {
      return (
        <EmptyState
          title={t('vendor.couldNotLoadVendorsTitle')}
          description={vendorsError}
          tone="admin"
          actionLabel={t('vendor.tryAgain')}
          onAction={() => void loadWorkspace(selectedBranchId)}
        />
      );
    }

    return (
      <ResponsiveDataList
        rows={vendors.map(vendor => ({
          id: vendor.vendorId,
          name: vendor.name,
          type: vendor.vendorType,
          contact: vendor.contactName ?? vendor.mobileNumber ?? '-',
          status: vendor.isActive ? 'Active' : 'Inactive',
          action: t('vendor.vendorListEditButton'),
        }))}
        columns={[
          { key: 'name', label: t('vendor.vendorListColumnVendor') },
          { key: 'type', label: t('vendor.vendorListColumnType') },
          { key: 'contact', label: t('vendor.vendorListColumnContact') },
          {
            key: 'status',
            label: t('vendor.vendorListColumnStatus'),
            render: row => <StatusBadge status={row.status} label={row.status} />,
          },
          {
            key: 'action',
            label: t('vendor.vendorListColumnAction'),
            render: row => (
              <Button
                size="sm"
                variant="secondary"
                onClick={() => {
                  const vendor = vendors.find(candidate => candidate.vendorId === row.id);
                  if (vendor) {
                    openEditVendorForm(vendor);
                  }
                }}
              >
                {t('vendor.vendorListEditButton')}
              </Button>
            ),
          },
        ]}
        mobileTitle={row => row.name}
        mobileDescription={row => `${row.type} · ${row.contact}`}
        emptyTitle={t('vendor.vendorListEmptyTitle')}
      emptyDescription={t('vendor.vendorListEmptyDescription')}
      />
    );
  };

  const selectedBillTitle = selectedBill?.billNumber?.trim()
    ? t('vendor.billDetailTitle', { id: selectedBill.billNumber.trim() })
    : t('vendor.billDetailTitleGeneric');

  const inventoryOptions = inventoryItems.map(item => (
    <option key={item.inventoryItemId} value={item.inventoryItemId}>
      {item.name} ({item.category})
    </option>
  ));

  if (!canAccess) {
    return (
      <AdminLayout
        title={t('vendor.workspaceTitle')}
        description={t('vendor.workspaceDescriptionNoAccess')}
        breadcrumbs={['Dashboard', t('nav.vendors')]}
        operatorLabel={operatorLabel}
        restaurantName={restaurantName}
        branchName={branchName}
        navItems={navItems}
      >
        <EmptyState
          title={t('vendor.notAuthorizedTitle')}
          description={t('vendor.notAuthorizedDescription')}
          tone="admin"
        />
      </AdminLayout>
    );
  }

  return (
    <AdminLayout
      title={t('vendor.workspaceTitle')}
      description={t('vendor.workspaceDescription')}
      breadcrumbs={['Dashboard', t('nav.vendors')]}
      operatorLabel={operatorLabel}
      restaurantName={restaurantName}
      branchName={selectedBranch?.name ?? branchName}
      navItems={navItems}
      actions={
        <Button variant="secondary" onClick={() => void loadWorkspace(selectedBranchId)} disabled={!selectedBranchId}>
          {t('vendor.refreshWorkspace')}
        </Button>
      }
    >
      <div className="preview-sequence vendor-workspace-page">
        <div className="summary-grid">
          <SummaryCard
            label={t('vendor.summaryVendors')}
            value={vendorsLoading && vendors.length === 0 ? t('vendor.branchLoading') : vendors.length.toString()}
            tone="admin"
            detail={t('vendor.summaryVendorsDetail')}
          />
          <SummaryCard
            label={t('vendor.summaryActiveVendors')}
            value={activeVendorCount.toString()}
            tone="accent"
            detail={t('vendor.summaryActiveVendorsDetail')}
          />
          <SummaryCard
            label={t('vendor.summaryOpenBills')}
            value={billCount.toString()}
            tone="orders"
            detail={`${paidBillCount} paid and ${partialBillCount} partially paid.`}
          />
          <SummaryCard
            label={t('vendor.summaryOutstandingPayable')}
            value={formatMoney(openBalance)}
            tone="inventory"
            detail={t('vendor.summaryOutstandingPayableDetail')}
          />
        </div>

        {canViewReport ? (
          <Card
            title={t('vendor.payableReportTitle')}
            description={t('vendor.payableReportDescription')}
            tone="admin"
            actions={
              <Button variant="secondary" onClick={() => navigate('/reports/vendor-payables')}>
                {t('vendor.viewPayableReport')}
              </Button>
            }
          >
            {payablesReportError ? (
              <div className="admin-notice admin-notice--warning" role="status">
                {payablesReportError}
              </div>
            ) : null}
            <div className="summary-grid">
              <SummaryCard
                label={t('vendor.payableReportOutstandingLabel')}
                value={formatVendorPayablesCurrency(payablesReport?.summary.totalOutstandingAmount ?? 0, payablesReport?.currencyCode ?? 'INR')}
                tone="inventory"
                detail={payablesReportLoading ? t('vendor.payableReportRefreshing') : t('vendor.payableReportOutstandingDetail')}
              />
              <SummaryCard
                label={t('vendor.payableReportOverdueBills')}
                value={(payablesReport?.summary.overdueBillCount ?? 0).toString()}
                tone="admin"
                detail={payablesReportLoading ? t('vendor.payableReportRefreshing') : `${reportMonthRange.fromDate} to ${reportMonthRange.toDate}`}
              />
            </div>
          </Card>
        ) : null}

        <Card
          title={t('vendor.statementCardTitle')}
          description={t('vendor.statementCardDescription')}
          tone="admin"
          actions={
            <Button variant="secondary" onClick={() => navigate('/vendors/statement')}>
              {t('vendor.openStatement')}
            </Button>
          }
        >
          {vendorStatementError ? (
            <div className="admin-notice admin-notice--warning" role="status">
              {vendorStatementError}
            </div>
          ) : null}
          {!vendorStatement ? (
            <EmptyState
              title={vendorStatementLoading ? t('vendor.statementLoadingTitle') : t('vendor.statementNoDataTitle')}
              description={
                vendorStatementLoading
                  ? t('vendor.statementLoadingDescription')
                  : selectedVendor
                    ? t('vendor.statementNoDataDescriptionSelected')
                    : t('vendor.statementNoDataDescriptionNone')
              }
              tone="admin"
            />
          ) : (
            <div className="summary-grid">
              <SummaryCard
                label={t('vendor.statementOpeningOutstanding')}
                value={formatMoney(vendorStatement.openingOutstandingAmount)}
                tone="inventory"
                detail={`${vendorStatement.fromDate} to ${vendorStatement.toDate}`}
              />
              <SummaryCard
                label={t('vendor.statementCurrentOutstanding')}
                value={formatMoney(vendorStatement.currentOutstandingAmount)}
                tone="admin"
                detail={vendorStatement.branchName ?? t('vendorStatement.branchAllBranches')}
              />
              <SummaryCard
                label={t('vendor.statementPayableBills')}
                value={vendorStatement.summary.payableBillCount.toString()}
                tone="orders"
                detail={t('vendor.statementPayableBillsDetail')}
              />
              <SummaryCard
                label={t('vendor.statementSettlements')}
                value={vendorStatement.summary.settlementCount.toString()}
                tone="dashboard"
                detail={`Overdue ${vendorStatement.summary.overdueBillCount.toString()}`}
              />
            </div>
          )}
        </Card>

        <VendorBillOcrDraftPanel
          branchId={selectedBranchId}
          vendors={vendors}
          inventoryItems={inventoryItems}
          canAccess={canAccess && (auth.hasPermission('VendorBill.Upload') || auth.hasPermission('VendorBill.ReviewOcr') || auth.hasPermission('VendorBill.OverrideOcr'))}
          onVendorBillCreated={vendorBillId => {
            setSelectedBillId(vendorBillId);
            void loadWorkspace(selectedBranchId);
          }}
        />

        {notice ? (
          <div className={`admin-notice admin-notice--${notice.tone}`} role={notice.tone === 'danger' ? 'alert' : 'status'}>
            {notice.message}
          </div>
        ) : null}

        {canSwitchBranch ? (
          <Card
            title={t('vendor.branchSelectionTitle')}
            description={t('vendor.branchSelectionDescription')}
            tone="admin"
            actions={<Badge tone={branchesLoading ? 'warning' : 'neutral'} label={branchesLoading ? t('vendor.branchLoading') : t('vendor.branchReady')} />}
          >
            <Select
              label={t('vendor.branchLabel')}
              value={selectedBranchId}
              onChange={event => {
                setSelectedBranchId(event.target.value);
                setVendorMode('create');
                setSelectedVendorId(null);
                setVendorForm(emptyVendorForm());
                setBillForm(emptyVendorBillForm());
                setSelectedBillId(null);
                setSelectedBill(null);
                setSettlementForm(emptySettlementForm());
                setNotice(null);
              }}
              error={branchesError ?? undefined}
              helperText={t('vendor.branchHelperText')}
            >
              <option value="">{t('vendor.branchSelectPlaceholder')}</option>
              {branches.map(branch => (
                <option key={branch.branchId} value={branch.branchId}>
                  {branch.name}
                </option>
              ))}
            </Select>
          </Card>
        ) : null}

        {branchesError ? (
          <div className="admin-notice admin-notice--danger" role="alert">
            {branchesError}
          </div>
        ) : null}

        <div className="preview-split preview-split--admin">
          <div className="admin-workspace-stack">
            <Card
              title={t('vendor.vendorsCardTitle')}
              description={t('vendor.vendorsCardDescription')}
              tone="admin"
              actions={
                <>
                  <Badge tone="neutral" label={vendorsLoading ? t('vendor.vendorsRefreshing') : t('vendor.vendorsLoaded', { count: vendors.length })} />
                  <Button size="sm" variant="secondary" onClick={openCreateVendorForm}>
                    {t('vendor.newVendorButton')}
                  </Button>
                </>
              }
            >
              {renderVendorList()}
            </Card>

            <Card
              title={vendorMode === 'create' ? t('vendor.addVendorTitle') : t('vendor.editVendorTitle', { name: selectedVendor?.name ?? '' })}
              description={t('vendor.vendorFormDescription')}
              tone="admin"
            >
              <form className="admin-form" onSubmit={handleVendorSubmit} noValidate>
                <div className="admin-form-grid">
                  <Input
                    label={t('vendor.vendorNameLabel')}
                    value={vendorForm.name}
                    onChange={event => setVendorForm(current => ({ ...current, name: event.target.value }))}
                    error={vendorErrors.name}
                    placeholder="Fresh Rice"
                  />
                  <Select
                    label={t('vendor.vendorTypeLabel')}
                    value={vendorForm.vendorType}
                    onChange={event => setVendorForm(current => ({ ...current, vendorType: event.target.value }))}
                    error={vendorErrors.vendorType}
                  >
                    {vendorTypeOptions.map(option => (
                      <option key={option} value={option}>
                        {option}
                      </option>
                    ))}
                  </Select>
                  <Input
                    label={t('vendor.contactNameLabel')}
                    value={vendorForm.contactName}
                    onChange={event => setVendorForm(current => ({ ...current, contactName: event.target.value }))}
                    placeholder="Kumar"
                  />
                  <Input
                    label={t('vendor.mobileNumberLabel')}
                    value={vendorForm.mobileNumber}
                    onChange={event => setVendorForm(current => ({ ...current, mobileNumber: event.target.value }))}
                    error={vendorErrors.mobileNumber}
                    placeholder="90010001"
                  />
                  <TextAreaField
                    label={t('vendor.addressLabel')}
                    value={vendorForm.address}
                    onChange={value => setVendorForm(current => ({ ...current, address: value }))}
                    placeholder="Market Road"
                  />
                  <TextAreaField
                    label={t('vendor.notesLabel')}
                    value={vendorForm.notes}
                    onChange={value => setVendorForm(current => ({ ...current, notes: value }))}
                    placeholder="Daily vendor"
                  />
                </div>
                <Checkbox
                  label={t('vendor.activeVendorLabel')}
                  checked={vendorForm.isActive}
                  onChange={event => setVendorForm(current => ({ ...current, isActive: event.target.checked }))}
                  helperText={t('vendor.activeVendorHelperText')}
                />
                <div className="admin-form-actions">
                  <Button type="submit" disabled={vendorSubmitting}>
                    {vendorMode === 'create' ? t('vendor.createVendorButton') : t('vendor.saveVendorButton')}
                  </Button>
                  {vendorMode === 'edit' ? (
                    <Button type="button" variant="secondary" onClick={openCreateVendorForm}>
                      {t('vendor.newVendorButton')}
                    </Button>
                  ) : null}
                </div>
              </form>
            </Card>
          </div>

          <div className="admin-workspace-stack">
            <Card
              title={t('vendor.outstandingPayablesTitle')}
              description={t('vendor.outstandingPayablesDescription')}
              tone="admin"
              actions={<Badge tone="neutral" label={billsLoading ? t('vendor.vendorsRefreshing') : t('vendor.outstandingOpen', { count: outstandingBills.length })} />}
            >
              {billsError ? (
                <EmptyState
                  title={t('vendor.couldNotLoadPayablesTitle')}
                  description={billsError}
                  tone="admin"
                  actionLabel={t('vendor.tryAgain')}
                  onAction={() => void loadWorkspace(selectedBranchId)}
                />
              ) : (
                <ResponsiveDataList
                  rows={outstandingBills.map(bill => ({
                    id: bill.vendorBillId,
                    billNumber: formatBillNumber(bill.billNumber),
                    vendorName: bill.vendorName,
                    billDate: formatDate(bill.billDate),
                    dueDate: formatDate(bill.dueDate),
                    total: formatMoney(bill.totalAmount),
                    paid: formatMoney(bill.paidAmount),
                    balance: formatMoney(bill.balanceAmount),
                    status: bill.status,
                    action: t('vendor.billSettleButton'),
                  }))}
                  columns={[
                    { key: 'billNumber', label: t('vendor.billColumnBillNumber') },
                    { key: 'vendorName', label: t('vendor.billColumnVendor') },
                    { key: 'billDate', label: t('vendor.billColumnBillDate') },
                    { key: 'dueDate', label: t('vendor.billColumnDueDate'), hideOnMobile: true },
                    { key: 'total', label: t('vendor.billColumnTotal'), align: 'right' },
                    { key: 'paid', label: t('vendor.billColumnPaid'), align: 'right' },
                    { key: 'balance', label: t('vendor.billColumnOutstanding'), align: 'right' },
                    {
                      key: 'status',
                      label: t('vendor.billColumnStatus'),
                      render: row => <StatusBadge status={row.status} label={row.status} />,
                    },
                    {
                      key: 'action',
                      label: t('vendor.billColumnAction'),
                      render: row => (
                        <div className="vendor-payable-actions">
                          <Button size="sm" variant="secondary" onClick={() => handleViewBill(row.id)}>
                            {t('vendor.billViewButton')}
                          </Button>
                          <Button
                            size="sm"
                            onClick={() => openSettlementDialog(row.id)}
                            disabled={!canSettleVendorBills}
                          >
                            {t('vendor.billSettleButton')}
                          </Button>
                        </div>
                      ),
                    },
                  ]}
                  mobileTitle={row => row.billNumber}
                  mobileDescription={row => `${row.vendorName} · ${row.balance} ${t('vendor.billColumnOutstanding').toLowerCase()}`}
                  emptyTitle={t('vendor.billEmptyTitle')}
                  emptyDescription={t('vendor.billEmptyDescription')}
                />
              )}
            </Card>

            <Card
              title={t('vendor.createBillTitle')}
              description={t('vendor.createBillDescription')}
              tone="admin"
            >
              <form className="admin-form" onSubmit={handleBillSubmit} noValidate>
                <div className="admin-form-grid">
                  <Select
                    label={t('vendor.billVendorLabel')}
                    value={billForm.vendorId}
                    onChange={event => setBillForm(current => ({ ...current, vendorId: event.target.value }))}
                    error={billErrors.vendorId}
                  >
                    <option value="">{t('vendor.billVendorSelectPlaceholder')}</option>
                    {vendors.map(vendor => (
                      <option key={vendor.vendorId} value={vendor.vendorId}>
                        {vendor.name} ({vendor.vendorType})
                      </option>
                    ))}
                  </Select>
                  <Input
                    label={t('vendor.billDateLabel')}
                    type="date"
                    value={billForm.billDate}
                    onChange={event => setBillForm(current => ({ ...current, billDate: event.target.value }))}
                    error={billErrors.billDate}
                  />
                  <Input
                    label={t('vendor.billNumberLabel')}
                    value={billForm.billNumber}
                    onChange={event => setBillForm(current => ({ ...current, billNumber: event.target.value }))}
                    placeholder="VB-001"
                  />
                  <Input
                    label={t('vendor.billDueDateLabel')}
                    type="date"
                    value={billForm.dueDate}
                    onChange={event => setBillForm(current => ({ ...current, dueDate: event.target.value }))}
                  />
                  <TextAreaField
                    label={t('vendor.notesLabel')}
                    value={billForm.notes}
                    onChange={value => setBillForm(current => ({ ...current, notes: value }))}
                    placeholder="Morning purchase"
                  />
                </div>

                <div className="admin-form-section">
                  <div className="admin-form-section__title-row">
                    <div className="admin-form-section__title">{t('vendor.billLinesTitle')}</div>
                    <Button size="sm" variant="secondary" onClick={addBillLine} type="button">
                      {t('vendor.billLinesAddButton')}
                    </Button>
                  </div>
                  <div className="admin-form-section__description">
                    {t('vendor.billLinesDescription')}
                  </div>
                  {billErrors.lines ? <div className="admin-form-error">{billErrors.lines}</div> : null}

                  <div className="vendor-bill-lines">
                    {billForm.lines.map((line, index) => (
                      <div key={line.lineId} className="vendor-bill-line">
                        <div className="vendor-bill-line__header">
                          <strong>{t('vendor.billLineHeader', { n: index + 1 })}</strong>
                          {billForm.lines.length > 1 ? (
                            <Button size="sm" variant="ghost" type="button" onClick={() => removeBillLine(line.lineId)}>
                              {t('vendor.billLineRemoveButton')}
                            </Button>
                          ) : null}
                        </div>
                        <div className="admin-form-grid">
                          <Select
                            label={t('vendor.billLineInventoryItemLabel')}
                            value={line.inventoryItemId}
                            onChange={event =>
                              updateBillLine(line.lineId, current => ({
                                ...current,
                                inventoryItemId: event.target.value,
                                description:
                                  current.description.trim() || inventoryItems.find(item => item.inventoryItemId === event.target.value)?.name || '',
                              }))
                            }
                            helperText={inventoryLoading ? t('vendor.billLineInventoryItemLoading') : t('vendor.billLineInventoryItemHelper')}
                            error={inventoryError ?? undefined}
                          >
                            <option value="">{t('vendor.billLineInventoryItemNoItem')}</option>
                            {inventoryOptions}
                          </Select>
                          <Input
                            label={t('vendor.billLineDescriptionLabel')}
                            value={line.description}
                            onChange={event =>
                              updateBillLine(line.lineId, current => ({ ...current, description: event.target.value }))
                            }
                            placeholder="Rice"
                          />
                          <Input
                            label={t('vendor.billLineQuantityLabel')}
                            type="number"
                            min="0.01"
                            step="0.01"
                            value={line.quantity}
                            onChange={event =>
                              updateBillLine(line.lineId, current => ({ ...current, quantity: event.target.value }))
                            }
                            placeholder="10"
                          />
                          <Input
                            label={t('vendor.billLineUnitCostLabel')}
                            type="number"
                            min="0"
                            step="0.01"
                            value={line.unitCost}
                            onChange={event =>
                              updateBillLine(line.lineId, current => ({ ...current, unitCost: event.target.value }))
                            }
                            placeholder="10"
                          />
                        </div>
                        <div className="vendor-bill-line__totals">
                          <Badge
                            tone={line.inventoryItemId ? 'success' : 'neutral'}
                            label={line.inventoryItemId ? t('vendor.billLineStockInLinked') : t('vendor.billLineNoStockIn')}
                          />
                        </div>
                      </div>
                    ))}
                  </div>
                </div>

                <div className="admin-form-actions">
                  <Button type="submit" disabled={billSubmitting}>
                    {t('vendor.createBillButton')}
                  </Button>
                </div>
              </form>
            </Card>

            <div ref={billDetailsSectionRef} className="scroll-target" tabIndex={-1} aria-label={selectedBillTitle}>
              <Card
                title={selectedBillTitle}
                description={t('vendor.billDetailDescription')}
                tone="admin"
                actions={
                  selectedBill && canSettleVendorBills ? (
                    <Button size="sm" onClick={() => openSettlementDialog(selectedBill.vendorBillId)}>
                      {t('vendor.recordSettlementButton')}
                    </Button>
                  ) : null
                }
              >
                {selectedBillLoading ? (
                  <EmptyState title={t('vendor.billDetailLoadingTitle')} description={t('vendor.billDetailLoadingDescription')} tone="admin" />
                ) : selectedBillError ? (
                  <EmptyState
                    title={t('vendor.couldNotLoadBillTitle')}
                    description={selectedBillError}
                    tone="admin"
                    actionLabel={t('vendor.tryAgain')}
                    onAction={() => selectedBillId && void loadSelectedBill(selectedBillId)}
                  />
                ) : selectedBill ? (
                  <div className="admin-workspace-stack">
                    <div className="vendor-bill-detail-summary">
                      <div className="vendor-bill-detail-summary__row">
                        <span className="vendor-bill-detail-summary__label">{t('vendor.billDetailVendorLabel')}</span>
                        <strong>{selectedBill.vendorName}</strong>
                      </div>
                      <div className="vendor-bill-detail-summary__row">
                        <span className="vendor-bill-detail-summary__label">{t('vendor.billDetailStatusLabel')}</span>
                        <StatusBadge status={selectedBill.status} label={selectedBill.status} />
                      </div>
                      <div className="vendor-bill-detail-summary__row">
                        <span className="vendor-bill-detail-summary__label">{t('vendor.billDetailTotalLabel')}</span>
                        <strong>{formatMoney(selectedBill.totalAmount)}</strong>
                      </div>
                      <div className="vendor-bill-detail-summary__row">
                        <span className="vendor-bill-detail-summary__label">{t('vendor.billDetailPaidLabel')}</span>
                        <strong>{formatMoney(selectedBill.paidAmount)}</strong>
                      </div>
                      <div className="vendor-bill-detail-summary__row">
                        <span className="vendor-bill-detail-summary__label">{t('vendor.billDetailBalanceLabel')}</span>
                        <strong>{formatMoney(selectedBill.balanceAmount)}</strong>
                      </div>
                    </div>

                    <Card
                      title={t('vendor.billLinesCardTitle')}
                      description={t('vendor.billLinesCardDescription')}
                      tone="admin"
                    >
                      <ResponsiveDataList
                        rows={selectedBill.lines.map(line => ({
                          id: line.vendorBillLineId,
                          description: line.description,
                          quantity: line.quantity.toString(),
                          unitCost: formatMoney(line.unitCost),
                          lineTotal: formatMoney(line.lineTotal),
                          inventoryItem: line.inventoryItemName ?? '-',
                          stockIn: line.inventoryMovementId ? t('vendor.billLineStockInLinkedBadge') : t('vendor.billLineStockInNotLinked'),
                        }))}
                        columns={[
                          { key: 'description', label: t('vendor.billLineColDescription') },
                          { key: 'inventoryItem', label: t('vendor.billLineColInventoryItem') },
                          { key: 'quantity', label: t('vendor.billLineColQuantity'), align: 'right' },
                          { key: 'unitCost', label: t('vendor.billLineColUnitCost'), align: 'right' },
                          { key: 'lineTotal', label: t('vendor.billLineColLineTotal'), align: 'right' },
                          {
                            key: 'stockIn',
                            label: t('vendor.billLineColStockIn'),
                            render: row => (
                              <Badge tone={row.stockIn === t('vendor.billLineStockInLinkedBadge') ? 'success' : 'neutral'} label={row.stockIn} />
                            ),
                          },
                        ]}
                        mobileTitle={row => row.description}
                        mobileDescription={row => row.inventoryItem}
                        emptyTitle={t('vendor.billLinesEmptyTitle')}
                        emptyDescription={t('vendor.billLinesEmptyDescription')}
                      />
                    </Card>

                    <Card
                      title={t('vendor.settlementHistoryTitle')}
                      description={t('vendor.settlementHistoryDescription')}
                      tone="admin"
                    >
                      <ResponsiveDataList
                        rows={selectedBill.settlements.map(settlement => ({
                          id: settlement.vendorSettlementId,
                          paymentMode: settlement.paymentMode,
                          amount: formatMoney(settlement.amount),
                          paidAt: formatDate(settlement.paidAtUtc),
                          referenceNumber: settlement.referenceNumber ?? '-',
                          status: settlement.status,
                        }))}
                        columns={[
                          { key: 'paymentMode', label: t('vendor.settlementColMode') },
                          { key: 'amount', label: t('vendor.settlementColAmount'), align: 'right' },
                          { key: 'paidAt', label: t('vendor.settlementColPaidAt') },
                          { key: 'referenceNumber', label: t('vendor.settlementColReference') },
                          {
                            key: 'status',
                            label: t('vendor.settlementColStatus'),
                            render: row => <StatusBadge status={row.status} label={row.status} />,
                          },
                        ]}
                        mobileTitle={row => row.paymentMode}
                        mobileDescription={row => row.amount}
                        emptyTitle={t('vendor.settlementEmptyTitle')}
                        emptyDescription={t('vendor.settlementEmptyDescription')}
                      />
                    </Card>

                    <div className="admin-form-note">
                      {t('vendor.billDetailNote')}
                    </div>
                  </div>
                ) : (
                  <EmptyState
                    title={t('vendor.chooseBillTitle')}
                    description={t('vendor.chooseBillDescription')}
                    tone="admin"
                  />
                )}
              </Card>
            </div>
          </div>
        </div>
        </div>

        {settlementDialogOpen ? (
          selectedBill ? (
            <VendorSettlementDialog
              bill={selectedBill}
              form={settlementForm}
              errors={settlementErrors}
              previewOutstandingAmount={
                settlementForm.amount.trim() === '' || Number.isNaN(Number(settlementForm.amount))
                  ? null
                  : Math.max(0, selectedBill.balanceAmount - Number(settlementForm.amount))
              }
              submitting={settlementSubmitting}
              confirmDisabled={!canSettleVendorBills}
              currencyCode={payablesReport?.currencyCode ?? selectedBranch?.currency ?? restaurantCurrencyCode}
              locale={locale}
              onSubmit={handleSettlementSubmit}
              onClose={closeSettlementDialog}
              onPaymentModeChange={value => setSettlementForm(current => ({ ...current, paymentMode: value }))}
              onAmountChange={value => setSettlementForm(current => ({ ...current, amount: value }))}
              onReferenceNumberChange={value => setSettlementForm(current => ({ ...current, referenceNumber: value }))}
              onNotesChange={value => setSettlementForm(current => ({ ...current, notes: value }))}
              onPaidAtChange={value =>
                setSettlementForm(current => ({
                  ...current,
                  paidAtUtc: value ? `${value}:00.000Z` : new Date().toISOString(),
                }))
              }
            />
          ) : (
            <div className="vendor-settlement-dialog" role="dialog" aria-modal="true" aria-label="Record vendor settlement">
              <Card title={t('vendor.recordSettlementButton')} description={t('vendor.settlementDialogLoadingDescription')} tone="admin">
                <EmptyState
                  title={selectedBillLoading ? t('vendor.settlementDialogLoadingTitle') : t('vendor.chooseBillTitle')}
                  description={
                    selectedBillLoading
                      ? t('vendor.settlementDialogLoadingDescription')
                      : t('vendor.settlementDialogNoPayable')
                  }
                  tone="admin"
                  actionLabel={t('vendor.settlementDialogClose')}
                  onAction={closeSettlementDialog}
                />
              </Card>
            </div>
          )
        ) : null}
      </AdminLayout>
  );
};

export default VendorWorkspacePage;
