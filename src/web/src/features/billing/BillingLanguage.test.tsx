import { afterEach, describe, expect, it, vi } from 'vitest';
import { screen } from '@testing-library/react';

import App from '../../App';
import { clearAuthSession, createJsonResponse, storeAuthSession } from '../../test/authTestUtils';
import { LANGUAGE_STORAGE_KEY } from '../../i18n/translations';
import { renderWithRouter } from '../../test/renderWithRouter';

describe('Billing language chrome', () => {
  afterEach(() => {
    clearAuthSession();
    localStorage.removeItem(LANGUAGE_STORAGE_KEY);
    vi.unstubAllGlobals();
  });

  it('renders Tamil billing chrome when the Tamil locale is selected', async () => {
    localStorage.setItem(LANGUAGE_STORAGE_KEY, 'ta');
    storeAuthSession({
      permissions: ['Billing.View'],
      roles: ['Cashier'],
      activeRole: 'Cashier',
    });

    vi.stubGlobal(
      'fetch',
      vi.fn(async () => createJsonResponse({ items: [] }))
    );

    renderWithRouter(<App />, '/billing');

    expect(await screen.findByRole('heading', { name: /பில்லிங்/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /புதுப்பி/i })).toBeInTheDocument();
  });
});
