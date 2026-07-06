import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import App from './App';
import { storeAuthSession } from './test/authTestUtils';
import { renderWithRouter } from './test/renderWithRouter';

describe('App smoke', () => {
  it('renders the authenticated BillSoft shell without crashing', () => {
    storeAuthSession();

    renderWithRouter(<App />, '/');

    expect(screen.getByRole('heading', { name: /billsoft dashboard/i })).toBeInTheDocument();
    expect(screen.getByText('BillSoft', { selector: '.brand-logo__name' })).toBeInTheDocument();
  });
});
