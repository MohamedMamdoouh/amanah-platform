import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnDestroy, signal, viewChild } from '@angular/core';
import {
  AbstractControl,
  FormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { Router, RouterLink, ActivatedRoute } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorBody, ApiErrorService } from '../../i18n/api-error.service';
import { AuthService } from '../auth.service';
import { AuthMode, OtpPurpose } from '../models/auth.models';
import { TurnstileWidgetComponent } from '../turnstile-widget/turnstile-widget.component';

type AuthStep = 'phone' | 'otp' | 'register' | 'reset';

const PASSWORD_MIN_LENGTH = 8;

function passwordsMatch(control: AbstractControl): ValidationErrors | null {
  const password = control.parent?.get('password')?.value;
  const confirmPassword = control.value;
  if (!password || !confirmPassword) {
    return null;
  }

  return password === confirmPassword ? null : { passwordMismatch: true };
}

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    RouterLink,
    TranslateModule,
    TurnstileWidgetComponent,
  ],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss',
})
export class LoginComponent implements OnDestroy {
  private readonly auth = inject(AuthService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly fb = inject(FormBuilder);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);
  private readonly translate = inject(TranslateService);

  private readonly turnstile = viewChild<TurnstileWidgetComponent>('turnstile');

  readonly mode = signal<AuthMode>('signin');
  readonly step = signal<AuthStep>('phone');
  readonly submitting = signal(false);
  readonly summaryError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly captchaToken = signal<string | null>(null);
  readonly resendCooldown = signal(0);

  private phone = '';
  private signupToken: string | null = null;
  private resetToken: string | null = null;
  private cooldownTimer: ReturnType<typeof setInterval> | null = null;

  readonly signInForm = this.fb.nonNullable.group({
    phone: ['', [Validators.required, Validators.minLength(10)]],
    password: ['', [Validators.required]],
  });

  readonly phoneForm = this.fb.nonNullable.group({
    phone: ['', [Validators.required, Validators.minLength(10)]],
  });

  readonly otpForm = this.fb.nonNullable.group({
    code: [
      '',
      [Validators.required, Validators.pattern(/^[0-9\u0660-\u0669]{6}$/)],
    ],
  });

