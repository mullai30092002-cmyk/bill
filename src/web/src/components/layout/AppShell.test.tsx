import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';

import { AppShell } from './AppShell';
import { renderWithMemoryRouter } from '../../test/renderWithRouter';

describe('AppShell', () => {
  it('renders the BillSoft shell chrome and child content', () => {
    renderWithMemoryRouter(
      <AppShell>
        <section>Smoke child</section>
      </AppShell>
    );

    expect(screen.getByText('BillSoft', { selector: '.brand-logo__name' })).toBeInTheDocument();
    expect(screen.getByText('Sample Restaurant')).toBeInTheDocument();
    expect(screen.getByText('Smoke child')).toBeInTheDocument();
    expect(screen.getByText('Touch-friendly shell')).toBeInTheDocument();
    expect(screen.getByText('Mobile, tablet, desktop')).toBeInTheDocument();
    expect(screen.getAllByText('Dashboard', { selector: '.responsive-nav__link-label' })).toHaveLength(3);
    expect(screen.getAllByText('Orders preview', { selector: '.responsive-nav__link-label' })).toHaveLength(3);
    expect(screen.getAllByText('Orders', { selector: '.responsive-nav__link-label' })).toHaveLength(3);
    expect(screen.getAllByText('Kitchen Display', { selector: '.responsive-nav__link-label' })).toHaveLength(3);
  });
});
