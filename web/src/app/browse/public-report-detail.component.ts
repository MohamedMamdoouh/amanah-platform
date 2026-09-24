import { HttpErrorResponse } from '@angular/common/http';
import { AppDatePipe } from '../i18n/app-date.pipe';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import {
  AbuseFlagService,
  FlagListingResponse,
} from '../abuse/abuse-flag.service';
import { FlagListingDialogComponent } from '../abuse/flag-listing-dialog.component';
import { AuthService } from '../auth/auth.service';
import { ClaimFormComponent } from '../claims/claim-form/claim-form.component';
import { ClaimResolutionActionsComponent } from '../claims/claim-resolution-actions/claim-resolution-actions.component';
import { ClaimService } from '../claims/claim.service';
import {
  isResolutionEligibleReportStatus,
  loadClaimantApprovedClaimId,
  loadReporterApprovedClaimId,
  showResolutionActions,
} from '../claims/resolution/resolution.helpers';
import { CatalogLabelService } from '../i18n/catalog-label.service';
import { DomainLabelService } from '../i18n/domain-label.service';
import { ReportType } from '../reports/models/report.models';
import { ReportService } from '../reports/report.service';
import { AlertComponent } from '../shared/ui/alert/alert.component';
import { BadgeComponent } from '../shared/ui/badge/badge.component';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { CardComponent } from '../shared/ui/card/card.component';
import { IconComponent } from '../shared/ui/icon/icon.component';
import { LoadingIndicatorComponent } from '../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../shared/ui/page-header/page-header.component';
import { PhotoLightboxComponent } from '../shared/ui/photo-lightbox/photo-lightbox.component';
import { ReportTypeMarkComponent } from '../shared/ui/report-type-mark/report-type-mark.component';
import { BrowseService, mapBrowseError } from './browse.service';
import { PublicReportDetail } from './models/browse.models';

