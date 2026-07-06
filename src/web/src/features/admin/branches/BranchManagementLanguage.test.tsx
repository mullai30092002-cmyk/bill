import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../../i18n/translations';
import { renderWithRouter } from '../../../test/renderWithRouter';

const adminBranchesPath = '/api/v1/admin/branches';

const makeBranchList = () =>
  createJsonResponse({
    items: [
      {
        branchId: 'branch-1',
        restaurantId: 'restaurant-1',
        name: 'Main Branch',
        address: '123 Market Street',
        phone: '60000001',
        timezone: 'Asia/Singapore',
        currency: 'INR',
        status: 'Active',
        createdAt: '2026-06-11T09:00:00Z',
        updatedAt: '2026-06-11T09:30:00Z',
      },
    ],
  });

const setupFetch = (responses: Record<string, Response[]>) => {
  const fetchMock = vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const method = (init?.method ?? 'GET').toUpperCase();
    const path = new URL(String(input)).pathname;
    const key = `${method} ${path}`;
    const queue = responses[key];
    if (!queue || queue.length === 0) {
      throw new Error(`Unhandled request: ${key}`);
    }
    return queue.shift()!;
  });
  vi.stubGlobal('fetch', fetchMock);
  return fetchMock;
};

afterEach(() => {
  clearAuthSession();
  localStorage.removeItem(LANGUAGE_STORAGE_KEY);
  vi.unstubAllGlobals();
});

describe('BranchManagement Tamil chrome', () => {
  it('renders Tamil UI chrome for the branch management page', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Branch.Manage'],
      roles: ['Admin'],
      activeRole: 'Admin',
    });

    setupFetch({
      [`GET ${adminBranchesPath}`]: [makeBranchList(), makeBranchList(), makeBranchList()],
    });

    renderWithRouter(<App />, '/admin/branches');

    expect(await screen.findByRole('heading', { name: /கிளை நிர்வாகம்/i })).toBeInTheDocument();
    expect((await screen.findAllByText('Main Branch')).length).toBeGreaterThan(0);
    expect(screen.getAllByRole('button', { name: /திருத்து/i }).length).toBeGreaterThan(0);
  });
});
