import { useNavigate } from 'react-router-dom';
import { ModuleLayout } from '../../components/layout';
import type { ShellNavItem } from '../../components/layout/navigation';
import { DASHBOARD_SAMPLE } from './dashboardFixtures';
import type { DashboardSnapshot } from './dashboardFixtures';

export interface DashboardPageProps {
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
  /** Override the displayed snapshot — primarily for testing. Defaults to DASHBOARD_SAMPLE. */
  snapshot?: DashboardSnapshot;
}

/* ── Static workspace tiles ── */
const workspaceTiles = [
  {
    title: 'Orders',
    hint: 'Eat-in and parcel order capture',
    to: '/pos/orders',
    cls: 'db-tile--orders',
    icon: '🧾',
  },
  {
    title: 'Billing',
    hint: 'Settle bills and record payments',
    to: '/billing',
    cls: 'db-tile--orders',
    icon: '💳',
  },
  {
    title: 'Daily Report',
    hint: 'Cash sales, shift variance, control exceptions',
    to: '/reports/daily-cash-sales',
    cls: 'db-tile--dashboard',
    icon: '📊',
  },
  {
    title: 'Kitchen Tickets',
    hint: 'Live kitchen queue and ticket status',
    to: '/kitchen/tickets',
    cls: 'db-tile--accent',
    icon: '🔥',
  },
  {
    title: 'Inventory',
    hint: 'Stock levels, adjustments, movement history',
    to: '/inventory',
    cls: 'db-tile--inventory',
    icon: '📦',
  },
  {
    title: 'Admin Setup',
    hint: 'Users, roles, branches, menu',
    to: '/admin/users',
    cls: 'db-tile--admin',
    icon: '⚙️',
  },
] as const;

/* ── Quick action sidebar links ── */
const quickLinks = [
  { label: 'Open Orders', to: '/pos/orders' },
  { label: 'Open Billing', to: '/billing' },
  { label: 'View Daily Report', to: '/reports/daily-cash-sales' },
  { label: 'Kitchen Tickets', to: '/kitchen/tickets' },
  { label: 'View Inventory', to: '/inventory' },
  { label: 'Manage Users', to: '/admin/users' },
] as const;

const severityDot: Record<'warning' | 'danger' | 'info', string> = {
  warning: 'db-dot--warning',
  danger: 'db-dot--danger',
  info: 'db-dot--info',
};

