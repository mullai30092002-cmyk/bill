import type { ButtonHTMLAttributes, ReactNode } from 'react';

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: 'primary' | 'secondary' | 'ghost' | 'danger';
  size?: 'sm' | 'md' | 'lg';
  leftIcon?: ReactNode;
  rightIcon?: ReactNode;
  fullWidth?: boolean;
}

const variantClass: Record<NonNullable<ButtonProps['variant']>, string> = {
  primary: 'ui-button--primary',
  secondary: 'ui-button--secondary',
  ghost: 'ui-button--ghost',
  danger: 'ui-button--danger',
};

const sizeClass: Record<NonNullable<ButtonProps['size']>, string> = {
  sm: 'ui-button--sm',
  md: 'ui-button--md',
  lg: 'ui-button--lg',
};

export const Button = ({
  variant = 'primary',
  size = 'md',
  leftIcon,
  rightIcon,
  fullWidth = false,
  className,
  children,
  type = 'button',
  ...props
}: ButtonProps) => (
  <button
    type={type}
    className={[
      'ui-button',
      variantClass[variant],
      sizeClass[size],
      fullWidth && 'ui-button--full',
      className,
    ]
      .filter(Boolean)
      .join(' ')}
    {...props}
  >
    {leftIcon ? <span className="ui-button__icon">{leftIcon}</span> : null}
    {children ? <span className="ui-button__label">{children}</span> : null}
    {rightIcon ? <span className="ui-button__icon">{rightIcon}</span> : null}
  </button>
);

export default Button;
