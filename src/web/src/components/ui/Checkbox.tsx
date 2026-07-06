import { forwardRef, useId, type InputHTMLAttributes, type ReactNode } from 'react';

export interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: ReactNode;
  helperText?: ReactNode;
}

export const Checkbox = forwardRef<HTMLInputElement, CheckboxProps>(
  ({ id, label, helperText, className, ...props }, ref) => {
    const generatedId = useId();
    const checkboxId = id ?? generatedId;
    const helperId = helperText ? `${checkboxId}-hint` : undefined;

    return (
      <div className="ui-checkbox-field">
        <label className="ui-checkbox-field__label" htmlFor={checkboxId}>
          <input
            id={checkboxId}
            ref={ref}
            type="checkbox"
            aria-describedby={helperId}
            className={['ui-checkbox', className].filter(Boolean).join(' ')}
            {...props}
          />
          <span className="ui-checkbox__copy">
            <span className="ui-checkbox__label">{label}</span>
          </span>
        </label>
        {helperText ? (
          <span id={helperId} className="ui-field__helper ui-checkbox__helper">
            {helperText}
          </span>
        ) : null}
      </div>
    );
  }
);

Checkbox.displayName = 'Checkbox';

export default Checkbox;
