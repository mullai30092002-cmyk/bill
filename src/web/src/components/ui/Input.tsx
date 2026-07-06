import { forwardRef, useId } from 'react';
import type { InputHTMLAttributes } from 'react';

export interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  helperText?: string;
  error?: string;
}

export const Input = forwardRef<HTMLInputElement, InputProps>(
  ({ id, label, helperText, error, className, ...props }, ref) => {
    const generatedId = useId();
    const inputId = id ?? generatedId;
    const helperId = helperText || error ? `${inputId}-hint` : undefined;

    return (
      <label className="ui-field" htmlFor={inputId}>
        {label ? <span className="ui-field__label">{label}</span> : null}
        <input
          id={inputId}
          ref={ref}
          aria-invalid={Boolean(error)}
          aria-describedby={helperId}
          className={['ui-input', error && 'ui-input--error', className].filter(Boolean).join(' ')}
          {...props}
        />
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

Input.displayName = 'Input';

export default Input;
