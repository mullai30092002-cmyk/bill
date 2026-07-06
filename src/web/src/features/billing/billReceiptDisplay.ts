import { formatCurrency } from '../finance/currencyDisplay';
import type { BillReceiptResponse } from './billReceiptTypes';

export interface BillReceiptDisplayLabels {
  notAvailable: string;
  recordedBySystem: string;
  reprint: string;
  receipt: string;
  cash: string;
  card: string;
  upi: string;
}

const defaultLabels: BillReceiptDisplayLabels = {
  notAvailable: 'Not available',
  recordedBySystem: 'Recorded by system',
  reprint: 'Reprint',
  receipt: 'Receipt',
  cash: 'Cash',
  card: 'Card',
  upi: 'UPI',
};

export const formatReceiptCurrency = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export const formatReceiptDate = (value?: string | null, labels: Pick<BillReceiptDisplayLabels, 'notAvailable'> = defaultLabels) => {
  if (!value) {
    return labels.notAvailable;
  }

  const datePart = value.slice(0, 10);
  const [yearText, monthText, dayText] = datePart.split('-');
  const year = Number.parseInt(yearText ?? '', 10);
  const month = Number.parseInt(monthText ?? '', 10);
  const day = Number.parseInt(dayText ?? '', 10);

  if ([year, month, day].some(part => Number.isNaN(part))) {
    return value;
  }

  const parsed = new Date(year, month - 1, day);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleDateString(undefined, {
    dateStyle: 'medium',
  });
};

export const formatReceiptTimestamp = (
  value?: string | null,
  labels: Pick<BillReceiptDisplayLabels, 'notAvailable'> = defaultLabels
) => {
  if (!value) {
    return labels.notAvailable;
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

export const formatReceiptPaymentMode = (
  value: string,
  labels: Pick<BillReceiptDisplayLabels, 'cash' | 'card' | 'upi'> = defaultLabels
) => {
  switch (value) {
    case 'Cash':
      return labels.cash;
    case 'Card':
      return labels.card;
    case 'Upi':
      return labels.upi;
    default:
      return value;
  }
};

export const formatReceiptStatus = (value: string, partiallyPaidLabel = 'Partially paid') =>
  value === 'PartiallyPaid' ? partiallyPaidLabel : value;

export const buildReceiptActorLabel = (
  label?: string | null,
  userId?: string | null,
  recordedBySystemLabel = defaultLabels.recordedBySystem
) => {
  if (label && label.trim()) {
    return label.trim();
  }

  if (userId && userId.trim()) {
    return userId.trim();
  }

  return recordedBySystemLabel;
};

export const buildReceiptMarker = (
  receipt?: BillReceiptResponse | null,
  labels: Pick<BillReceiptDisplayLabels, 'reprint' | 'receipt'> = defaultLabels
) => (receipt?.isReprint ? labels.reprint : labels.receipt);
