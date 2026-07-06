export const brandTokens = {
  palette: {
    primary: '#d97706',
    accent: '#0f766e',
    success: '#15803d',
    warning: '#b45309',
    danger: '#b91c1c',
    info: '#2563eb',
    background: '#f7f2eb',
    surface: '#fffdf9',
    surfaceAlt: '#f5ede2',
    border: '#e8d8c4',
    text: '#1f2937',
    textMuted: '#5b6472',
  },
  radius: {
    sm: '10px',
    md: '14px',
    lg: '20px',
    xl: '28px',
  },
  spacing: {
    xs: '0.25rem',
    sm: '0.5rem',
    md: '0.75rem',
    lg: '1rem',
    xl: '1.5rem',
    '2xl': '2rem',
    '3xl': '3rem',
  },
  shadow: {
    sm: '0 8px 20px rgba(90, 63, 27, 0.08)',
    md: '0 16px 42px rgba(90, 63, 27, 0.14)',
    lg: '0 30px 80px rgba(90, 63, 27, 0.18)',
  },
  touchTarget: {
    min: '44px',
    comfortable: '48px',
    roomy: '56px',
  },
  moduleAccents: {
    dashboard: '#1d4ed8',
    orders: '#d97706',
    inventory: '#0f766e',
    admin: '#475569',
  },
} as const;

export type BrandTokenPalette = typeof brandTokens.palette;
export type BrandModuleTone = keyof typeof brandTokens.moduleAccents;
