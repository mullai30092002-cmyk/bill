import type { ReactNode } from 'react';
import { Navigate, useLocation } from 'react-router-dom';

import { resolveLandingRoute } from '../features/auth/landingRoute';
import { resolveSafeReturnPath } from '../features/auth/loginNavigation';
import { useAuth } from '../features/auth/useAuth';

export interface PublicRouteProps {
  children: ReactNode;
}

export const PublicRoute = ({ children }: PublicRouteProps) => {
  const { isAuthenticated, session } = useAuth();
  const location = useLocation();

  if (isAuthenticated) {
    const returnPath = resolveSafeReturnPath(location.state);
    return <Navigate to={returnPath ?? resolveLandingRoute(session?.roles ?? [], session?.permissions ?? [])} replace />;
  }

  return <>{children}</>;
};

export default PublicRoute;
