import { formatCurrency } from '../finance/currencyDisplay';
import type { CashierShiftDetail, CashierShiftListItem, CashierShiftStatus } from './cashierShiftTypes';

export interface CashierShiftDisplayMessages {
  statusOpen: string;
  statusClosed: string;
  statusVoided: string;
  notAvailable: string;
}

const defaultMessages: CashierShiftDisplayMessages = {
  statusOpen: 'Open',
  statusClosed: 'Closed',
  statusVoided: 'Voided',
  notAvailable: 'Not available',
};

export const formatCashierCurrency = (value: number, currency?: string | null, locale?: string | null) =>
  formatCurrency(value, currency, locale);

export const formatCashierSignedCurrency = (value: number, currency?: string | null, locale?: string | null) => {
  try {
    return new Intl.NumberFormat(locale || undefined, {
      style: 'currency',
      currency: currency || 'INR',
      signDisplay: 'always',
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  } catch {
    const prefix = value >= 0 ? '+' : '';
    return `${prefix}${currency || 'INR'} ${value.toFixed(2)}`;
  }
};

export const formatCashierTimestamp = (value?: string | null, notAvailableText = defaultMessages.notAvailable) => {
  if (!value) {
    return notAvailableText;
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  });
};

export const formatCashierDate = (value?: string | null, notAvailableText = defaultMessages.notAvailable) => {
  if (!value) {
    return notAvailableText;
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleDateString(undefined, {
    dateStyle: 'medium',
  });
};

export const formatCashierDateInput = (date = new Date()) => {
  const year = date.getFullYear();
  const month = `${date.getMonth() + 1}`.padStart(2, '0');
  const day = `${date.getDate()}`.padStart(2, '0');
  return `${year}-${month}-${day}`;
};

export const formatCashierShiftStatus = (value: CashierShiftStatus, messages: CashierShiftDisplayMessages = defaultMessages) => {
  if (value === 'Open') {
    return messages.statusOpen;
  }

  if (value === 'Closed') {
    return messages.statusClosed;
  }

  return messages.statusVoided;
};

export const sortCashierShiftsNewestFirst = (items: CashierShiftListItem[]) =>
  [...items].sort((left, right) => {
    const businessDateDelta = Date.parse(`${right.businessDate}`) - Date.parse(`${left.businessDate}`);
    if (businessDateDelta !== 0) {
      return businessDateDelta;
    }

    const openedDelta = Date.parse(right.openedAtUtc) - Date.parse(left.openedAtUtc);
    if (openedDelta !== 0) {
      return openedDelta;
    }

    return right.cashierShiftId.localeCompare(left.cashierShiftId, undefined, { sensitivity: 'base' });
  });

export const buildCashierShiftSummaryLabel = (
  shift: CashierShiftListItem,
  currency?: string | null,
  locale?: string | null,
  messages: CashierShiftDisplayMessages = defaultMessages
) => `${formatCashierShiftStatus(shift.status, messages)} · ${formatCashierCurrency(shift.expectedClosingCashAmount, currency, locale)}`;

export const buildCashierShiftDetailHeader = (
  shift: CashierShiftDetail,
  currency?: string | null,
  locale?: string | null,
  messages: CashierShiftDisplayMessages = defaultMessages
) => `${shift.cashierShiftId} · ${formatCashierShiftStatus(shift.status, messages)} · ${formatCashierCurrency(shift.expectedClosingCashAmount, currency, locale)}`;
