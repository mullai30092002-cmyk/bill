export interface LoginRequest {
  restaurantCode: string;
  mobileNumber: string;
  password: string;
}

export interface AuthSession {
  accessToken: string;
  refreshToken: string;
  accessTokenExpiresAtUtc: string;
  refreshTokenExpiresAtUtc: string;
  userId: string;
  restaurantId: string;
  restaurantCode: string;
  countryCode?: string;
  currencyCode?: string;
  timeZoneId?: string;
  branchId: string | null;
  fullName: string;
  mobileNumber: string;
  roles: string[];
  permissions: string[];
  activeRole: string;
}

export interface AuthUserContext {
  userId: string;
  restaurantId: string;
  restaurantCode: string;
  branchId: string | null;
  fullName: string;
  mobileNumber: string;
  roles: string[];
  permissions: string[];
  activeRole: string;
}
