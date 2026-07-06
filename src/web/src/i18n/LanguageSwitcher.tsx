import { useLanguage } from './LanguageProvider';
import './LanguageSwitcher.css';

export interface LanguageSwitcherProps {
  variant?: 'chrome' | 'light';
  className?: string;
}

export const LanguageSwitcher = ({ variant = 'light', className }: LanguageSwitcherProps) => {
  const { language, setLanguage, supportedLanguages, t } = useLanguage();

  return (
    <div
      className={['language-switcher', `language-switcher--${variant}`, className].filter(Boolean).join(' ')}
      role="group"
      aria-label={t('language.switcherLabel')}
    >
      {supportedLanguages.map(option => {
        const isActive = option.code === language;
        const label = isActive
          ? t('language.currentLanguage').replace('{language}', option.nativeLabel)
          : option.code === 'en'
            ? t('language.switchToEnglish')
            : t('language.switchToTamil');

        return (
          <button
            key={option.code}
            type="button"
            className={[
              'language-switcher__option',
              isActive && 'language-switcher__option--active',
            ]
              .filter(Boolean)
              .join(' ')}
            aria-pressed={isActive}
            aria-label={label}
            onClick={() => setLanguage(option.code)}
          >
            {option.nativeLabel}
          </button>
        );
      })}
    </div>
  );
};

export default LanguageSwitcher;
