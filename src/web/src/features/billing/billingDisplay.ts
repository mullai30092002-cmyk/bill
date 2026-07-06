import { formatCurrency } from '../finance/currencyDisplay';
import type { BillDetail, BillListItem, BillStatus, PaymentMode, PaymentStatus } from './billingTypes';

export interface BillingDisplayLabels {
  notAvailable: string;
  noBillSelected: string;
  partiallyPaid: string;
  unpaid: string;
  paid: string;
  cancelled: string;
  recorded: string;
  cash: string;
  card: string;
  upi: string;
  eatIn: string;
  parcel: string;
  draft: string;
  confirmed: string;
}

const defaultLabels: BillingDisplayLabels = {
  notAvailable: 'Not available',
  noBillSelected: 'No bill selected',
  partiallyPaid: 'Partially paid',
  unpaid: 'Unpaid',
  paid: 'Paid',
  cancelled: 'Cancelled',
  recorded: 'Recorded',
  cash: 'Cash',
  card: 'Card',
  upi: 'UPI',
  eatIn: 'Eat-in',
  parcel: 'Parcel',
  draft: 'Draft',
  confirmed: 'Confirmed',
};

const expandScientificNotation = (value: string) => {
  const [mantissa, exponentText] = value.toLowerCase().split('e');
  const exponent = Number.parseInt(exponentText, 10);
  const negative = mantissa.startsWith('-');
  const unsignedMantissa = negative ? mantissa.slice(1) : mantissa;
  const [wholePart, fractionPart = ''] = unsignedMantissa.split('.');
  const digits = `${wholePart}${fractionPart}`;
  const decimalIndex = wholePart.length + exponent;

  if (decimalIndex <= 0) {
    return `${negative ? '-' : ''}0.${'0'.repeat(Math.abs(decimalIndex))}${digits}`;
  }

  if (decimalIndex >= digits.length) {
    return `${negative ? '-' : ''}${digits}${'0'.repeat(decimalIndex - digits.length)}`;
  }

  return `${negative ? '-' : ''}${digits.slice(0, decimalIndex)}.${digits.slice(decimalIndex)}`;
};

const toDecimalString = (value: number) => {
  const raw = value.toString();
  return raw.includes('e') || raw.includes('E') ? expandScientificNotation(raw) : raw;
};

export const roundMoney = (value: number) => {
  if (!Number.isFinite(value)) {
    return value;
  }

  const negative = value < 0;
  const absolute = Math.abs(value);
  const [wholePart, fractionPart = ''] = toDecimalString(absolute).split('.');
  const paddedFraction = `${fractionPart}000`;

  if (paddedFraction.length <= 2) {
    return Number(`${negative ? '-' : ''}${wholePart}.${paddedFraction.slice(0, 2)}`);
  }

  const cents = Number(paddedFraction.slice(0, 2));
  const thirdDigit = Number(paddedFraction[2]);
  let roundedWhole = Number(wholePart);
  let roundedCents = thirdDigit >= 5 ? cents + 1 : cents;

  if (roundedCents === 100) {
    roundedWhole += 1;
    roundedCents = 0;
  }

  return Number(`${negative ? '-' : ''}${roundedWhole}.${String(roundedCents).padStart(2, '0')}`);
};

export const formatBillingCurrency = (value: number, currencyCode?: string | null, locale?: string | null) =>
  formatCurrency(value, currencyCode, locale);

export const formatBillingTimestamp = (value?: string | null, labels: Pick<BillingDisplayLabels, 'notAvailable'> = defaultLabels) => {
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

export const formatBillingDate = (value?: string | null, labels: Pick<BillingDisplayLabels, 'notAvailable'> = defaultLabels) => {
  if (!value) {
    return labels.notAvailable;
  }

  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) {
    return value;
  }

  return parsed.toLocaleDateString(undefined, {
    dateStyle: 'medium',
  });
};

export const formatBillingBillStatus = (
  value: BillStatus,
  labels: Pick<BillingDisplayLabels, 'partiallyPaid' | 'unpaid' | 'paid' | 'cancelled'> = defaultLabels
) => {
  switch (value) {
    case 'Unpaid':
      return labels.unpaid;
    case 'PartiallyPaid':
      return labels.partiallyPaid;
    case 'Paid':
      return labels.paid;
    case 'Cancelled':
      return labels.cancelled;
    default:
      return value;
  }
};

export const formatBillingPaymentStatus = (
  value: PaymentStatus | string,
  labels: Pick<BillingDisplayLabels, 'recorded' | 'cancelled'> = defaultLabels
) => {
  switch (value) {
    case 'Recorded':
      return labels.recorded;
    case 'Cancelled':
      return labels.cancelled;
    default:
      return value;
  }
};

export const formatBillingPaymentMode = (
  value: PaymentMode,
  labels: Pick<BillingDisplayLabels, 'cash' | 'card' | 'upi'> = defaultLabels
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

export const formatBillingOrderType = (
  value: 'EatIn' | 'Parcel',
  labels: Pick<BillingDisplayLabels, 'eatIn' | 'parcel'> = defaultLabels
) => (value === 'EatIn' ? labels.eatIn : labels.parcel);

export const formatBillingOrderStatus = (
  value: 'Draft' | 'Confirmed' | 'Cancelled',
  labels: Pick<BillingDisplayLabels, 'draft' | 'confirmed' | 'cancelled'> = defaultLabels
) => {
  switch (value) {
    case 'Draft':
      return labels.draft;
    case 'Confirmed':
      return labels.confirmed;
    case 'Cancelled':
      return labels.cancelled;
    default:
      return value;
  }
};

export const buildSelectedBillLabel = (
  bill?: BillDetail | BillListItem | null,
  labels: Pick<BillingDisplayLabels, 'noBillSelected' | 'partiallyPaid' | 'unpaid' | 'paid' | 'cancelled'> = defaultLabels
) => {
  if (!bill) {
    return labels.noBillSelected;
  }

  return `${bill.billNumber} · ${formatBillingBillStatus(bill.status, labels)}`;
};
