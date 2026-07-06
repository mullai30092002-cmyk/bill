import { requestJson } from '../../api/apiClient';
import type { BillReceiptResponse, RecordBillReceiptPrintEventRequest } from './billReceiptTypes';

export const getBillReceipt = (billId: string) =>
  requestJson<BillReceiptResponse>(`/api/v1/billing/bills/${billId}/receipt`);

export const recordBillReceiptPrintEvent = (billId: string, request?: RecordBillReceiptPrintEventRequest) =>
  requestJson<BillReceiptResponse>(`/api/v1/billing/bills/${billId}/receipt/print-events`, {
    method: 'POST',
    body: request,
  });
