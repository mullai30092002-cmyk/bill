import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { renderWithMemoryRouter } from '../../test/renderWithRouter';
import KitchenTicketStatusActions from './KitchenTicketStatusActions';
import type { KitchenTicketDetail } from './kitchenTicketTypes';

const buildTicket = (status: KitchenTicketDetail['status']): KitchenTicketDetail => ({
  kitchenTicketId: 'ticket-1',
  restaurantId: 'restaurant-1',
  branchId: 'branch-1',
  posOrderId: 'order-1',
  ticketNumber: 'KIT-20260613-0001',
  orderNumberSnapshot: 'ORD-20260613-0001',
  orderTypeSnapshot: 'EatIn',
  tableNameSnapshot: 'Table 5',
  customerNameSnapshot: null,
  orderNotesSnapshot: null,
  status,
  createdByUserId: 'user-1',
  lastStatusChangedByUserId: 'user-1',
  cancelledByUserId: null,
  cancelledAt: null,
  cancelReason: null,
  createdAt: '2026-06-13T08:00:00Z',
  updatedAt: '2026-06-13T08:02:00Z',
  preparingAt: null,
  readyAt: null,
  servedAt: null,
  inventoryDeductionStatus: 'NotDeducted',
  lines: [],
});

const defaultProps = {
  canUpdateStatus: true,
  canManage: true,
  submitting: false,
  cancelReason: '',
  cancelConfirmPending: false,
  onCancelReasonChange: vi.fn(),
  onStatusChange: vi.fn(),
  onCancelRequest: vi.fn(),
  onCancelConfirm: vi.fn(),
  onCancelAbort: vi.fn(),
};

describe('KitchenTicketStatusActions', () => {
  // ── Status-specific buttons ────────────────────────────────────────────────

  it.each([
    ['Pending',   ['Start Preparing', 'Mark Ready']],
    ['Preparing', ['Mark Ready']],
    ['Ready',     ['Mark Served']],
  ] as const)('shows correct action buttons for %s ticket', (status, expectedButtons) => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions {...defaultProps} ticket={buildTicket(status)} />
    );
    for (const label of expectedButtons) {
      expect(screen.getByRole('button', { name: label })).toBeInTheDocument();
    }
  });

  it.each(['Served', 'Cancelled'] as const)('shows no status buttons for terminal %s ticket', status => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions {...defaultProps} ticket={buildTicket(status)} />
    );
    expect(screen.queryByRole('button', { name: /start preparing/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /mark ready/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /mark served/i })).not.toBeInTheDocument();
  });

  it('shows terminal message for Served ticket', () => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions {...defaultProps} ticket={buildTicket('Served')} />
    );
    expect(screen.getByText(/this ticket has been served/i)).toBeInTheDocument();
  });

  it('shows terminal message for Cancelled ticket', () => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions {...defaultProps} ticket={buildTicket('Cancelled')} />
    );
    expect(screen.getByText(/this ticket has been cancelled/i)).toBeInTheDocument();
  });

  // ── Cancel section visibility ──────────────────────────────────────────────

  it.each(['Pending', 'Preparing', 'Ready'] as const)('shows cancel input for manageable %s ticket', status => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions {...defaultProps} ticket={buildTicket(status)} />
    );
    expect(screen.getByLabelText(/cancel reason/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /cancel ticket/i })).toBeInTheDocument();
  });

  it.each(['Served', 'Cancelled'] as const)('hides cancel input for terminal %s ticket', status => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions {...defaultProps} ticket={buildTicket(status)} />
    );
    expect(screen.queryByLabelText(/cancel reason/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel ticket/i })).not.toBeInTheDocument();
  });

  it('hides cancel input when user lacks KitchenTicket.Manage', () => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions
        {...defaultProps}
        canManage={false}
        ticket={buildTicket('Pending')}
      />
    );
    expect(screen.queryByLabelText(/cancel reason/i)).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel ticket/i })).not.toBeInTheDocument();
  });

  // ── Inline cancel confirmation ─────────────────────────────────────────────

  it('renders inline confirm panel when cancelConfirmPending is true', () => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions
        {...defaultProps}
        ticket={buildTicket('Pending')}
        cancelConfirmPending={true}
      />
    );
    expect(screen.getByRole('alertdialog')).toBeInTheDocument();
    expect(screen.getByText(/cannot be undone/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /confirm cancel/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /go back/i })).toBeInTheDocument();
  });

  it('calls onCancelConfirm when Confirm cancel clicked', async () => {
    const onCancelConfirm = vi.fn();
    const user = userEvent.setup();
    renderWithMemoryRouter(
      <KitchenTicketStatusActions
        {...defaultProps}
        ticket={buildTicket('Pending')}
        cancelConfirmPending={true}
        onCancelConfirm={onCancelConfirm}
      />
    );
    await user.click(screen.getByRole('button', { name: /confirm cancel/i }));
    expect(onCancelConfirm).toHaveBeenCalledTimes(1);
  });

  it('calls onCancelAbort when Go back clicked', async () => {
    const onCancelAbort = vi.fn();
    const user = userEvent.setup();
    renderWithMemoryRouter(
      <KitchenTicketStatusActions
        {...defaultProps}
        ticket={buildTicket('Pending')}
        cancelConfirmPending={true}
        onCancelAbort={onCancelAbort}
      />
    );
    await user.click(screen.getByRole('button', { name: /go back/i }));
    expect(onCancelAbort).toHaveBeenCalledTimes(1);
  });

  it('hides normal buttons while confirm panel is showing', () => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions
        {...defaultProps}
        ticket={buildTicket('Pending')}
        cancelConfirmPending={true}
      />
    );
    expect(screen.queryByRole('button', { name: /start preparing/i })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /cancel ticket/i })).not.toBeInTheDocument();
  });

  // ── Disabled states while submitting ─────────────────────────────────────

  it('disables all buttons while submitting', () => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions
        {...defaultProps}
        ticket={buildTicket('Pending')}
        submitting={true}
      />
    );
    for (const btn of screen.getAllByRole('button')) {
      expect(btn).toBeDisabled();
    }
  });

  // ── Empty state ────────────────────────────────────────────────────────────

  it('shows empty state when no ticket is selected', () => {
    renderWithMemoryRouter(
      <KitchenTicketStatusActions {...defaultProps} ticket={null} />
    );
    expect(screen.getByText(/select a ticket to manage status/i)).toBeInTheDocument();
  });
});
