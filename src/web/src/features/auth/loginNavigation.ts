const isInternalPath = (value: string) => value.startsWith('/') && !value.startsWith('//');

const resolvePathFromLocation = (from: unknown) => {
  if (typeof from === 'string') {
    const pathname = from.trim();
    return isInternalPath(pathname) ? pathname : null;
  }

  if (!from || typeof from !== 'object') {
    return null;
  }

  const location = from as { pathname?: unknown; search?: unknown };
  if (typeof location.pathname !== 'string') {
    return null;
  }

  const pathname = location.pathname.trim();
  if (!isInternalPath(pathname)) {
    return null;
  }

  const search = typeof location.search === 'string' ? location.search.trim() : '';
  if (search && !search.startsWith('?')) {
    return null;
  }

  return `${pathname}${search}`;
};

export const resolveSafeReturnPath = (state: unknown) => {
  const locationState = state as { from?: unknown } | null | undefined;
  return resolvePathFromLocation(locationState?.from);
};
