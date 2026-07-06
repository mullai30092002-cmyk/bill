import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import { LanguageProvider } from '../../i18n/LanguageProvider';
import { AboutDialog } from './AboutDialog';

const renderAboutDialog = (open: boolean, onClose = vi.fn()) =>
  render(
    <LanguageProvider>
      <AboutDialog open={open} onClose={onClose} />
    </LanguageProvider>
  );

describe('AboutDialog', () => {
  it('renders nothing when closed', () => {
    renderAboutDialog(false);
    expect(screen.queryByRole('dialog')).not.toBeInTheDocument();
  });

  it('renders About BillSoft title when open', () => {
    renderAboutDialog(true);
    expect(screen.getByRole('dialog', { name: /about billsoft/i })).toBeInTheDocument();
  });

  it('shows INTELSOFT PTE. LTD.', () => {
    renderAboutDialog(true);
    expect(screen.getByText('INTELSOFT PTE. LTD.')).toBeInTheDocument();
  });

  it('shows company registration number 202302812D', () => {
    renderAboutDialog(true);
    expect(screen.getByText('202302812D')).toBeInTheDocument();
  });

  it('shows registered office as Confirm from ACRA BizFile', () => {
    renderAboutDialog(true);
    expect(screen.getByText('Confirm from ACRA BizFile')).toBeInTheDocument();
  });

  it('shows support email sales@intelsoft.live', () => {
    renderAboutDialog(true);
    expect(screen.getByText('sales@intelsoft.live')).toBeInTheDocument();
  });

  it('shows website URL', () => {
    renderAboutDialog(true);
    expect(screen.getByText('https://www.intelsoft.sg')).toBeInTheDocument();
  });

  it('calls onClose when close button is clicked', async () => {
    const onClose = vi.fn();
    renderAboutDialog(true, onClose);

    const user = userEvent.setup();
    await user.click(screen.getByRole('button', { name: /close/i }));

    expect(onClose).toHaveBeenCalledTimes(1);
  });

  it('calls onClose when backdrop is clicked', async () => {
    const onClose = vi.fn();
    renderAboutDialog(true, onClose);

    const user = userEvent.setup();
    await user.click(screen.getByRole('presentation'));

    expect(onClose).toHaveBeenCalledTimes(1);
  });
});
