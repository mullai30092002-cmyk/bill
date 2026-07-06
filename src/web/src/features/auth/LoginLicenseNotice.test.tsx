import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import App from '../../App';
import { clearAuthSession } from '../../test/authTestUtils';
import { renderWithRouter } from '../../test/renderWithRouter';

describe('Login page – legal notice', () => {
  it('renders Intelsoft copyright notice on the login page', () => {
    clearAuthSession();
    renderWithRouter(<App />, '/login');

    expect(screen.getByText(/© 2026 Intelsoft\. All rights reserved\./i)).toBeInTheDocument();
  });

  it('renders the anti-piracy notice on the login page', () => {
    clearAuthSession();
    renderWithRouter(<App />, '/login');

    expect(
      screen.getByText(/Unauthorized copying or distribution is prohibited\./i)
    ).toBeInTheDocument();
  });
});
