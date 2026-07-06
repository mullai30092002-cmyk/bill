import { requestJson, requestVoid } from '../../api/apiClient';
import type { SetupBusinessType, SetupChecklistResponse } from './setupChecklistTypes';

const buildQueryString = (branchId?: string | null) => {
  if (!branchId) {
    return '';
  }

  const query = new URLSearchParams();
  query.set('branchId', branchId);
  const queryString = query.toString();
  return queryString ? `?${queryString}` : '';
};

export const getSetupChecklist = (branchId?: string | null) =>
  requestJson<SetupChecklistResponse>(`/api/v1/setup/checklist${buildQueryString(branchId)}`);

export const updateSetupBusinessType = (businessType: SetupBusinessType) =>
  requestVoid('/api/v1/setup/business-type', {
    method: 'PUT',
    body: { businessType },
  });
