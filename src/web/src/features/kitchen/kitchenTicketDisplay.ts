import type { BadgeTone } from '../../components/ui/Badge';
import type { KitchenTicketDetail, KitchenTicketListItem, KitchenTicketQueueFilter, KitchenTicketStatus } from './kitchenTicketTypes';

export const kitchenTicketStatusOrder: KitchenTicketStatus[] = ['Pending', 'Preparing', 'Ready', 'Served', 'Cancelled'];

const activeStatuses = new Set<KitchenTicketStatus>(['Pending', 'Preparing', 'Ready']);

export interface KitchenTicketDisplayMessages {
  statusPending: string;
  statusPreparing: string;
  statusReady: string;
  statusServed: string;
  statusCancelled: string;
  orderTypeEatIn: string;
  orderTypeParcel: string;
  notAvailable: string;
  notRecorded: string;
  activeQueue: string;
  allTickets: string;
  createdPrefix: string;
  updatedPrefix: string;
  ageUnavailable: string;
  ageMinutesOld: string;
  ageHoursOld: string;
  ageHoursMinutesOld: string;
  ageDaysOld: string;
  ageDaysHoursOld: string;
  ageDaysHoursMinutesOld: string;
  sufficient: string;
  insufficient: string;
  noRecipe: string;
}

const defaultMessages: KitchenTicketDisplayMessages = {
  statusPending: 'Pending',
  statusPreparing: 'Preparing',
  statusReady: 'Ready',
  statusServed: 'Served',
  statusCancelled: 'Cancelled',
  orderTypeEatIn: 'Eat-in',
  orderTypeParcel: 'Parcel',
  notAvailable: 'Not available',
  notRecorded: 'not recorded',
  activeQueue: 'Active queue',
  allTickets: 'All tickets',
  createdPrefix: 'Created',
  updatedPrefix: 'Updated',
  ageUnavailable: 'Age unavailable',
  ageMinutesOld: '{count}m old',
  ageHoursOld: '{count}h old',
  ageHoursMinutesOld: '{hours}h {minutes}m old',
  ageDaysOld: '{count}d old',
  ageDaysHoursOld: '{days}d {hours}h old',
  ageDaysHoursMinutesOld: '{days}d {hours}h {minutes}m old',
  sufficient: 'Sufficient',
  insufficient: 'Insufficient',
  noRecipe: 'No recipe',
};

const interpolate = (template: string, values: Record<string, string | number>) =>
  Object.entries(values).reduce((result, [key, value]) => result.split(`{${key}}`).join(String(value)), template);

export const kitchenTicketStatusTone: Record<KitchenTicketStatus, BadgeTone> = {
  Pending: 'warning',
  Preparing: 'primary',
  Ready: 'success',
  Served: 'success',
  Cancelled: 'danger',
};

export const isActiveKitchenTicket = (status: KitchenTicketStatus) => activeStatuses.has(status);

export const formatKitchenTicketStatus = (value: KitchenTicketStatus, messages: KitchenTicketDisplayMessages = defaultMessages) => {
  switch (value) {
    case 'Pending':
      return messages.statusPending;
    case 'Preparing':
      return messages.statusPreparing;
    case 'Ready':
      return messages.statusReady;
    case 'Served':
      return messages.statusServed;
    case 'Cancelled':
      return messages.statusCancelled;
    default:
      return value;
  }
};

export const sortKitchenTickets = (tickets: KitchenTicketListItem[]) =>
  [...tickets].sort((left, right) => {
    const leftActive = isActiveKitchenTicket(left.status) ? 0 : 1;
    const rightActive = isActiveKitchenTicket(right.status) ? 0 : 1;
    if (leftActive !== rightActive) {
      return leftActive - rightActive;
    }

    const leftStatus = kitchenTicketStatusOrder.indexOf(left.status);
    const rightStatus = kitchenTicketStatusOrder.indexOf(right.status);
    if (leftStatus !== rightStatus) {
      return leftStatus - rightStatus;
    }

    const createdDelta = Date.parse(right.createdAt) - Date.parse(left.createdAt);
    if (createdDelta !== 0) {
      return createdDelta;
    }

    return right.ticketNumber.localeCompare(left.ticketNumber, undefined, { sensitivity: 'base' });
  });

export const filterKitchenTickets = (tickets: KitchenTicketListItem[], filter: KitchenTicketQueueFilter) => {
  const sortedTickets = sortKitchenTickets(tickets);

  if (filter === 'All') {
    return sortedTickets;
  }

  if (filter === 'Active') {
    return sortedTickets.filter(ticket => isActiveKitchenTicket(ticket.status));
  }

  return sortedTickets.filter(ticket => ticket.status === filter);
};

