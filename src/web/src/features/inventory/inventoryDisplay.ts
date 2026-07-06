export const formatInventoryStock = (value: number) =>
  new Intl.NumberFormat(undefined, { maximumFractionDigits: 2, minimumFractionDigits: value % 1 === 0 ? 0 : 2 }).format(value);

export const formatInventoryDateTime = (value: string | null | undefined) => {
  if (!value) {
    return 'Not recorded';
  }

  const parsed = new Date(value);
  return Number.isNaN(parsed.getTime()) ? value : parsed.toLocaleString();
};

export const getInventoryStatusTone = (status: string) => {
  const normalized = status.trim().toLowerCase();
  if (normalized === 'inactive') {
    return 'neutral' as const;
  }

  if (normalized === 'out of stock') {
    return 'danger' as const;
  }

  if (normalized === 'low stock') {
    return 'warning' as const;
  }

  return 'success' as const;
};

export const formatInventoryMovementType = (movementType: string) => {
  const normalized = movementType.trim().toLowerCase();

  if (normalized === 'adjustmentincrease' || normalized === 'increase') {
    return 'Increase';
  }

  if (normalized === 'adjustmentdecrease' || normalized === 'decrease') {
    return 'Decrease';
  }

  return movementType.replace(/([a-z])([A-Z])/g, '$1 $2');
};
