/**
 * Frontend-only mock snapshot for the dashboard page.
 * Used as the default display state until a real dashboard summary API exists.
 * Clearly labelled "Sample data" in the UI — never presented as production data.
 */
export interface DashboardSnapshot {
  /** Formatted currency string, e.g. "₹4,280.50" */
  todaysSales: string;
  /** Formatted currency string */
  paidToday: string;
  openShifts: number;
  alertsCount: number;
  openOrders: number;
  pendingKitchenTickets: number;
  activeCashierShifts: number;
  attention: {
    label: string;
    count: number;
    note: string;
    to: string;
    severity: 'warning' | 'danger' | 'info';
  }[];
}

export const DASHBOARD_SAMPLE: DashboardSnapshot = {
  todaysSales: '₹4,280.50',
  paidToday: '₹3,960.00',
  openShifts: 2,
  alertsCount: 3,
  openOrders: 7,
  pendingKitchenTickets: 4,
  activeCashierShifts: 2,
  attention: [
    {
      label: 'Unpaid bills',
      count: 2,
      note: '₹320.50 outstanding — open Daily Report',
      to: '/reports/daily-cash-sales',
      severity: 'warning',
    },
    {
      label: 'Cash variance',
      count: 1,
      note: '₹−45.00 on Shift #3 — review Daily Report',
      to: '/reports/daily-cash-sales',
      severity: 'danger',
    },
    {
      label: 'Vendor dues',
      count: 3,
      note: '₹1,200.00 outstanding — open Vendor Payables',
      to: '/reports/vendor-payables',
      severity: 'info',
    },
  ],
};
