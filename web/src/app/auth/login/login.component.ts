import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnDestroy, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorBody, ApiErrorService } from '../../i18n/api-error.service';
import { AuthService } from '../auth.service';
import { TurnstileWidgetComponent } from '../turnstile-widget/turnstile-widget.component';

type LoginStep = 'phone' | 'otp' | 'register';

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
  private readonly translate = inject(TranslateService);

  private readonly turnstile = viewChild<TurnstileWidgetComponent>('turnstile');

  readonly step = signal<LoginStep>('phone');
  readonly submitting = signal(false);
  readonly summaryError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly captchaToken = signal<string | null>(null);
  readonly resendCooldown = signal(0);

  private phone = '';
  private signupToken: string | null = null;
  private loginToken: string | null = null;
  private cooldownTimer: ReturnType<typeof setInterval> | null = null;

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
    acceptTerms: [false, Validators.requiredTrue],
  });

  stepLabelKey(): string {
    switch (this.step()) {
      case 'phone':
        return 'auth.login.step_phone';
      case 'otp':
        return 'auth.login.step_otp';
      case 'register':
        return 'auth.login.step_profile';
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
        }),
      );

      if (result.status === 'new_user') {
        this.signupToken = result.signupToken ?? null;
        this.submitting.set(false);
        this.step.set('register');
        return;
      }

      this.loginToken = result.loginToken ?? null;
      await this.completeLogin();
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
          acceptTerms: this.registerForm.controls.acceptTerms.value,
        }),
      );
      this.submitting.set(false);
      await this.router.navigate(['/']);
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

  private async completeLogin(): Promise<void> {
    if (!this.loginToken) {
      this.submitting.set(false);
      this.summaryError.set(
        this.translate.instant('error.field.login_token.required'),
      );
      return;
    }

    try {
      await firstValueFrom(
        this.auth.login({
          phone: this.phone,
          loginToken: this.loginToken,
        }),
      );
      this.submitting.set(false);
      await this.router.navigate(['/']);
    } catch (error) {
      this.handleError(error);
    }
  }

  private async sendOtp(): Promise<void> {
    await firstValueFrom(
      this.auth.sendOtp({
        phone: this.phone,
        captchaToken: this.captchaToken()!,
      }),
    );
    this.submitting.set(false);
    this.startResendCooldown(120);
  }

  private handleSendOtpError(error: unknown): void {
    this.handleError(error, () => this.resetCaptchaOnFailure(error));
  }

  private resetCaptchaOnFailure(error: unknown): void {
    if (error instanceof HttpErrorResponse && this.isCaptchaFailure(error)) {
      this.turnstile()?.reset();
      this.captchaToken.set(null);
    }
  }

  private handleError(error: unknown, onHandled?: () => void): void {
    this.submitting.set(false);

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

    onHandled?.();
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

  private isCaptchaFailure(error: HttpErrorResponse): boolean {
    const apiError = this.parseApiError(error);
    return apiError?.code === 'auth.captcha_failed';
  }

  private clearErrors(): void {
    this.summaryError.set(null);
    this.fieldErrors.set({});
  }

  private unexpectedError(): string {
    return this.translate.instant('error.internal.error');
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
