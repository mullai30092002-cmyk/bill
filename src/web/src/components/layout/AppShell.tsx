import { useState, type ReactNode } from 'react';
import { billsoftBrandConfig, getBranchDisplayName, getRestaurantDisplayName } from '../../brand/brandConfig';
import { BrandHeader } from '../brand/BrandHeader';
import { Badge } from '../ui/Badge';
import { Button } from '../ui/Button';
import { LanguageSwitcher } from '../../i18n/LanguageSwitcher';
import { useLanguage } from '../../i18n/LanguageProvider';
import { ResponsiveNav } from './ResponsiveNav';
import { shellNavItems, type ShellNavItem } from './navigation';
import { AboutDialog } from './AboutDialog';

export interface AppShellProps {
  children: ReactNode;
  tone?: 'dashboard' | 'orders' | 'inventory' | 'admin';
  navItems?: ShellNavItem[];
  restaurantName?: string;
  branchName?: string;
  operatorLabel?: string;
}

export const AppShell = ({
  children,
  tone = 'dashboard',
  navItems = shellNavItems,
  restaurantName,
  branchName,
  operatorLabel = billsoftBrandConfig.previewLabel,
}: AppShellProps) => {
  const { t } = useLanguage();
  const [aboutOpen, setAboutOpen] = useState(false);

  return (
    <div className="app-shell" data-tone={tone}>
      <div className="app-shell__halo app-shell__halo--one" aria-hidden="true" />
      <div className="app-shell__halo app-shell__halo--two" aria-hidden="true" />
      <div className="app-shell__frame">
        <header className="app-shell__topbar">
          <BrandHeader
            restaurantName={getRestaurantDisplayName(restaurantName)}
            branchName={getBranchDisplayName(branchName)}
            operatorLabel={operatorLabel}
          />
          <div className="app-shell__actions">
            <div className="app-shell__status-row">
              <Badge tone="neutral" label={t('shell.touchFriendlyShell')} />
              <Badge tone="info" label={t('shell.deviceCoverage')} />
            </div>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              onClick={() => setAboutOpen(true)}
              aria-label={t('software.aboutTitle')}
              className="app-shell__about-btn"
            >
              {t('software.aboutTitle')}
            </Button>
            <LanguageSwitcher variant="chrome" />
          </div>
        </header>

        <div className="app-shell__workspace">
          <ResponsiveNav items={navItems} />
          <main className="app-shell__content">{children}</main>
        </div>
      </div>

      <AboutDialog open={aboutOpen} onClose={() => setAboutOpen(false)} />
    </div>
  );
};

export default AppShell;