export const DashboardPage = ({
  navItems,
  restaurantName,
  branchName,
  operatorLabel,
  snapshot = DASHBOARD_SAMPLE,
}: DashboardPageProps) => {
  const navigate = useNavigate();

  return (
    <ModuleLayout
      tone="dashboard"
      title="BillSoft dashboard"
      description="Monitor today's sales, active orders, cashier shifts, kitchen tickets, inventory alerts, and setup actions from one place."
      breadcrumbs={['Dashboard']}
      operatorLabel={operatorLabel ?? 'Signed in'}
      navItems={navItems}
      restaurantName={restaurantName}
      branchName={branchName}
      maxWidth="xl"
    >
      <div className="db-layout">

        {/* ── Sample data notice ── */}
        <p className="db-sample-notice" role="note">
          <span className="db-sample-notice__badge">Sample data</span>
          Figures below are illustrative. A live summary API is not yet connected — open individual workspaces for real-time data.
        </p>

        {/* ═══ KPI strip ═══ */}
        <div className="db-kpi-strip">
          <div className="db-kpi-card db-kpi-card--sales">
            <span className="db-kpi-card__label">Today's sales</span>
            <span className="db-kpi-card__value">{snapshot.todaysSales}</span>
            <span className="db-kpi-card__note">Open <strong>Daily Report</strong> for full breakdown</span>
          </div>
          <div className="db-kpi-card db-kpi-card--paid">
            <span className="db-kpi-card__label">Paid today</span>
            <span className="db-kpi-card__value">{snapshot.paidToday}</span>
            <span className="db-kpi-card__note">Recorded payments across all modes</span>
          </div>
          <div className="db-kpi-card db-kpi-card--shifts">
            <span className="db-kpi-card__label">Open shifts</span>
            <span className="db-kpi-card__value">{snapshot.openShifts}</span>
            <span className="db-kpi-card__note">Cashier shifts currently open</span>
          </div>
          <div className="db-kpi-card db-kpi-card--alerts">
            <span className="db-kpi-card__label">Alerts needing attention</span>
            <span className="db-kpi-card__value">{snapshot.alertsCount}</span>
            <span className="db-kpi-card__note">Open <strong>Daily Report</strong> to review</span>
          </div>
        </div>

        {/* ═══ Body: main + sidebar ═══ */}
        <div className="db-body">

          {/* ── Left/main column ── */}
          <div className="db-main">

            {/* Live operations */}
            <section className="db-section" aria-labelledby="db-live-heading">
              <h2 id="db-live-heading" className="db-section__heading">Live operations</h2>
              <div className="db-ops-grid">

                <div className="db-ops-card">
                  <span className="db-ops-card__label">Open orders</span>
                  <span className="db-ops-card__value">{snapshot.openOrders}</span>
                  <span className="db-ops-card__sub">Active eat-in and parcel orders</span>
                  <button
                    type="button"
                    className="db-ops-card__link"
                    onClick={() => navigate('/pos/orders')}
                  >
                    Go to Orders →
                  </button>
                </div>

                <div className="db-ops-card">
                  <span className="db-ops-card__label">Pending kitchen tickets</span>
                  <span className="db-ops-card__value">{snapshot.pendingKitchenTickets}</span>
                  <span className="db-ops-card__sub">Tickets waiting to be prepared</span>
                  <button
                    type="button"
                    className="db-ops-card__link"
                    onClick={() => navigate('/kitchen/tickets')}
                  >
                    Go to Kitchen →
                  </button>
                </div>

                <div className="db-ops-card">
                  <span className="db-ops-card__label">Active cashier shifts</span>
                  <span className="db-ops-card__value">{snapshot.activeCashierShifts}</span>
                  <span className="db-ops-card__sub">Shifts currently open</span>
                  <button
                    type="button"
                    className="db-ops-card__link"
                    onClick={() => navigate('/cashier/shifts')}
                  >
                    Go to Shifts →
                  </button>
                </div>

              </div>
            </section>

            {/* Attention required */}
            <section className="db-section" aria-labelledby="db-attention-heading">
              <h2 id="db-attention-heading" className="db-section__heading">Attention required</h2>
              <div className="db-attention-list">
                {snapshot.attention.map(item => (
                  <button
                    key={item.label}
                    type="button"
                    className="db-attention-row"
                    onClick={() => navigate(item.to)}
                    aria-label={`${item.label}: ${item.count} item${item.count !== 1 ? 's' : ''} — ${item.note}`}
                  >
                    <span className={`db-dot ${severityDot[item.severity]}`} aria-hidden="true" />
                    <span className="db-attention-row__content">
                      <span className="db-attention-row__label">{item.label}</span>
                      <span className="db-attention-row__note">{item.note}</span>
                    </span>
                    <span className="db-attention-row__count">{item.count}</span>
                    <span className="db-attention-row__arrow" aria-hidden="true">→</span>
                  </button>
                ))}
              </div>
            </section>

            {/* Workspace entry points */}
            <section className="db-section" aria-labelledby="db-workspaces-heading">
              <h2 id="db-workspaces-heading" className="db-section__heading">Workspaces</h2>
              <div className="db-tile-grid">
                {workspaceTiles.map(tile => (
                  <button
                    key={tile.to}
                    type="button"
                    className={`db-tile ${tile.cls}`}
                    onClick={() => navigate(tile.to)}
                  >
                    <span className="db-tile__icon" aria-hidden="true">{tile.icon}</span>
                    <span className="db-tile__title">{tile.title}</span>
                    <span className="db-tile__hint">{tile.hint}</span>
                  </button>
                ))}
              </div>
            </section>

          </div>

          {/* ── Sidebar ── */}
          <aside className="db-sidebar">

            <div className="db-quick-actions">
              <p className="db-section-label">Quick actions</p>
              <div className="db-quick-actions__list">
                {quickLinks.map(link => (
                  <button
                    key={link.to}
                    type="button"
                    className="db-quick-link"
                    onClick={() => navigate(link.to)}
                  >
                    <span className="db-quick-link__label">{link.label}</span>
                    <span className="db-quick-link__arrow" aria-hidden="true">→</span>
                  </button>
                ))}
              </div>
            </div>

            <div className="db-system-panel">
              <p className="db-system-panel__title">System</p>
              <ul className="db-system-panel__list">
                <li>Role-based access per permission code</li>
                <li>Touch-friendly 44 px minimum targets</li>
                <li>Mobile, tablet, and desktop responsive</li>
                <li>Shared brand shell across all workspaces</li>
              </ul>
            </div>

          </aside>

        </div>
      </div>
    </ModuleLayout>
  );
};

export default DashboardPage;
