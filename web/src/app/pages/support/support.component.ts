import { Component, inject, signal, viewChild } from '@angular/core';
import {
  FormControl,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { TurnstileWidgetComponent } from '../../auth/turnstile-widget/turnstile-widget.component';
import { ApiErrorService } from '../../i18n/api-error.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { SupportService } from './support.service';

@Component({
  selector: 'app-support',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    TranslateModule,
    IconComponent,
    FormFieldComponent,
    ButtonComponent,
    AlertComponent,
    TurnstileWidgetComponent,
  ],
  templateUrl: './support.component.html',
  styleUrl: './support.component.scss',
})
export class SupportComponent {
  private readonly support = inject(SupportService);
  private readonly auth = inject(AuthService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);

  private readonly turnstile = viewChild<TurnstileWidgetComponent>('turnstile');

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly showSupportForm = signal(false);
  readonly summaryError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly captchaToken = signal<string | null>(null);

  readonly form = new FormGroup({
    displayName: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(3), Validators.maxLength(40)],
    }),
    replyEmail: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.email],
    }),
    message: new FormControl('', {
      nonNullable: true,
      validators: [Validators.required, Validators.minLength(10), Validators.maxLength(2000)],
    }),
  });

  constructor() {
    const user = this.auth.currentUser();
    if (user?.displayName) {
      this.form.controls.displayName.setValue(user.displayName);
    }

    if (user?.email) {
      this.form.controls.replyEmail.setValue(user.email);
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

  openSupportForm(): void {
    this.summaryError.set(null);
    this.fieldErrors.set({});
    this.showSupportForm.set(true);
  }

  closeSupportForm(): void {
    this.showSupportForm.set(false);
    this.summaryError.set(null);
    this.fieldErrors.set({});
    this.form.reset();
    this.captchaToken.set(null);
    this.turnstile()?.reset();
    const user = this.auth.currentUser();
    if (user?.displayName) {
      this.form.controls.displayName.setValue(user.displayName);
    }
    if (user?.email) {
      this.form.controls.replyEmail.setValue(user.email);
    }
  }

  async submit(): Promise<void> {
    if (this.form.invalid || !this.captchaToken()) {
      this.form.markAllAsTouched();
      return;
    }

    this.summaryError.set(null);
    this.fieldErrors.set({});
    this.submitting.set(true);

    try {
      await firstValueFrom(
        this.support.submitMessage({
          displayName: this.form.controls.displayName.value.trim(),
          replyEmail: this.form.controls.replyEmail.value.trim(),
          message: this.form.controls.message.value.trim(),
          captchaToken: this.captchaToken()!,
        }),
      );
      this.submitted.set(true);
      this.showSupportForm.set(false);
      this.form.reset();
      this.captchaToken.set(null);
      this.turnstile()?.reset();
    } catch (error) {
      this.summaryError.set(this.apiErrors.messageFromHttpError(error));
      this.fieldErrors.set(this.apiErrors.formErrorsFromHttpError(error));
      this.turnstile()?.reset();
    } finally {
      this.submitting.set(false);
    }
  }

  sendAnother(): void {
    this.submitted.set(false);
    this.showSupportForm.set(true);
    this.summaryError.set(null);
    this.fieldErrors.set({});
    this.turnstile()?.reset();
  }
}
