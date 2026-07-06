import { useState, type FormEvent } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';

import { ApiError, isApiError } from '../../api/apiErrors';
import { Button, Checkbox, Input } from '../../components/ui';
import { LanguageSwitcher } from '../../i18n/LanguageSwitcher';
import { useLanguage } from '../../i18n/LanguageProvider';
import { useAuth } from './useAuth';
import { resolveLandingRoute } from './landingRoute';
import { resolveSafeReturnPath } from './loginNavigation';
import './LoginPage.css';

const heroImage =
  'https://images.unsplash.com/photo-1705917893168-e03c7da62577?crop=entropy&cs=tinysrgb&fit=max&fm=jpg&ixid=M3w3Nzg4Nzd8MHwxfHNlYXJjaHw2fHxjaGVmJTIwcGxhdGluZyUyMGRpc2glMjBkYXJrJTIwbW9vZHklMjBraXRjaGVuJTIwY2luZW1hdGljfGVufDF8fHx8MTc4MTE3Mzg4M3ww&ixlib=rb-4.1.0&q=80&w=1920';

const setFieldValidity = (message: string) => (event: FormEvent<HTMLInputElement>) => {
  event.currentTarget.setCustomValidity(message);
};

const clearFieldValidity = (event: FormEvent<HTMLInputElement>) => {
  event.currentTarget.setCustomValidity('');
};

export const LoginPage = () => {
  const { login } = useAuth();
  const { t } = useLanguage();
  const navigate = useNavigate();
  const location = useLocation();
  const [restaurantCode, setRestaurantCode] = useState('');
  const [mobileNumber, setMobileNumber] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [showPassword, setShowPassword] = useState(false);

  const handleSubmit = async (event: FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    setSubmitting(true);
    setError(null);

    try {
      const nextSession = await login({ restaurantCode, mobileNumber, password });
      const returnPath = resolveSafeReturnPath(location.state);
      navigate(returnPath ?? resolveLandingRoute(nextSession.roles, nextSession.permissions), { replace: true });
    } catch (caughtError) {
      setError(t('login.signInFailed'));
      if (caughtError instanceof ApiError || isApiError(caughtError)) {
        return;
      }
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <main className="billsoft-page">
      <section className="billsoft-shell" aria-label={t('login.pageAria')}>
        <div className="hero-panel">
          <div className="hero-top">
            <div className="brand-lockup">
              <span className="brand-badge">BS</span>
              <span className="brand-name">BillSoft</span>
            </div>
          </div>

          <div className="hero-scene" aria-hidden="true">
            <img src={heroImage} alt="" className="hero-image" />
            <div className="hero-overlay" />
            <div className="hero-glow hero-glow-a" />
            <div className="hero-glow hero-glow-b" />
            <div className="hero-edge" />
          </div>

          <div className="hero-copy">
            <p className="eyebrow">{t('login.heroEyebrow')}</p>
            <h1>{t('login.heroTitle')}</h1>
            <p>{t('login.heroSubtitle')}</p>
          </div>
        </div>

        <aside className="auth-panel">
          <div className="language-switcher-row">
            <LanguageSwitcher variant="light" />
          </div>

          <div className="auth-copy">
            <p className="eyebrow eyebrow-dark">{t('login.welcomeBack')}</p>
            <h2>{t('login.title')}</h2>
            <p className="auth-copy__subtitle">{t('login.subtitle')}</p>
          </div>

          <form className="auth-form" onSubmit={handleSubmit}>
            <Input
              id="restaurantCode"
              label={t('login.restaurantCode')}
              helperText={t('login.restaurantCodeHelp')}
              value={restaurantCode}
              onChange={event => setRestaurantCode(event.target.value)}
              onInput={clearFieldValidity}
              onInvalid={setFieldValidity(t('login.restaurantCodeRequired'))}
              autoComplete="organization"
              autoCapitalize="characters"
              autoCorrect="off"
              spellCheck={false}
              placeholder="BILL01"
              required
            />

            <p className="auth-form__note auth-form__note--branch">
              {t('login.branchNote')}
            </p>

            <Input
              id="mobileNumber"
              label={t('login.mobileNumber')}
              helperText={t('login.mobileNumberHelp')}
              value={mobileNumber}
              onChange={event => setMobileNumber(event.target.value)}
              onInput={clearFieldValidity}
              onInvalid={setFieldValidity(t('login.mobileNumberRequired'))}
              autoComplete="username"
              inputMode="tel"
              placeholder="91234567"
              required
            />

            <div className="field">
              <div className="field-head">
                <label htmlFor="password">{t('login.password')}</label>
                <Button type="button" variant="ghost" size="sm" className="auth-inline-action">
                  {t('login.forgotPassword')}
                </Button>
              </div>

              <div className="password-shell">
                <input
                  id="password"
                  className="ui-input password-input"
                  type={showPassword ? 'text' : 'password'}
                  value={password}
                  onChange={event => setPassword(event.target.value)}
                  onInput={clearFieldValidity}
                  onInvalid={setFieldValidity(t('login.passwordRequired'))}
                  autoComplete="current-password"
                  placeholder="••••••••"
                  required
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="sm"
                  className="password-toggle"
                  aria-label={showPassword ? t('login.hidePassword') : t('login.showPassword')}
                  onClick={() => setShowPassword(previous => !previous)}
                  rightIcon={
                    showPassword ? (
                      <svg
                        viewBox="0 0 24 24"
                        aria-hidden="true"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="2"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                      >
                        <path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" />
                        <line x1="1" y1="1" x2="23" y2="23" />
                      </svg>
                    ) : (
                      <svg viewBox="0 0 24 24" aria-hidden="true">
                        <path d="M2.5 12s3.5-6 9.5-6 9.5 6 9.5 6-3.5 6-9.5 6-9.5-6-9.5-6Z" fill="none" stroke="currentColor" />
                        <circle cx="12" cy="12" r="2.5" fill="none" stroke="currentColor" />
                      </svg>
                    )
                  }
                />
              </div>
            </div>

            <Checkbox
              label={t('login.trustDevice')}
              helperText={t('login.trustDeviceHelp')}
            />

            {error ? (
              <div className="auth-form__error" role="alert">
                {error}
              </div>
            ) : null}

            <Button type="submit" fullWidth rightIcon={<ArrowRightIcon />} disabled={submitting} className="auth-submit">
              {submitting ? t('login.signingIn') : t('login.signIn')}
            </Button>
          </form>

          <div className="access-row">
            <p className="access-copy">{t('login.needAccess')}</p>
            <Button
              type="button"
              variant="ghost"
              size="sm"
              className="auth-inline-action auth-inline-action--owner"
            >
              {t('login.ownerRequest')}
            </Button>
          </div>

          <p className="terms">
            {t('login.termsPrefix')}{' '}
            <button type="button" className="inline-link">
              {t('login.termsService')}
            </button>{' '}
            {t('login.termsConjunction')}{' '}
            <button type="button" className="inline-link">
              {t('login.privacyPolicy')}
            </button>
          </p>

          <p className="login-legal-notice">
            {t('software.copyrightNotice')}{' '}
            {t('software.shortAntiPiracyNotice')}
          </p>
        </aside>
      </section>
    </main>
  );
};

const ArrowRightIcon = () => (
  <svg viewBox="0 0 24 24" aria-hidden="true">
    <path d="M5 12h12" fill="none" stroke="currentColor" strokeLinecap="round" />
    <path d="M13 6l6 6-6 6" fill="none" stroke="currentColor" strokeLinecap="round" strokeLinejoin="round" />
  </svg>
);

export default LoginPage;
