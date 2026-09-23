import { Component, computed, inject, OnInit, signal, viewChild } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { AuthIdentifierChannel } from '../../auth/models/auth.models';
import { TurnstileWidgetComponent } from '../../auth/turnstile-widget/turnstile-widget.component';
import { ApiErrorService } from '../../i18n/api-error.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import {
  AccountDeactivationStatus,
  AccountIdentifiers,
  AccountService,
} from '../account.service';

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    AlertComponent,
    ButtonComponent,
    ConfirmDialogComponent,
    FormFieldComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    TurnstileWidgetComponent,
    TranslateModule,
  ],
  templateUrl: './account.component.html',
  styleUrl: './account.component.scss',
})
export class AccountComponent implements OnInit {
  private readonly accountService = inject(AccountService);
  private readonly auth = inject(AuthService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);

  private readonly linkTurnstile = viewChild<TurnstileWidgetComponent>('linkTurnstile');

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly status = signal<AccountDeactivationStatus | null>(null);
  readonly identifiers = signal<AccountIdentifiers | null>(null);
  readonly showConfirmDialog = signal(false);
  readonly deactivating = signal(false);
  readonly deactivateError = signal<string | null>(null);
  readonly loggingOutEverywhere = signal(false);
  readonly logoutEverywhereError = signal<string | null>(null);
  readonly linkStep = signal<'idle' | 'otp'>('idle');
  readonly linkSubmitting = signal(false);
  readonly linkError = signal<string | null>(null);
  readonly linkCaptchaToken = signal<string | null>(null);

  readonly linkForm = this.fb.nonNullable.group({
    identifier: ['', [Validators.required, Validators.minLength(3)]],
  });

  readonly otpForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.pattern(/^[0-9\u0660-\u0669]{6}$/)]],
  });

  readonly translatedBlockers = computed(() => {
    const blockers = this.status()?.blockers ?? [];
    return blockers.map((blocker) => this.translateBlocker(blocker));
  });

  readonly canLinkIdentifier = computed(() => {
    const ids = this.identifiers();
    return ids !== null && (!ids.phone || !ids.email);
  });

  readonly linkChannel = computed((): AuthIdentifierChannel | null => {
    const ids = this.identifiers();
    if (!ids || (ids.phone && ids.email)) {
      return null;
    }

    return ids.email ? 'phone' : 'email';
  });

  ngOnInit(): void {
    void this.loadPage();
  }

  onLinkCaptchaToken(token: string): void {
    this.linkCaptchaToken.set(token);
  }

  onLinkCaptchaExpired(): void {
    this.linkCaptchaToken.set(null);
  }

  async submitLinkSend(): Promise<void> {
    const channel = this.linkChannel();
    if (
      !channel
      || this.linkForm.invalid
      || !this.linkCaptchaToken()
      || this.linkSubmitting()
    ) {
      return;
    }

    this.linkSubmitting.set(true);
    this.linkError.set(null);

    try {
      await firstValueFrom(
        this.accountService.sendLinkIdentifierOtp(
          channel,
          this.linkForm.controls.identifier.value.trim(),
          this.linkCaptchaToken()!,
        ),
      );
      this.linkStep.set('otp');
      this.resetLinkCaptcha();
    } catch (error) {
      this.linkError.set(this.apiErrors.messageFromHttpError(error));
      this.resetLinkCaptcha();
    } finally {
      this.linkSubmitting.set(false);
    }
  }

  async submitLinkVerify(): Promise<void> {
    const channel = this.linkChannel();
    if (!channel || this.otpForm.invalid || this.linkSubmitting()) {
      return;
    }

    this.linkSubmitting.set(true);
    this.linkError.set(null);

    try {
      const updated = await firstValueFrom(
        this.accountService.verifyLinkIdentifierOtp(
          channel,
          this.linkForm.controls.identifier.value.trim(),
          this.otpForm.controls.code.value.trim(),
        ),
      );
      this.identifiers.set(updated);
      this.linkStep.set('idle');
      this.linkForm.reset();
      this.otpForm.reset();
      await firstValueFrom(this.auth.fetchCurrentUser());
    } catch (error) {
      this.linkError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.linkSubmitting.set(false);
    }
  }

  openConfirmDialog(): void {
    if (!this.status()?.canDeactivate || this.deactivating()) {
      return;
    }

    this.deactivateError.set(null);
    this.showConfirmDialog.set(true);
  }

  closeConfirmDialog(): void {
    this.showConfirmDialog.set(false);
    this.deactivateError.set(null);
  }

  async logoutEverywhere(): Promise<void> {
    if (this.loggingOutEverywhere()) {
      return;
    }

    this.loggingOutEverywhere.set(true);
    this.logoutEverywhereError.set(null);

    try {
      await firstValueFrom(this.auth.logoutEverywhere());
      await this.router.navigate(['/']);
    } catch (error) {
      this.logoutEverywhereError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.loggingOutEverywhere.set(false);
    }
  }

  async confirmDeactivate(): Promise<void> {
    if (!this.status()?.canDeactivate || this.deactivating()) {
      return;
    }

    this.deactivating.set(true);
    this.deactivateError.set(null);

    try {
      await firstValueFrom(this.accountService.deactivateAccount());
      this.auth.clearSession();
      this.showConfirmDialog.set(false);
      await this.router.navigate(['/']);
    } catch (error) {
      this.deactivateError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.deactivating.set(false);
    }
  }

  private async loadPage(): Promise<void> {
    try {
      const [status, identifiers] = await Promise.all([
        firstValueFrom(this.accountService.getDeactivationStatus()),
        firstValueFrom(this.accountService.getIdentifiers()),
      ]);
      this.status.set(status);
      this.identifiers.set(identifiers);
      this.configureLinkIdentifierValidators(identifiers);
    } catch {
      this.loadError.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }

  private configureLinkIdentifierValidators(identifiers: AccountIdentifiers): void {
    const channel: AuthIdentifierChannel = identifiers.email ? 'phone' : 'email';
    const control = this.linkForm.controls.identifier;
    control.clearValidators();
    control.addValidators(
      channel === 'phone'
        ? [Validators.required, Validators.pattern(/^\d{11}$/)]
        : [Validators.required, Validators.email],
    );
    control.updateValueAndValidity();
  }

  private resetLinkCaptcha(): void {
    this.linkCaptchaToken.set(null);
    this.linkTurnstile()?.reset();
  }

  private translateBlocker(code: string): string {
    const key = `account.blocker.${code}`;
    const translated = this.translate.instant(key);
    return translated === key ? code : translated;
  }
}
