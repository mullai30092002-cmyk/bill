import { forwardRef, useId, type SelectHTMLAttributes } from 'react';
import type { ReactNode } from 'react';

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  helperText?: string;
  error?: string;
  children: ReactNode;
}

export const Select = forwardRef<HTMLSelectElement, SelectProps>(
  ({ id, label, helperText, error, className, children, ...props }, ref) => {
    const generatedId = useId();
    const selectId = id ?? generatedId;
    const helperId = helperText || error ? `${selectId}-hint` : undefined;

    return (
      <label className="ui-field" htmlFor={selectId}>
        {label ? <span className="ui-field__label">{label}</span> : null}
        <select
          id={selectId}
          ref={ref}
          aria-invalid={Boolean(error)}
          aria-describedby={helperId}
          className={['ui-select', error && 'ui-select--error', className].filter(Boolean).join(' ')}
          {...props}
        >
          {children}
        </select>
        {error ? (
          <span id={helperId} className="ui-field__helper ui-field__helper--error">
            {error}
          </span>
        ) : helperText ? (
          <span id={helperId} className="ui-field__helper">
            {helperText}
          </span>
        ) : null}
      </label>
    );
  }
);

Select.displayName = 'Select';

export default Select;
