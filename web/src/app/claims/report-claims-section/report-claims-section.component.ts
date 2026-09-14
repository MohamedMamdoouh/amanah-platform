import { DatePipe } from '@angular/common';
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
import { DomainLabelService } from '../../i18n/domain-label.service';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { SpinnerComponent } from '../../shared/ui/spinner/spinner.component';
import { ClaimPhotoUploadService } from '../../uploads/claim-photo-upload.service';
import { ClaimService } from '../claim.service';
import { ReportClaimSummary } from '../models/claim.models';

interface ClaimPhotoState {
  url: string | null;
  loading: boolean;
}

@Component({
  selector: 'app-report-claims-section',
  standalone: true,
  imports: [
    AlertComponent,
    BadgeComponent,
    ButtonComponent,
    ConfirmDialogComponent,
    DatePipe,
    LoadingIndicatorComponent,
    SpinnerComponent,
    TranslateModule,
  ],
  templateUrl: './report-claims-section.component.html',
  styleUrl: './report-claims-section.component.scss',
})
export class ReportClaimsSectionComponent implements OnInit {
  private readonly claimService = inject(ClaimService);
  private readonly photoService = inject(ClaimPhotoUploadService);
  private readonly apiErrors = inject(ApiErrorService);
  protected readonly domainLabels = inject(DomainLabelService);
  private readonly translate = inject(TranslateService);

  readonly reportId = input.required<string>();
  readonly canReview = input(false);

  readonly reviewed = output<void>();

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly claims = signal<ReportClaimSummary[]>([]);
  readonly photos = signal<Record<string, ClaimPhotoState>>({});
  readonly actionError = signal<string | null>(null);
  readonly actingClaimId = signal<string | null>(null);
  readonly confirmApproveId = signal<string | null>(null);
  readonly confirmRejectId = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadClaims();
  }

  canActOn(claim: ReportClaimSummary): boolean {
    return this.canReview() && claim.status === 'pending';
  }

  photoState(claimId: string): ClaimPhotoState | null {
    return this.photos()[claimId] ?? null;
  }

  openApprove(claimId: string): void {
    this.actionError.set(null);
    this.confirmApproveId.set(claimId);
    this.confirmRejectId.set(null);
  }

  openReject(claimId: string): void {
    this.actionError.set(null);
    this.confirmRejectId.set(claimId);
    this.confirmApproveId.set(null);
  }

  closeDialogs(): void {
    this.confirmApproveId.set(null);
    this.confirmRejectId.set(null);
  }

  async confirmApprove(): Promise<void> {
    const claimId = this.confirmApproveId();
    if (!claimId) {
      return;
    }

    await this.runAction(claimId, () => this.claimService.approve(claimId));
    this.closeDialogs();
  }

  async confirmReject(): Promise<void> {
    const claimId = this.confirmRejectId();
    if (!claimId) {
      return;
    }

    await this.runAction(claimId, () => this.claimService.reject(claimId));
    this.closeDialogs();
  }

  private async runAction(
    claimId: string,
    action: () => ReturnType<ClaimService['approve']>,
  ): Promise<void> {
    if (this.actingClaimId()) {
      return;
    }

    this.actingClaimId.set(claimId);
    this.actionError.set(null);

    try {
      await firstValueFrom(action());
      this.reviewed.emit();
      await this.loadClaims();
    } catch (error) {
      this.actionError.set(
        this.apiErrors.messageFromHttpError(error, {
          conflictKey: 'claims.review.invalid_status',
        }),
      );
    } finally {
      this.actingClaimId.set(null);
    }
  }

  private async loadClaims(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const claims = await firstValueFrom(
        this.claimService.getByReport(this.reportId()),
      );
      this.claims.set(claims);
      void this.loadPhotos(claims);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }

  private async loadPhotos(claims: ReportClaimSummary[]): Promise<void> {
    const initial: Record<string, ClaimPhotoState> = {};
    for (const claim of claims) {
      if (claim.hasPhoto) {
        initial[claim.id] = { url: null, loading: true };
      }
    }
    this.photos.set(initial);

    await Promise.all(
      claims
        .filter((claim) => claim.hasPhoto)
        .map(async (claim) => {
          try {
            const presign = await firstValueFrom(
              this.photoService.getPresignedUrl(claim.id),
            );
            this.updatePhoto(claim.id, { url: presign.url, loading: false });
          } catch {
            this.updatePhoto(claim.id, { url: null, loading: false });
          }
        }),
    );
  }

  private updatePhoto(claimId: string, patch: Partial<ClaimPhotoState>): void {
    this.photos.update((current) => ({
      ...current,
      [claimId]: { ...current[claimId], ...patch },
    }));
  }
}
