import type { ChangeEventHandler } from 'react';

import { Select } from '../../../components/ui';
import type { BranchSelectOption } from './adminUserDisplay';

interface BranchSelectProps {
  label: string;
  value: string;
  options: BranchSelectOption[];
  onChange: ChangeEventHandler<HTMLSelectElement>;
  helperText?: string;
  error?: string;
  disabled?: boolean;
}

export const BranchSelect = ({
  label,
  value,
  options,
  onChange,
  helperText,
  error,
  disabled,
}: BranchSelectProps) => (
  <Select label={label} value={value} onChange={onChange} helperText={helperText} error={error} disabled={disabled}>
    {options.map(option => (
      <option key={option.value || 'no-branch'} value={option.value} disabled={option.disabled}>
        {option.label}
      </option>
    ))}
  </Select>
);

export default BranchSelect;
