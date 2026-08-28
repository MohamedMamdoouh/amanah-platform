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

export interface VerifyOtpResult {
  status: 'new_user' | 'existing_user';
  signupToken?: string;
  loginToken?: string;
}

export interface SendOtpRequest {
  phone: string;
  captchaToken: string;
}

export interface VerifyOtpRequest {
  phone: string;
  code: string;
}

export interface RegisterRequest {
  signupToken: string;
  displayName: string;
  acceptTerms: boolean;
}

export interface LoginRequest {
  phone: string;
  loginToken: string;
}
