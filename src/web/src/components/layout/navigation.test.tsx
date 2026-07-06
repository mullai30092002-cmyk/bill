import { describe, expect, it } from 'vitest';

import { shellNavItems, getVisibleShellNavItems } from './navigation';

describe('getVisibleShellNavItems', () => {
  it('shows Cashier Shifts when any cashier-shift permission is present', () => {
    expect(getVisibleShellNavItems(['CashShift.View']).some(item => item.label === 'Cashier Shifts')).toBe(true);
    expect(getVisibleShellNavItems(['CashShift.Manage']).some(item => item.label === 'Cashier Shifts')).toBe(true);
    expect(getVisibleShellNavItems(['CashMovement.Record']).some(item => item.label === 'Cashier Shifts')).toBe(true);
  });

  it('shows Kitchen Display when any kitchen-ticket permission is present', () => {
    expect(getVisibleShellNavItems(['KitchenTicket.View']).some(item => item.label === 'Kitchen Display')).toBe(true);
    expect(getVisibleShellNavItems(['KitchenTicket.Manage']).some(item => item.label === 'Kitchen Display')).toBe(true);
    expect(getVisibleShellNavItems(['KitchenTicket.UpdateStatus']).some(item => item.label === 'Kitchen Display')).toBe(true);
  });

  it('hides Cashier Shifts when cashier-shift permissions are missing', () => {
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Cashier Shifts')).toBe(false);
    expect(getVisibleShellNavItems([]).some(item => item.label === 'Cashier Shifts')).toBe(false);
  });

  it('hides Kitchen Display when kitchen-ticket permissions are missing', () => {
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Kitchen Display')).toBe(false);
    expect(getVisibleShellNavItems([]).some(item => item.label === 'Kitchen Display')).toBe(false);
  });

  it('shows Daily Report only when Report.View is present', () => {
    expect(getVisibleShellNavItems(['Report.View']).some(item => item.label === 'Daily Report')).toBe(true);
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Daily Report')).toBe(false);
  });

  it('shows Vendor Payables only when Report.View is present', () => {
    expect(getVisibleShellNavItems(['Report.View']).some(item => item.label === 'Vendor Payables')).toBe(true);
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Vendor Payables')).toBe(false);
  });

  it('shows Cash Reconciliation only when Report.View is present', () => {
    expect(getVisibleShellNavItems(['Report.View']).some(item => item.label === 'Cash Reconciliation')).toBe(true);
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Cash Reconciliation')).toBe(false);
  });

  it('shows Setup when owner or admin permissions are present', () => {
    expect(getVisibleShellNavItems(['Report.View']).some(item => item.label === 'Setup')).toBe(true);
    expect(getVisibleShellNavItems(['Branch.Manage']).some(item => item.label === 'Setup')).toBe(true);
    expect(getVisibleShellNavItems(['User.Manage']).some(item => item.label === 'Setup')).toBe(true);
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Setup')).toBe(false);
  });

  it('shows Owner Dashboard only when Report.View is present', () => {
    expect(getVisibleShellNavItems(['Report.View']).some(item => item.label === 'Owner Dashboard')).toBe(true);
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Owner Dashboard')).toBe(false);
  });

  it('shows Vendors when any vendor permission is present', () => {
    expect(getVisibleShellNavItems(['VendorBill.Confirm']).some(item => item.label === 'Vendors')).toBe(true);
    expect(getVisibleShellNavItems(['VendorPayment.Create']).some(item => item.label === 'Vendors')).toBe(true);
    expect(getVisibleShellNavItems(['Billing.View']).some(item => item.label === 'Vendors')).toBe(false);
  });

  it('has no duplicate icons across all nav items', () => {
    const icons = shellNavItems.map(item => item.icon);
    const duplicates = icons.filter((icon, idx) => icons.indexOf(icon) !== idx);
    expect(duplicates).toEqual([]);
  });

  it('report nav items each have distinct icons', () => {
    const reportItems = shellNavItems.filter(item =>
      ['Daily Report', 'Cash Reconciliation', 'Vendor Payables', 'Prepared Stock', 'Expiry Stock'].includes(item.label)
    );
    const reportIcons = reportItems.map(item => item.icon);
    const unique = new Set(reportIcons);
    expect(unique.size).toBe(reportItems.length);
  });
});
