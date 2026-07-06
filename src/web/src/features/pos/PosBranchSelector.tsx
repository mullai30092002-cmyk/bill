import { Select } from '../../components/ui';
import { useLanguage } from '../../i18n/LanguageProvider';

export interface PosBranchSelectorOption {
  value: string;
  label: string;
  disabled?: boolean;
}

export interface PosBranchSelectorProps {
  options: PosBranchSelectorOption[];
  value: string;
  canEdit: boolean;
  error?: string;
  helperText?: string;
  onChange: (value: string) => void;
}

export const PosBranchSelector = ({
  options,
  value,
  canEdit,
  error,
  helperText,
  onChange,
}: PosBranchSelectorProps) => {
  const { t } = useLanguage();

  return (
    <Select
      label={t('pos.branch')}
      value={value}
      disabled={!canEdit}
      error={error}
      helperText={helperText ?? t('pos.branchSelectorHelp')}
      onChange={event => onChange(event.target.value)}
    >
      <option value="">{canEdit ? t('pos.selectBranch') : t('pos.noBranchSelected')}</option>
      {options.map(option => (
        <option key={option.value} value={option.value} disabled={option.disabled}>
          {option.label}
        </option>
      ))}
    </Select>
  );
};

export default PosBranchSelector;
