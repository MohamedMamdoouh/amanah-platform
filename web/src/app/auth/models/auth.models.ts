export interface UserProfile {
  id: string;
  displayName: string;
  role: string;
  phone: string | null;
  email: string | null;
  requiresAccountReactivation?: boolean;
}

export interface AuthSession {
  accessToken: string;
  user: UserProfile;
}

export type OtpPurpose = 'signup' | 'password_reset';

export type AuthMode = 'signin' | 'signup' | 'forgot';

export type AuthIdentifierChannel = 'phone' | 'email';

export enum VerifyOtpStatus {
  SignupReady = 'signupReady',
  ResetReady = 'resetReady',
}

export interface VerifyOtpResult {
  status: VerifyOtpStatus;
  signupToken?: string;
  resetToken?: string;
}

export interface SendOtpRequest {
  channel: AuthIdentifierChannel;
  identifier: string;
  captchaToken: string;
  purpose: OtpPurpose;
}

export interface VerifyOtpRequest {
  channel: AuthIdentifierChannel;
  identifier: string;
  code: string;
  purpose: OtpPurpose;
}

export interface RegisterRequest {
  signupToken: string;
  displayName: string;
  password: string;
  acceptTerms: boolean;
}

export interface LoginRequest {
  channel: AuthIdentifierChannel;
  identifier: string;
  password: string;
}

export interface ResetPasswordRequest {
  resetToken: string;
  password: string;
}