  readonly registerForm = this.fb.nonNullable.group({
    displayName: ['', [Validators.required, Validators.minLength(3)]],
    password: [
      '',
      [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH)],
    ],
    confirmPassword: ['', [Validators.required, passwordsMatch]],
    acceptTerms: [false, Validators.requiredTrue],
  });

  readonly resetForm = this.fb.nonNullable.group({
    password: [
      '',
      [Validators.required, Validators.minLength(PASSWORD_MIN_LENGTH)],
    ],
    confirmPassword: ['', [Validators.required, passwordsMatch]],
  });

  constructor() {
    this.registerForm.controls.password.valueChanges.subscribe(() => {
      this.registerForm.controls.confirmPassword.updateValueAndValidity();
    });
    this.resetForm.controls.password.valueChanges.subscribe(() => {
      this.resetForm.controls.confirmPassword.updateValueAndValidity();
    });
  }

  setMode(mode: AuthMode): void {
    this.mode.set(mode);
    this.step.set('phone');
    this.clearErrors();
    this.phone = '';
    this.signupToken = null;
    this.resetToken = null;
    this.captchaToken.set(null);
    this.turnstile()?.reset();
  }

  stepLabelKey(): string {
    if (this.mode() === 'signin') {
      return 'auth.login.step_signin';
    }

    switch (this.step()) {
      case 'phone':
        return 'auth.login.step_phone';
      case 'otp':
        return 'auth.login.step_otp';
      case 'register':
        return 'auth.login.step_profile';
      case 'reset':
        return 'auth.login.step_reset';
    }
  }

  titleKey(): string {
    switch (this.mode()) {
      case 'signin':
        return 'auth.login.title_signin';
      case 'signup':
        return 'auth.login.title_signup';
      case 'forgot':
        return 'auth.login.title_forgot';
    }
  }

  fieldError(name: string): string | null {
    const errors = this.fieldErrors()[name];
    return errors?.[0] ?? null;
  }

  onCaptchaToken(token: string): void {
    this.captchaToken.set(token);
  }

  onCaptchaExpired(): void {
    this.captchaToken.set(null);
  }

  onCaptchaErrored(): void {
    this.captchaToken.set(null);
    this.summaryError.set(this.translate.instant('error.auth.captcha_failed'));
  }

  async submitSignIn(): Promise<void> {
    if (this.signInForm.invalid) {
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    try {
      await firstValueFrom(
        this.auth.login({
          phone: this.signInForm.controls.phone.value.trim(),
          password: this.signInForm.controls.password.value,
        }),
      );
      this.submitting.set(false);
      await this.navigateAfterAuth();
    } catch (error) {
      this.handleError(error);
    }
  }

  async submitPhone(): Promise<void> {
    if (this.phoneForm.invalid || !this.captchaToken()) {
      return;
    }

    this.clearErrors();
    this.submitting.set(true);
    this.phone = this.phoneForm.controls.phone.value.trim();

    try {
      await this.sendOtp();
      this.step.set('otp');
    } catch (error) {
      this.handleSendOtpError(error);
    }
  }

  async submitOtp(): Promise<void> {
    if (this.otpForm.invalid) {
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    try {
      const result = await firstValueFrom(
        this.auth.verifyOtp({
          phone: this.phone,
          code: this.otpForm.controls.code.value.trim(),
          purpose: this.otpPurpose(),
        }),
      );

      if (result.status === 'signup_ready') {
        this.signupToken = result.signupToken ?? null;
        this.submitting.set(false);
        this.step.set('register');
        return;
      }

      this.resetToken = result.resetToken ?? null;
      this.submitting.set(false);
      this.step.set('reset');
    } catch (error) {
      this.handleError(error);
    }
  }

  async resendOtp(): Promise<void> {
    if (this.resendCooldown() > 0 || !this.captchaToken()) {
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    try {
      await this.sendOtp();
    } catch (error) {
      this.handleSendOtpError(error);
    }
  }

  async submitRegister(): Promise<void> {
    if (this.registerForm.invalid || !this.signupToken) {
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    try {
      await firstValueFrom(
        this.auth.register({
          signupToken: this.signupToken,
          displayName: this.registerForm.controls.displayName.value.trim(),
          password: this.registerForm.controls.password.value,
          acceptTerms: this.registerForm.controls.acceptTerms.value,
        }),
      );
      this.submitting.set(false);
      await this.navigateAfterAuth();
    } catch (error) {
      this.handleError(error);
    }
  }

  async submitReset(): Promise<void> {
    if (this.resetForm.invalid || !this.resetToken) {
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    try {
      await firstValueFrom(
        this.auth.resetPassword({
          resetToken: this.resetToken,
          password: this.resetForm.controls.password.value,
        }),
      );
      this.submitting.set(false);
      await this.navigateAfterAuth();
    } catch (error) {
      this.handleError(error);
    }
  }

  ngOnDestroy(): void {
    if (this.cooldownTimer) {
      clearInterval(this.cooldownTimer);
      this.cooldownTimer = null;
    }
  }

  private otpPurpose(): OtpPurpose {
    return this.mode() === 'forgot' ? 'password_reset' : 'signup';
  }

  private async sendOtp(): Promise<void> {
    await firstValueFrom(
      this.auth.sendOtp({
        phone: this.phone,
        captchaToken: this.captchaToken()!,
        purpose: this.otpPurpose(),
      }),
    );
    this.submitting.set(false);
    this.startResendCooldown(120);
    this.refreshCaptcha();
  }

  private handleSendOtpError(error: unknown): void {
    this.handleError(error, () => this.refreshCaptcha());
  }

  private refreshCaptcha(): void {
    this.captchaToken.set(null);
    this.turnstile()?.reset();
  }

  private handleError(error: unknown, onHandled?: () => void): void {
    this.submitting.set(false);

    try {
      if (!(error instanceof HttpErrorResponse)) {
        this.summaryError.set(this.unexpectedError());
        return;
      }

      const apiError = this.parseApiError(error);
      if (!apiError) {
        this.summaryError.set(this.unexpectedError());
        return;
      }

      this.summaryError.set(this.apiErrors.summary(apiError));
      this.fieldErrors.set(this.apiErrors.fieldErrors(apiError));

      const retryAfter = error.headers.get('Retry-After');
      if (error.status === 429 && retryAfter) {
        const seconds = Number.parseInt(retryAfter, 10);
        if (!Number.isNaN(seconds)) {
          this.startResendCooldown(seconds);
        }
      }
    } finally {
      onHandled?.();
    }
  }

  private parseApiError(error: HttpErrorResponse): ApiErrorBody | null {
    if (
      error.error &&
      typeof error.error === 'object' &&
      'code' in error.error &&
      'message' in error.error
    ) {
      return error.error as ApiErrorBody;
    }

    return null;
  }

  private clearErrors(): void {
    this.summaryError.set(null);
    this.fieldErrors.set({});
  }

  private unexpectedError(): string {
    return this.translate.instant('error.internal.error');
  }

  private async navigateAfterAuth(): Promise<void> {
    const returnUrl = this.route.snapshot.queryParamMap.get('returnUrl');
    if (returnUrl && returnUrl.startsWith('/') && !returnUrl.startsWith('//')) {
      await this.router.navigateByUrl(returnUrl);
      return;
    }

    await this.router.navigate(['/']);
  }

  private startResendCooldown(seconds: number): void {
    if (this.cooldownTimer) {
      clearInterval(this.cooldownTimer);
    }

    this.resendCooldown.set(seconds);
    this.cooldownTimer = setInterval(() => {
      const next = this.resendCooldown() - 1;
      if (next <= 0) {
        this.resendCooldown.set(0);
        if (this.cooldownTimer) {
          clearInterval(this.cooldownTimer);
          this.cooldownTimer = null;
        }
        return;
      }

      this.resendCooldown.set(next);
    }, 1000);
  }
}