@Component({
  selector: 'app-public-report-detail',
  standalone: true,
  imports: [
    AppDatePipe,
    AlertComponent,
    BadgeComponent,
    ButtonComponent,
    CardComponent,
    ClaimFormComponent,
    ClaimResolutionActionsComponent,
    FlagListingDialogComponent,
    IconComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    PhotoLightboxComponent,
    ReportTypeMarkComponent,
    TranslateModule,
  ],
  templateUrl: './public-report-detail.component.html',
  styleUrl: './public-report-detail.component.scss',
})
export class PublicReportDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly browseService = inject(BrowseService);
  private readonly catalogLabels = inject(CatalogLabelService);
  protected readonly auth = inject(AuthService);
  private readonly claimService = inject(ClaimService);
  private readonly reportService = inject(ReportService);
  private readonly abuseFlagService = inject(AbuseFlagService);
  protected readonly domainLabels = inject(DomainLabelService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly report = signal<PublicReportDetail | null>(null);
  readonly approvedClaimId = signal<string | null>(null);
  readonly chatThreadId = signal<string | null>(null);
  readonly isListingOwner = signal(false);
  readonly openFlag = signal<FlagListingResponse | null>(null);
  readonly flagDialogOpen = signal(false);
  readonly flagSuccessMessage = signal<string | null>(null);
  readonly lightboxUrl = signal<string | null>(null);

  readonly displayPhotos = computed(() => {
    const detail = this.report();
    if (!detail) {
      return [];
    }

    return detail.photos
      .filter((photo) => photo.thumbnailUrl)
      .map((photo) => ({
        id: photo.id,
        url: photo.thumbnailUrl!,
      }));
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    const type = this.route.snapshot.data['type'] as ReportType;

    if (!id) {
      void this.router.navigate(['/not-found']);
      return;
    }

    void this.loadReport(id, type);
  }

  openPhoto(url: string): void {
    this.lightboxUrl.set(url);
  }

  closePhoto(): void {
    this.lightboxUrl.set(null);
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  fieldLabel(fieldKey: string): string {
    const categoryCode = this.report()?.categoryCode ?? '';
    return this.catalogLabels.field(categoryCode, fieldKey);
  }

  categoryFieldEntries(): [string, string][] {
    const detail = this.report();
    if (!detail) {
      return [];
    }

    return Object.entries(detail.categoryFields).sort(([a], [b]) =>
      a.localeCompare(b),
    );
  }

  isFound(): boolean {
    return this.report()?.type === 'found';
  }

  isClaimInProgress(): boolean {
    return this.report()?.status === 'claim_in_progress';
  }

  isPublished(): boolean {
    return this.report()?.status === 'published';
  }

  isFlaggableListing(): boolean {
    return this.isPublished() || this.isClaimInProgress();
  }

  showFlagSection(): boolean {
    return this.isFlaggableListing();
  }

  showFlagAction(): boolean {
    return (
      this.isFlaggableListing() &&
      !this.isListingOwner() &&
      !this.openFlag()
    );
  }

  showOpenFlagSummary(): boolean {
    return (
      this.auth.isLoggedIn() &&
      !this.isListingOwner() &&
      this.openFlag() !== null
    );
  }

  flagReasonLabel(code: string): string {
    const translated = this.translate.instant(code);
    return translated === code ? code : translated;
  }

  canClickClaim(): boolean {
    return this.isPublished() && !this.auth.isLoggedIn();
  }

  showClaimForm(): boolean {
    return (
      this.isPublished() &&
      this.auth.isLoggedIn() &&
      !this.isListingOwner()
    );
  }

  isClaimDisabled(): boolean {
    return this.isClaimInProgress();
  }

  canClickMessage(): boolean {
    return !this.auth.isLoggedIn() || this.chatThreadId() !== null;
  }

  isMessageDisabled(): boolean {
    return this.auth.isLoggedIn() && this.chatThreadId() === null;
  }

  isMessageAwaitingApproval(): boolean {
    return this.auth.isLoggedIn() && this.chatThreadId() === null;
  }

  claimHint(): string | null {
    if (this.isClaimInProgress()) {
      return this.translate.instant('browse.detail.claim_in_progress_note');
    }

    if (this.isPublished() && !this.auth.isLoggedIn()) {
      return this.translate.instant('browse.detail.claim_login_required');
    }

    return null;
  }

  messageHint(): string | null {
    if (!this.auth.isLoggedIn()) {
      return this.translate.instant('browse.detail.message_login_required');
    }

    if (this.chatThreadId() !== null) {
      return null;
    }

    return this.translate.instant('browse.detail.message_after_approval');
  }

  onClaimClick(): void {
    if (!this.canClickClaim()) {
      return;
    }

    void this.router.navigate(['/login'], {
      queryParams: { returnUrl: this.router.url },
    });
  }

  onMessageClick(): void {
    if (this.isMessageDisabled()) {
      return;
    }

    const threadId = this.chatThreadId();
    if (threadId) {
      void this.router.navigate(['/my/chats', threadId]);
      return;
    }

    void this.router.navigate(['/login'], {
      queryParams: { returnUrl: this.router.url },
    });
  }

  onFlagClick(): void {
    if (!this.isFlaggableListing() || this.isListingOwner()) {
      return;
    }

    if (!this.auth.isLoggedIn()) {
      void this.router.navigate(['/login'], {
        queryParams: { returnUrl: this.router.url },
      });
      return;
    }

    if (this.openFlag()) {
      this.flagDialogOpen.set(true);
      return;
    }

    this.flagSuccessMessage.set(null);
    this.flagDialogOpen.set(true);
  }

  closeFlagDialog(): void {
    this.flagDialogOpen.set(false);
  }

  onFlagSubmitted(flag: FlagListingResponse): void {
    this.openFlag.set(flag);
    this.flagDialogOpen.set(false);
    this.flagSuccessMessage.set(
      this.translate.instant('abuse.flag.submit_success'),
    );
  }

  showResolutionActions(): boolean {
    const detail = this.report();
    if (!detail) {
      return false;
    }

    return showResolutionActions({
      reportStatus: detail.status,
      approvedClaimId: this.approvedClaimId(),
      isLoggedIn: this.auth.isLoggedIn(),
    });
  }

  async onResolutionChanged(): Promise<void> {
    const detail = this.report();
    if (!detail) {
      return;
    }

    await this.loadReport(detail.id, detail.type);
  }

  private async loadReport(id: string, type: ReportType): Promise<void> {
    const request$ =
      type === 'lost'
        ? this.browseService.getLostDetail(id)
        : this.browseService.getFoundDetail(id);

    try {
      const detail = await firstValueFrom(request$);
      this.report.set(detail);
      await this.loadFlagContext(id, detail.status);
      await this.loadParticipantChat(id, detail.status);
      this.loading.set(false);
    } catch (error) {
      const route = mapBrowseError(error);
      if (route) {
        void this.router.navigate([`/${route}`]);
        return;
      }

      this.error.set(this.translate.instant('error.internal.error'));
      this.loading.set(false);
    }
  }

  private async loadFlagContext(
    reportId: string,
    status: PublicReportDetail['status'],
  ): Promise<void> {
    this.isListingOwner.set(false);
    this.openFlag.set(null);

    if (!this.auth.isLoggedIn()) {
      return;
    }

    const flaggable =
      status === 'published' || status === 'claim_in_progress';
    if (!flaggable) {
      return;
    }

    this.isListingOwner.set(await this.checkListingOwnership(reportId));
    if (this.isListingOwner()) {
      return;
    }

    try {
      const flag = await firstValueFrom(
        this.abuseFlagService.getOpenFlag(reportId),
      );
      this.openFlag.set(flag);
    } catch (error) {
      if (
        error instanceof HttpErrorResponse &&
        error.status === 404
      ) {
        return;
      }
    }
  }

  private async loadParticipantChat(
    reportId: string,
    status: PublicReportDetail['status'],
  ): Promise<void> {
    this.approvedClaimId.set(null);
    this.chatThreadId.set(null);

    if (!this.auth.isLoggedIn() || !isResolutionEligibleReportStatus(status)) {
      return;
    }

    const approvedId = this.isListingOwner()
      ? await loadReporterApprovedClaimId(
          this.claimService,
          reportId,
          status,
        )
      : await loadClaimantApprovedClaimId(
          this.claimService,
          reportId,
          status,
        );

    this.approvedClaimId.set(approvedId);
    if (!approvedId) {
      return;
    }

    try {
      const claim = await firstValueFrom(this.claimService.getById(approvedId));
      this.chatThreadId.set(claim.chatThreadId ?? null);
    } catch {
      this.chatThreadId.set(null);
    }
  }

  private async checkListingOwnership(reportId: string): Promise<boolean> {
    try {
      await firstValueFrom(this.reportService.getById(reportId));
      return true;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        return false;
      }

      return false;
    }
  }
}
