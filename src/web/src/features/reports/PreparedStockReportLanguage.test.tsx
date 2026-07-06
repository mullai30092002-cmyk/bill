import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

const buildReportResponse = () =>
  createJsonResponse({
    branchId: 'branch-1',
    branchName: 'Main Branch',
    businessDate: '2026-06-13',
    totals: {
      producedQuantity: 0,
      servedQuantity: 0,
      wastedQuantity: 0,
      remainingQuantity: 0,
      itemCount: 0,
      warningCount: 0,
    },
    rows: [],
  });

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('Prepared stock report Tamil chrome', () => {
  it('renders Tamil prepared stock chrome', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Report.View'],
      roles: ['Admin'],
      activeRole: 'Admin',
      branchId: 'branch-1',
    });
    vi.stubGlobal('fetch', vi.fn(async () => buildReportResponse()));

    renderWithRouter(<App />, '/reports/prepared-stock?businessDate=2026-06-13&branchId=branch-1');

    expect((await screen.findAllByRole('heading', { name: /^தயாரான இருப்பு அறிக்கை$/ })).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: /புதுப்பி/i })).toBeInTheDocument();
    expect(screen.getByLabelText(/வணிக தேதி/i)).toBeInTheDocument();
  });
});
