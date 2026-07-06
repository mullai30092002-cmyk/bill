import { NavLink } from 'react-router-dom';
import { useLanguage } from '../../i18n/LanguageProvider';
import type { ShellNavItem } from './navigation';
import { NavIcon } from './NavIcon';

export interface ResponsiveNavProps {
  items: ShellNavItem[];
}

const navLinkClass = ({ isActive }: { isActive: boolean }) =>
  ['responsive-nav__link', isActive && 'responsive-nav__link--active'].filter(Boolean).join(' ');

export const ResponsiveNav = ({ items }: ResponsiveNavProps) => {
  const { t } = useLanguage();
  const primaryNavigationLabel = t('nav.primaryNavigationAria');

  return (
    <div className="responsive-nav-shell">
      <aside className="responsive-nav responsive-nav--desktop" aria-label={primaryNavigationLabel}>
        <div className="responsive-nav__section-title">{t('nav.workspaces')}</div>
        <nav className="responsive-nav__stack">
          {items.map(item => (
            <NavLink key={item.to} to={item.to} className={navLinkClass} end={item.to === '/'}>
              <span className="responsive-nav__link-icon"><NavIcon name={item.icon} /></span>
              <span className="responsive-nav__link-text">
                <span className="responsive-nav__link-label">{item.label}</span>
                <span className="responsive-nav__link-hint">{item.hint}</span>
              </span>
            </NavLink>
          ))}
        </nav>
      </aside>

      <details className="responsive-nav responsive-nav--tablet">
        <summary className="responsive-nav__summary">{t('nav.menu')}</summary>
        <nav className="responsive-nav__panel" aria-label={primaryNavigationLabel}>
          {items.map(item => (
            <NavLink key={item.to} to={item.to} className={navLinkClass} end={item.to === '/'}>
              <span className="responsive-nav__link-icon"><NavIcon name={item.icon} /></span>
              <span className="responsive-nav__link-text">
                <span className="responsive-nav__link-label">{item.label}</span>
                <span className="responsive-nav__link-hint">{item.hint}</span>
              </span>
            </NavLink>
          ))}
        </nav>
      </details>

      <nav className="responsive-nav responsive-nav--mobile" aria-label={primaryNavigationLabel}>
        {items.map(item => (
          <NavLink key={item.to} to={item.to} className={navLinkClass} end={item.to === '/'}>
            <span className="responsive-nav__link-icon"><NavIcon name={item.icon} /></span>
            <span className="responsive-nav__link-text">
              <span className="responsive-nav__link-label">{item.label}</span>
            </span>
          </NavLink>
        ))}
      </nav>
    </div>
  );
};

export default ResponsiveNav;
