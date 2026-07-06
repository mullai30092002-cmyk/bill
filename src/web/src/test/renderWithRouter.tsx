import type { ReactElement, ReactNode } from 'react';
import { MemoryRouter } from 'react-router-dom';
import { render } from '@testing-library/react';

import { LanguageProvider } from '../i18n/LanguageProvider';

export type RouterInitialEntry =
  | string
  | {
      pathname?: string;
      search?: string;
      hash?: string;
      state?: unknown;
      key?: string;
    };

export const renderWithRouter = (ui: ReactElement, initialPath: RouterInitialEntry = '/') =>
  render(
    <MemoryRouter
      initialEntries={[initialPath]}
      future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
    >
      <LanguageProvider>{ui}</LanguageProvider>
    </MemoryRouter>
  );

export const renderWithMemoryRouter = (children: ReactNode, initialPath = '/') =>
  render(
    <MemoryRouter
      initialEntries={[initialPath]}
      future={{ v7_startTransition: true, v7_relativeSplatPath: true }}
    >
      <LanguageProvider>{children}</LanguageProvider>
    </MemoryRouter>
  );
