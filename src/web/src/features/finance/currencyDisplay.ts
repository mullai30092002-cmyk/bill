export const DEFAULT_COUNTRY_CODE = 'IN';
export const DEFAULT_CURRENCY_CODE = 'INR';
export const DEFAULT_CURRENCY_LOCALE = 'en-IN';

const currencyLocaleByCountry: Record<string, string> = {
  IN: 'en-IN',
  SG: 'en-SG',
  US: 'en-US',
};

const currencyLocaleByCurrency: Record<string, string> = {
  INR: 'en-IN',
  SGD: 'en-SG',
  USD: 'en-US',
};

const normalizeCode = (value?: string | null) => {
  const trimmed = value?.trim().toUpperCase() ?? '';
  return trimmed || undefined;
};

export const resolveCurrencyLocale = (countryCode?: string | null, currencyCode?: string | null) => {
  const normalizedCountryCode = normalizeCode(countryCode);
  if (normalizedCountryCode && currencyLocaleByCountry[normalizedCountryCode]) {
    return currencyLocaleByCountry[normalizedCountryCode];
  }

  const normalizedCurrencyCode = normalizeCode(currencyCode);
  if (normalizedCurrencyCode && currencyLocaleByCurrency[normalizedCurrencyCode]) {
    return currencyLocaleByCurrency[normalizedCurrencyCode];
  }

  return DEFAULT_CURRENCY_LOCALE;
};

export const formatCurrency = (value: number, currencyCode?: string | null, locale?: string | null) => {
  const resolvedCurrencyCode = normalizeCode(currencyCode) ?? DEFAULT_CURRENCY_CODE;
  const resolvedLocale = locale?.trim() || resolveCurrencyLocale(undefined, resolvedCurrencyCode);

  try {
    return new Intl.NumberFormat(resolvedLocale, {
      style: 'currency',
      currency: resolvedCurrencyCode,
      minimumFractionDigits: 2,
      maximumFractionDigits: 2,
    }).format(value);
  } catch {
    return `${resolvedCurrencyCode} ${value.toFixed(2)}`;
  }
};
