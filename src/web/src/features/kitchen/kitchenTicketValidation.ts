import type {
  CancelKitchenTicketRequest,
  KitchenTicketStatus,
  UpdateKitchenTicketStatusRequest,
} from './kitchenTicketTypes';

export interface KitchenTicketValidationMessages {
  cancelReasonRequired: string;
}

const defaultMessages: KitchenTicketValidationMessages = {
  cancelReasonRequired: 'Cancellation requires a reason.',
};

export const getKitchenTicketCancelReasonError = (
  reason: string,
  messages: KitchenTicketValidationMessages = defaultMessages
) => (reason.trim() ? null : messages.cancelReasonRequired);

export const buildKitchenTicketCancelRequest = (reason: string): CancelKitchenTicketRequest => ({
  reason: reason.trim(),
});

export const buildKitchenTicketStatusRequest = (status: KitchenTicketStatus): UpdateKitchenTicketStatusRequest => ({
  status,
});
