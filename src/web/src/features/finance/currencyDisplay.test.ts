import { describe, expect, it } from 'vitest';

import { formatCurrency, resolveCurrencyLocale } from './currencyDisplay';

describe('currencyDisplay', () => {
  it('formats INR with the pilot default locale', () => {
    expect(formatCurrency(1234.5, 'INR', 'en-IN')).toBe('₹1,234.50');
  });

  it('formats SGD and USD with their configured currency codes', () => {
    expect(formatCurrency(1234.5, 'SGD', 'en-SG')).toBe('$1,234.50');
    expect(formatCurrency(1234.5, 'USD', 'en-US')).toBe('$1,234.50');
  });

  it('falls back to the pilot defaults when currency context is missing', () => {
    expect(formatCurrency(12.5)).toBe('₹12.50');
  });

  it('resolves a locale from the restaurant country or currency', () => {
    expect(resolveCurrencyLocale('IN', 'INR')).toBe('en-IN');
    expect(resolveCurrencyLocale('SG', 'SGD')).toBe('en-SG');
    expect(resolveCurrencyLocale(undefined, undefined)).toBe('en-IN');
  });
});
