import { useLanguage } from '../../i18n/LanguageProvider';
import { softwareOwnership } from '../../brand/softwareOwnership';
import { Button } from '../ui/Button';
import './AboutDialog.css';

interface AboutDialogProps {
  open: boolean;
  onClose: () => void;
}

export const AboutDialog = ({ open, onClose }: AboutDialogProps) => {
  const { t } = useLanguage();

  if (!open) return null;

  return (
    <div className="about-dialog-backdrop" role="presentation" onClick={onClose}>
      <div
        className="about-dialog"
        role="dialog"
        aria-modal="true"
        aria-label={t('software.aboutTitle')}
        onClick={event => event.stopPropagation()}
      >
        <div className="about-dialog__header">
          <h2 className="about-dialog__title">{t('software.aboutTitle')}</h2>
          <Button type="button" variant="ghost" size="sm" onClick={onClose} aria-label={t('software.close')}>
            ✕
          </Button>
        </div>

        <div className="about-dialog__body">
          <section className="about-dialog__section">
            <h3 className="about-dialog__section-title">{t('software.ownershipTitle')}</h3>
            <dl className="about-dialog__list">
              <div className="about-dialog__row">
                <dt>{t('software.softwareLabel')}</dt>
                <dd>{softwareOwnership.softwareName}</dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.companyLabel')}</dt>
                <dd>{softwareOwnership.companyDisplayName}</dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.legalEntityLabel')}</dt>
                <dd>{softwareOwnership.companyLegalName}</dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.registrationNumberLabel')}</dt>
                <dd>{softwareOwnership.companyRegistrationNumber}</dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.companyContactAddressLabel')}</dt>
                <dd>{softwareOwnership.companyContactAddress}</dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.registeredOfficeLabel')}</dt>
                <dd>{softwareOwnership.registeredOfficeAddress}</dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.supportLabel')}</dt>
                <dd>
                  <a href={`mailto:${softwareOwnership.supportEmail}`} className="about-dialog__link">
                    {softwareOwnership.supportEmail}
                  </a>
                </dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.websiteLabel')}</dt>
                <dd>
                  <a
                    href={softwareOwnership.websiteUrl}
                    className="about-dialog__link"
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    {softwareOwnership.websiteUrl}
                  </a>
                </dd>
              </div>
              <div className="about-dialog__row">
                <dt>{t('software.phoneLabel')}</dt>
                <dd>{softwareOwnership.phone}</dd>
              </div>
            </dl>
          </section>

          <section className="about-dialog__section">
            <h3 className="about-dialog__section-title">{t('software.licenseNoticeLabel')}</h3>
            <p className="about-dialog__notice">{t('software.fullLicenseNotice')}</p>
          </section>

          <section className="about-dialog__section">
            <h3 className="about-dialog__section-title">{t('software.antiPiracyNoticeLabel')}</h3>
            <p className="about-dialog__notice">{t('software.shortAntiPiracyNotice')}</p>
          </section>

          <p className="about-dialog__copyright">{t('software.copyrightNotice')}</p>
        </div>
      </div>
    </div>
  );
};

export default AboutDialog;
