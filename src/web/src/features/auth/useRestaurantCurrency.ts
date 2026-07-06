import { useMemo } from 'react';

import { DEFAULT_COUNTRY_CODE, DEFAULT_CURRENCY_CODE, resolveCurrencyLocale } from '../finance/currencyDisplay';
import { useAuth } from './useAuth';

const normalizeCode = (value?: string | null) => value?.trim().toUpperCase() || undefined;

export const useRestaurantCurrency = () => {
  const auth = useAuth();

  return useMemo(() => {
    const countryCode = normalizeCode(auth.session?.countryCode) ?? DEFAULT_COUNTRY_CODE;
    const currencyCode = normalizeCode(auth.session?.currencyCode) ?? DEFAULT_CURRENCY_CODE;
    const timeZoneId = auth.session?.timeZoneId?.trim() || undefined;

    return {
      countryCode,
      currencyCode,
      locale: resolveCurrencyLocale(countryCode, currencyCode),
      timeZoneId,
    };
  }, [auth.session?.countryCode, auth.session?.currencyCode, auth.session?.timeZoneId]);
};
