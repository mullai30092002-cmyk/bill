import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';

import { LanguageProvider, useLanguage } from './LanguageProvider';
import { LanguageSwitcher } from './LanguageSwitcher';
import { LANGUAGE_STORAGE_KEY } from './translations';

const LanguageProbe = () => {
  const { language, t } = useLanguage();

  return (
    <div>
      <span data-testid="active-language">{language}</span>
      <span>{t('login.signIn')}</span>
      <LanguageSwitcher />
    </div>
  );
};

describe('LanguageProvider', () => {
  it('defaults to English and switches to Tamil with persistence', async () => {
    const user = userEvent.setup();

    render(
      <LanguageProvider>
        <LanguageProbe />
      </LanguageProvider>
    );

    expect(screen.getByTestId('active-language')).toHaveTextContent('en');
    expect(screen.getByText('Sign in')).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /switch to tamil/i }));

    expect(screen.getByTestId('active-language')).toHaveTextContent('ta');
    expect(screen.getByText('உள்நுழை')).toBeInTheDocument();
    expect(localStorage.getItem(LANGUAGE_STORAGE_KEY)).toBe('ta');
    await waitFor(() => expect(document.documentElement).toHaveAttribute('lang', 'ta'));
  });
});
