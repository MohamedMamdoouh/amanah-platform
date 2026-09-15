import {
  Component,
  inject,
  input,
  OnInit,
  output,
  signal,
} from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorService } from '../../i18n/api-error.service';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { ClaimService } from '../claim.service';
import { ClaimDetail } from '../models/claim.models';
import {
  canCancelResolution,
  canConfirmResolution,
  resolutionStatusMessageKey,
  showClaimResolutionSection,
} from '../resolution/resolution.helpers';

@Component({
  selector: 'app-claim-resolution-actions',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    ConfirmDialogComponent,
    LoadingIndicatorComponent,
    TranslateModule,
  ],
  templateUrl: './claim-resolution-actions.component.html',
  styleUrl: './claim-resolution-actions.component.scss',
})
export class ClaimResolutionActionsComponent implements OnInit {
  private readonly claimService = inject(ClaimService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);

  readonly claimId = input.required<string>();

  readonly changed = output<void>();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly claim = signal<ClaimDetail | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly acting = signal(false);
  readonly showConfirmDialog = signal(false);
  readonly showCancelDialog = signal(false);

  ngOnInit(): void {
    void this.loadClaim();
  }

  showSection(): boolean {
    return showClaimResolutionSection(this.claim());
  }

  statusMessage(): string | null {
    const key = resolutionStatusMessageKey(this.claim());
    return key ? this.translate.instant(key) : null;
  }

  canConfirm(): boolean {
    return canConfirmResolution(this.claim());
  }

  canCancel(): boolean {
    return canCancelResolution(this.claim());
  }

  openConfirmDialog(): void {
    this.actionError.set(null);
    this.showConfirmDialog.set(true);
    this.showCancelDialog.set(false);
  }

  openCancelDialog(): void {
    this.actionError.set(null);
    this.showCancelDialog.set(true);
    this.showConfirmDialog.set(false);
  }

  closeDialogs(): void {
    this.showConfirmDialog.set(false);
    this.showCancelDialog.set(false);
  }

  async confirmResolution(): Promise<void> {
    if (this.acting()) {
      return;
    }

    this.acting.set(true);
    this.actionError.set(null);

    try {
      await firstValueFrom(
        this.claimService.confirmResolution(this.claimId()),
      );
      this.closeDialogs();
      await this.loadClaim();
      this.changed.emit();
    } catch (error) {
      this.actionError.set(
        this.apiErrors.messageFromHttpError(error, {
          conflictKey: 'claims.review.invalid_status',
        }),
      );
    } finally {
      this.acting.set(false);
    }
  }

  async cancelClaim(): Promise<void> {
    if (this.acting()) {
      return;
    }

    this.acting.set(true);
    this.actionError.set(null);

    try {
      await firstValueFrom(this.claimService.cancelClaim(this.claimId()));
      this.closeDialogs();
      await this.loadClaim();
      this.changed.emit();
    } catch (error) {
      this.actionError.set(
        this.apiErrors.messageFromHttpError(error, {
          conflictKey: 'claims.review.invalid_status',
        }),
      );
    } finally {
      this.acting.set(false);
    }
  }

  private async loadClaim(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const detail = await firstValueFrom(
        this.claimService.getById(this.claimId()),
      );
      this.claim.set(detail);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }
}
