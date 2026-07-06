const normalize = (value: string) => value.trim().toLowerCase();

const hasAny = (values: readonly string[] | undefined, expected: readonly string[]) => {
  const normalizedValues = values?.map(normalize) ?? [];
  return expected.some(entry => normalizedValues.includes(normalize(entry)));
};

const hasRole = (roles: readonly string[] | undefined, expected: string) =>
  hasAny(roles, [expected]);

const hasPermission = (permissions: readonly string[] | undefined, expected: string) =>
  hasAny(permissions, [expected]);

export const resolveLandingRoute = (roles: readonly string[] = [], permissions: readonly string[] = []) => {
  const hasReportView = hasPermission(permissions, 'Report.View');
  const hasBillingView = hasPermission(permissions, 'Billing.View');
  const hasPaymentRecord = hasPermission(permissions, 'Payment.Record');

  if (hasRole(roles, 'RestaurantOwner') || hasRole(roles, 'Admin')) {
    if (hasReportView) {
      return '/owner/dashboard';
    }
  }

  if (hasRole(roles, 'AccountsUser') && hasReportView) {
    return '/reports/daily-cash-sales';
  }

  if (
    hasRole(roles, 'Cashier') &&
    (hasPermission(permissions, 'Order.Create') || hasPermission(permissions, 'Billing.Manage') || hasPaymentRecord)
  ) {
    return '/pos/orders';
  }

  if (
    hasRole(roles, 'KitchenUser') &&
    (hasPermission(permissions, 'KitchenTicket.View') || hasPermission(permissions, 'KitchenTicket.UpdateStatus'))
  ) {
    return '/kitchen/tickets';
  }

  if (
    hasRole(roles, 'Waiter') &&
    (hasPermission(permissions, 'Order.Create') || hasPermission(permissions, 'Order.View'))
  ) {
    return '/pos/orders';
  }

  if (hasBillingView || hasPaymentRecord) {
    return '/billing';
  }

  if (hasReportView) {
    return '/owner/dashboard';
  }

  return '/';
};