const formatUtcTimestamp = (value: string) => {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return `${date.toISOString().slice(0, 16).replace('T', ' ')} UTC`;
};

export const formatKitchenTicketCreatedLabel = (
  ticket: KitchenTicketListItem | KitchenTicketDetail,
  messages: KitchenTicketDisplayMessages = defaultMessages
) => `${messages.createdPrefix} ${formatUtcTimestamp(ticket.createdAt)}`;

export const formatKitchenTicketUpdatedLabel = (
  ticket: KitchenTicketListItem | KitchenTicketDetail,
  messages: KitchenTicketDisplayMessages = defaultMessages
) => (ticket.updatedAt ? `${messages.updatedPrefix} ${formatUtcTimestamp(ticket.updatedAt)}` : `${messages.updatedPrefix} ${messages.notRecorded}`);

export const formatKitchenTicketAge = (
  createdAt: string,
  messages: KitchenTicketDisplayMessages = defaultMessages,
  now = new Date()
) => {
  const created = new Date(createdAt);
  if (Number.isNaN(created.getTime())) {
    return messages.ageUnavailable;
  }

  const minutes = Math.max(0, Math.round((now.getTime() - created.getTime()) / 60000));
  if (minutes < 60) {
    return interpolate(messages.ageMinutesOld, { count: minutes });
  }

  const hours = Math.floor(minutes / 60);
  const remainingMinutes = minutes % 60;
  if (hours < 24) {
    return remainingMinutes > 0
      ? interpolate(messages.ageHoursMinutesOld, { hours, minutes: remainingMinutes })
      : interpolate(messages.ageHoursOld, { count: hours });
  }

  const days = Math.floor(hours / 24);
  const remainingHours = hours % 24;
  if (remainingMinutes > 0) {
    return interpolate(messages.ageDaysHoursMinutesOld, { days, hours: remainingHours, minutes: remainingMinutes });
  }

  return remainingHours > 0
    ? interpolate(messages.ageDaysHoursOld, { days, hours: remainingHours })
    : interpolate(messages.ageDaysOld, { count: days });
};

export const formatKitchenTicketLifecycleLabel = (
  label: string,
  value: string | null,
  messages: KitchenTicketDisplayMessages = defaultMessages
) => (value ? `${label} ${formatUtcTimestamp(value)}` : `${label} ${messages.notRecorded}`);

export const formatKitchenTicketSummary = (ticket: KitchenTicketListItem, messages: KitchenTicketDisplayMessages = defaultMessages) =>
  `${ticket.orderNumberSnapshot} · ${formatKitchenTicketOrderType(ticket.orderTypeSnapshot === 'EatIn' ? 'EatIn' : 'Parcel', messages)}`;

export const formatKitchenTicketOrderType = (
  value: 'EatIn' | 'Parcel',
  messages: KitchenTicketDisplayMessages = defaultMessages
) => (value === 'EatIn' ? messages.orderTypeEatIn : messages.orderTypeParcel);

export const formatKitchenTicketReference = (ticket: KitchenTicketListItem | KitchenTicketDetail): string | null => {
  if (ticket.tableNameSnapshot) {
    return ticket.tableNameSnapshot;
  }
  if (ticket.customerNameSnapshot) {
    return ticket.customerNameSnapshot;
  }
  return null;
};

export type KitchenTicketUrgency = 'normal' | 'warning' | 'critical';

export const getKitchenTicketUrgency = (createdAt: string, status: KitchenTicketStatus, now = new Date()): KitchenTicketUrgency => {
  if (!isActiveKitchenTicket(status)) {
    return 'normal';
  }
  const created = new Date(createdAt);
  if (Number.isNaN(created.getTime())) {
    return 'normal';
  }
  const minutes = (now.getTime() - created.getTime()) / 60000;
  if (minutes >= 20) {
    return 'critical';
  }
  if (minutes >= 10) {
    return 'warning';
  }
  return 'normal';
};

export const getKitchenTicketFilterTitle = (filter: KitchenTicketQueueFilter) => {
  if (filter === 'Active') {
    return defaultMessages.activeQueue;
  }

  if (filter === 'All') {
    return defaultMessages.allTickets;
  }

  return `${filter} tickets`;
};

export const formatKitchenTicketPreviewStatus = (
  value: 'Sufficient' | 'Insufficient' | 'NoRecipe',
  messages: KitchenTicketDisplayMessages = defaultMessages
) => {
  switch (value) {
    case 'Sufficient':
      return messages.sufficient;
    case 'Insufficient':
      return messages.insufficient;
    case 'NoRecipe':
      return messages.noRecipe;
    default:
      return value;
  }
};
