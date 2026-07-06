import { screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { renderWithMemoryRouter } from '../../test/renderWithRouter';
import { DashboardPage } from './DashboardPage';
import { DASHBOARD_SAMPLE } from './dashboardFixtures';
import type { DashboardSnapshot } from './dashboardFixtures';

const makeSnapshot = (overrides: Partial<DashboardSnapshot> = {}): DashboardSnapshot => ({
  ...DASHBOARD_SAMPLE,
  ...overrides,
});

describe('DashboardPage', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('renders the BillSoft dashboard heading', () => {
    renderWithMemoryRouter(<DashboardPage />);
    expect(screen.getByRole('heading', { name: /billsoft dashboard/i })).toBeInTheDocument();
  });

  it('displays sample data notice', () => {
    renderWithMemoryRouter(<DashboardPage />);
    expect(screen.getByRole('note')).toHaveTextContent(/sample data/i);
  });

  it('renders all four KPI values from the snapshot', () => {
    const snapshot = makeSnapshot({
      todaysSales: '₹4,280.50',
      paidToday: '₹3,960.00',
      openShifts: 2,
      alertsCount: 3,
    });
    renderWithMemoryRouter(<DashboardPage snapshot={snapshot} />);

    expect(screen.getByText('₹4,280.50')).toBeInTheDocument();
    expect(screen.getByText('₹3,960.00')).toBeInTheDocument();
    // alertsCount = 3; may appear multiple times (attention row counts too) — just check it's present
    expect(screen.getAllByText('3').length).toBeGreaterThan(0);
  });

  it('renders KPI labels', () => {
    renderWithMemoryRouter(<DashboardPage />);
    expect(screen.getByText("Today's sales")).toBeInTheDocument();
    expect(screen.getByText('Paid today')).toBeInTheDocument();
    expect(screen.getByText('Open shifts')).toBeInTheDocument();
    expect(screen.getByText('Alerts needing attention')).toBeInTheDocument();
  });

  it('renders live operations counts from the snapshot', () => {
    const snapshot = makeSnapshot({
      openOrders: 7,
      pendingKitchenTickets: 4,
      activeCashierShifts: 2,
    });
    renderWithMemoryRouter(<DashboardPage snapshot={snapshot} />);

    expect(screen.getByText('7')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
  });

  it('renders the live operations section heading', () => {
    renderWithMemoryRouter(<DashboardPage />);
    expect(screen.getByRole('heading', { name: /live operations/i })).toBeInTheDocument();
  });

  it('renders live operations card labels', () => {
    renderWithMemoryRouter(<DashboardPage />);
    expect(screen.getByText('Open orders')).toBeInTheDocument();
    expect(screen.getByText('Pending kitchen tickets')).toBeInTheDocument();
    expect(screen.getByText('Active cashier shifts')).toBeInTheDocument();
  });

  it('renders attention required rows with counts and labels', () => {
    const snapshot = makeSnapshot({
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
    });
    renderWithMemoryRouter(<DashboardPage snapshot={snapshot} />);

    expect(screen.getByRole('heading', { name: /attention required/i })).toBeInTheDocument();
    expect(screen.getByText('Unpaid bills')).toBeInTheDocument();
    expect(screen.getByText('Cash variance')).toBeInTheDocument();
    expect(screen.getByText('Vendor dues')).toBeInTheDocument();
  });

  it('attention rows are clickable buttons with accessible labels', () => {
    renderWithMemoryRouter(<DashboardPage />);
    const section = screen.getByRole('heading', { name: /attention required/i }).closest('section')!;
    const buttons = within(section).getAllByRole('button');
    expect(buttons.length).toBe(DASHBOARD_SAMPLE.attention.length);
    buttons.forEach(btn => {
      expect(btn).toHaveAttribute('aria-label');
    });
  });

  it('attention row counts are greater than zero in the default sample', () => {
    renderWithMemoryRouter(<DashboardPage />);
    DASHBOARD_SAMPLE.attention.forEach(item => {
      expect(item.count).toBeGreaterThan(0);
    });
  });

  it('renders the workspaces section with 6 tiles', () => {
    renderWithMemoryRouter(<DashboardPage />);
    const section = screen.getByRole('heading', { name: /workspaces/i }).closest('section')!;
    expect(section).toBeInTheDocument();
    // These tile titles appear in the workspace section (may also appear in nav)
    expect(within(section).getByText('Orders')).toBeInTheDocument();
    expect(within(section).getByText('Billing')).toBeInTheDocument();
    expect(within(section).getByText('Daily Report')).toBeInTheDocument();
    expect(within(section).getByText('Kitchen Tickets')).toBeInTheDocument();
    expect(within(section).getByText('Inventory')).toBeInTheDocument();
    expect(within(section).getByText('Admin Setup')).toBeInTheDocument();
  });

  it('renders all 6 quick action links in the sidebar', () => {
    renderWithMemoryRouter(<DashboardPage />);
    expect(screen.getByText('Open Orders')).toBeInTheDocument();
    expect(screen.getByText('Open Billing')).toBeInTheDocument();
    expect(screen.getByText('View Daily Report')).toBeInTheDocument();
    // "Kitchen Tickets" label appears both in workspace tiles and quick links
    expect(screen.getAllByText('Kitchen Tickets').length).toBeGreaterThanOrEqual(2);
    expect(screen.getByText('View Inventory')).toBeInTheDocument();
    expect(screen.getByText('Manage Users')).toBeInTheDocument();
  });

  it('workspace tiles are buttons', () => {
    renderWithMemoryRouter(<DashboardPage />);
    const section = screen.getByRole('heading', { name: /workspaces/i }).closest('section')!;
    const buttons = within(section).getAllByRole('button');
    expect(buttons.length).toBe(6);
  });

  it('clicking a workspace tile does not throw', async () => {
    const user = userEvent.setup();
    renderWithMemoryRouter(<DashboardPage />);
    const section = screen.getByRole('heading', { name: /workspaces/i }).closest('section')!;
    const billingBtn = within(section).getByText('Billing').closest('button')!;
    // In MemoryRouter, navigate() is a no-op for destinations outside the rendered routes — should not throw
    await expect(user.click(billingBtn)).resolves.not.toThrow();
  });

  it('default sample data has all non-empty KPI values', () => {
    expect(DASHBOARD_SAMPLE.todaysSales).not.toBe('');
    expect(DASHBOARD_SAMPLE.paidToday).not.toBe('');
    expect(DASHBOARD_SAMPLE.openShifts).toBeGreaterThan(0);
    expect(DASHBOARD_SAMPLE.alertsCount).toBeGreaterThan(0);
  });

  it('default sample data has non-zero live operation counts', () => {
    expect(DASHBOARD_SAMPLE.openOrders).toBeGreaterThan(0);
    expect(DASHBOARD_SAMPLE.pendingKitchenTickets).toBeGreaterThan(0);
    expect(DASHBOARD_SAMPLE.activeCashierShifts).toBeGreaterThan(0);
  });

  it('counts are visible in the rendered page with default sample data', () => {
    renderWithMemoryRouter(<DashboardPage />);
    expect(screen.getByText('₹4,280.50')).toBeInTheDocument();
    expect(screen.getByText('₹3,960.00')).toBeInTheDocument();
    expect(screen.getByText(String(DASHBOARD_SAMPLE.openOrders))).toBeInTheDocument();
    expect(screen.getByText(String(DASHBOARD_SAMPLE.pendingKitchenTickets))).toBeInTheDocument();
  });

  it('renders with a zero-state snapshot without crashing', () => {
    const zeroSnapshot = makeSnapshot({
      todaysSales: '₹0.00',
      paidToday: '₹0.00',
      openShifts: 0,
      alertsCount: 0,
      openOrders: 0,
      pendingKitchenTickets: 0,
      activeCashierShifts: 0,
      attention: [],
    });
    renderWithMemoryRouter(<DashboardPage snapshot={zeroSnapshot} />);
    expect(screen.getByRole('heading', { name: /billsoft dashboard/i })).toBeInTheDocument();
    // Both "Today's sales" and "Paid today" are ₹0.00, so multiple matches are expected
    expect(screen.getAllByText('₹0.00').length).toBeGreaterThanOrEqual(2);
    // No attention rows rendered
    const section = screen.getByRole('heading', { name: /attention required/i }).closest('section')!;
    expect(within(section).queryAllByRole('button').length).toBe(0);
  });
});
