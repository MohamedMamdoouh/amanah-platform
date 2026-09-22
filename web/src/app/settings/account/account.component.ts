import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { ApiErrorService } from '../../i18n/api-error.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { AccountDeactivationStatus, AccountService } from '../account.service';

@Component({
  selector: 'app-account-settings',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    ConfirmDialogComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
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

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly status = signal<AccountDeactivationStatus | null>(null);
  readonly showConfirmDialog = signal(false);
  readonly deactivating = signal(false);
  readonly deactivateError = signal<string | null>(null);
  readonly loggingOutEverywhere = signal(false);
  readonly logoutEverywhereError = signal<string | null>(null);

  readonly translatedBlockers = computed(() => {
    const blockers = this.status()?.blockers ?? [];
    return blockers.map((blocker) => this.translateBlocker(blocker));
  });

  ngOnInit(): void {
    void this.loadStatus();
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

  private async loadStatus(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.accountService.getDeactivationStatus(),
      );
      this.status.set(response);
    } catch {
      this.loadError.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }

  private translateBlocker(code: string): string {
    const key = `account.blocker.${code}`;
    const translated = this.translate.instant(key);
    return translated === key ? code : translated;
  }
}
