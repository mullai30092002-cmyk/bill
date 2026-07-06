import { describe, expect, it } from 'vitest';

import { resolveLandingRoute } from './landingRoute';

describe('resolveLandingRoute', () => {
  it('routes RestaurantOwner and Admin report viewers to the owner dashboard', () => {
    expect(resolveLandingRoute(['RestaurantOwner'], ['Report.View'])).toBe('/owner/dashboard');
    expect(resolveLandingRoute(['Admin'], ['Report.View'])).toBe('/owner/dashboard');
  });

  it('routes accounts report viewers to the daily cash sales report', () => {
    expect(resolveLandingRoute(['AccountsUser'], ['Report.View'])).toBe('/reports/daily-cash-sales');
  });

  it('routes cashiers with money or order permissions to POS orders', () => {
    expect(resolveLandingRoute(['Cashier'], ['Order.Create'])).toBe('/pos/orders');
    expect(resolveLandingRoute(['Cashier'], ['Payment.Record'])).toBe('/pos/orders');
  });

  it('routes kitchen users with kitchen permissions to kitchen tickets', () => {
    expect(resolveLandingRoute(['KitchenUser'], ['KitchenTicket.View'])).toBe('/kitchen/tickets');
    expect(resolveLandingRoute(['KitchenUser'], ['KitchenTicket.UpdateStatus'])).toBe('/kitchen/tickets');
  });

  it('routes waiters with order permissions to POS orders', () => {
    expect(resolveLandingRoute(['Waiter'], ['Order.Create'])).toBe('/pos/orders');
  });

  it('routes billing-only users to billing and report-only users to the owner dashboard', () => {
    expect(resolveLandingRoute([], ['Billing.View'])).toBe('/billing');
    expect(resolveLandingRoute([], ['Report.View'])).toBe('/owner/dashboard');
  });

  it('falls back to the dashboard when nothing matches', () => {
    expect(resolveLandingRoute(['Cashier'], ['MenuItem.View'])).toBe('/');
    expect(resolveLandingRoute([], [])).toBe('/');
  });

  it('respects the configured priority order when multiple matches exist', () => {
    expect(
      resolveLandingRoute(['RestaurantOwner', 'Cashier'], ['Report.View', 'Order.Create', 'Billing.View'])
    ).toBe('/owner/dashboard');
    expect(resolveLandingRoute(['Cashier', 'Waiter'], ['Order.View', 'Payment.Record'])).toBe('/pos/orders');
    expect(resolveLandingRoute(['Cashier'], ['Billing.View', 'Order.Create'])).toBe('/pos/orders');
  });
});
