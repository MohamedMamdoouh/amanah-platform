export interface UserProfile {
  id: string;
  displayName: string;
  role: string;
  phone: string;
}

export interface AuthSession {
  accessToken: string;
  user: UserProfile;
}

export type OtpPurpose = 'signup' | 'password_reset';

export type AuthMode = 'signin' | 'signup' | 'forgot';

export interface VerifyOtpResult {
  status: 'signup_ready' | 'reset_ready';
  signupToken?: string;
  resetToken?: string;
}

export interface SendOtpRequest {
  phone: string;
  captchaToken: string;
  purpose: OtpPurpose;
}

export interface VerifyOtpRequest {
  phone: string;
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
  phone: string;
  password: string;
}

export interface ResetPasswordRequest {
  resetToken: string;
  password: string;
}
