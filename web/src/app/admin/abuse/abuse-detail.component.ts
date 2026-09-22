import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorService } from '../../i18n/api-error.service';
import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { DomainLabelService } from '../../i18n/domain-label.service';
import { ChatThreadDetail } from '../../chats/models/chat.models';
import { ClaimStatus } from '../../claims/models/claim.models';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import {
  BadgeComponent,
  BadgeVariant,
} from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { CardComponent } from '../../shared/ui/card/card.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { TabItem, TabsComponent } from '../../shared/ui/tabs/tabs.component';
import {
  AbuseReportDetail,
  AbuseResolutionOutcome,
  AdminAbuseService,
  FlaggedListingSummary,
  InvestigationClaim,
  InvestigationPhoto,
} from '../admin-abuse.service';

type InvestigationTab = 'chat' | 'claims' | 'photos';

const BAN_REASON_MIN_LENGTH = 3;

@Component({
  selector: 'app-abuse-detail',
  standalone: true,
  imports: [
    AlertComponent,
    BadgeComponent,
    ButtonComponent,
    CardComponent,
    ConfirmDialogComponent,
    DatePipe,
    EmptyStateComponent,
    FormFieldComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    TabsComponent,
    TranslateModule,
  ],
  templateUrl: './abuse-detail.component.html',
  styleUrl: './abuse-detail.component.scss',
})
export class AbuseDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly abuseService = inject(AdminAbuseService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly catalogLabels = inject(CatalogLabelService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly domainLabels = inject(DomainLabelService);

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly detail = signal<AbuseReportDetail | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly actionSuccess = signal<string | null>(null);
  readonly noteError = signal<string | null>(null);
  readonly showResolveConfirm = signal(false);
  readonly resolving = signal(false);
  readonly selectedOutcome = signal<AbuseResolutionOutcome>('no_action');

  readonly investigationTab = signal<InvestigationTab>('chat');
  readonly investigationLoading = signal(false);
  readonly investigationError = signal<string | null>(null);
  readonly chatThreads = signal<ChatThreadDetail[] | null>(null);
  readonly claims = signal<InvestigationClaim[] | null>(null);
  readonly photos = signal<InvestigationPhoto[] | null>(null);

  readonly investigationTabs: TabItem[] = [
    {
      id: 'chat',
      label: this.translate.instant('admin.abuse.investigation.tab_chat'),
    },
    {
      id: 'claims',
      label: this.translate.instant('admin.abuse.investigation.tab_claims'),
    },
    {
      id: 'photos',
      label: this.translate.instant('admin.abuse.investigation.tab_photos'),
    },
  ];

  readonly resolveForm = this.fb.nonNullable.group({
    outcome: this.fb.nonNullable.control<AbuseResolutionOutcome>(
      'no_action',
      Validators.required,
    ),
    adminNote: '',
    banTargetUserId: '',
  });

  private investigationGeneration = 0;
  private destroyed = false;

  constructor() {
    this.destroyRef.onDestroy(() => {
      this.destroyed = true;
      this.investigationGeneration += 1;
    });

    this.resolveForm.controls.outcome.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((outcome) => {
        this.selectedOutcome.set(outcome);
        this.resolveForm.controls.adminNote.setValue('');
        this.noteError.set(null);
        this.actionError.set(null);
      });
  }

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.loadError.set(this.translate.instant('error.internal.error'));
      this.loading.set(false);
      return;
    }

    void this.loadDetail(id);
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  claimStatusLabel(status: string): string {
    return this.domainLabels.claimStatus(status as ClaimStatus);
  }

  claimBadge(status: string): BadgeVariant {
    return this.domainLabels.claimBadgeVariant(status as ClaimStatus);
  }

  outcomeLabel(outcome: string): string {
    return this.translate.instant(`admin.abuse.outcome.${outcome}`);
  }

  decisionLabel(reason: string): string {
    const translated = this.translate.instant(reason);
    return translated === reason ? reason : translated;
  }

  canViewPublicListing(status: string): boolean {
    return status === 'published' || status === 'claim_in_progress';
  }

  listingPath(listing: FlaggedListingSummary): string[] {
    const segment = listing.type === 'found' ? 'found' : 'lost';
    return ['/', segment, listing.id];
  }

  canTakedown(): boolean {
    const status = this.detail()?.listing.status;
    return status === 'published' || status === 'claim_in_progress';
  }

  banTargets(): { id: string; label: string }[] {
    const current = this.detail();
    if (!current) {
      return [];
    }

    const owner = {
      id: current.listing.listingOwnerUserId,
      label: this.translate.instant('admin.abuse.resolve.ban_target_owner', {
        name: current.listing.listingOwnerDisplayName,
      }),
    };

    if (current.abuseReporterUserId === current.listing.listingOwnerUserId) {
      return [owner];
    }

    return [
      owner,
      {
        id: current.abuseReporterUserId,
        label: this.translate.instant(
          'admin.abuse.resolve.ban_target_flagger',
          { name: current.abuseReporterDisplayName },
        ),
      },
    ];
  }

  claimantName(thread: ChatThreadDetail): string {
    return thread.counterpartyDisplayName;
  }

  selectInvestigationTab(id: string): void {
    if (id !== 'chat' && id !== 'claims' && id !== 'photos') {
      return;
    }

    this.investigationTab.set(id);
    this.investigationError.set(null);
    if (this.isInvestigationLoaded(id)) {
      this.investigationLoading.set(false);
      return;
    }

    void this.loadInvestigation(id);
  }

  reloadInvestigation(): void {
    const tab = this.investigationTab();
    this.clearInvestigationTab(tab);
    void this.loadInvestigation(tab);
  }

  openResolveConfirm(): void {
    if (this.resolving() || this.detail()?.status !== 'open') {
      return;
    }

    this.actionError.set(null);
    this.noteError.set(null);
    const outcome = this.selectedOutcome();

    if (outcome === 'takedown' && !this.canTakedown()) {
      this.actionError.set(
        this.translate.instant('admin.abuse.resolve.takedown_unavailable'),
      );
      return;
    }

    if (outcome === 'ban') {
      const reason = this.resolveForm.controls.adminNote.value.trim();
      if (reason.length < BAN_REASON_MIN_LENGTH) {
        this.noteError.set(
          this.translate.instant('admin.abuse.resolve.ban_reason_required'),
        );
        return;
      }

      if (!this.resolveForm.controls.banTargetUserId.value) {
        this.actionError.set(this.translate.instant('error.internal.error'));
        return;
      }
    }

    this.showResolveConfirm.set(true);
  }

  closeResolveConfirm(): void {
    if (this.resolving()) {
      return;
    }

    this.showResolveConfirm.set(false);
  }

  confirmTitleKey(): string {
    switch (this.selectedOutcome()) {
      case 'takedown':
        return 'admin.abuse.resolve.confirm_takedown_title';
      case 'ban':
        return 'admin.abuse.resolve.confirm_ban_title';
      default:
        return 'admin.abuse.resolve.confirm_no_action_title';
    }
  }

  confirmIntroKey(): string {
    switch (this.selectedOutcome()) {
      case 'takedown':
        return 'admin.abuse.resolve.confirm_takedown_intro';
      case 'ban':
        return 'admin.abuse.resolve.confirm_ban_intro';
      default:
        return 'admin.abuse.resolve.confirm_no_action_intro';
    }
  }

  confirmLabelKey(): string {
    switch (this.selectedOutcome()) {
      case 'takedown':
        return 'admin.abuse.resolve.confirm_takedown';
      case 'ban':
        return 'admin.abuse.resolve.confirm_ban';
      default:
        return 'admin.abuse.resolve.confirm_no_action';
    }
  }

  doneMessageKey(outcome: AbuseResolutionOutcome): string {
    switch (outcome) {
      case 'takedown':
        return 'admin.abuse.resolve.done_takedown';
      case 'ban':
        return 'admin.abuse.resolve.done_ban';
      default:
        return 'admin.abuse.resolve.done_no_action';
    }
  }

  async confirmResolve(): Promise<void> {
    const current = this.detail();
    if (!current || current.status !== 'open' || this.resolving()) {
      return;
    }

    const outcome = this.selectedOutcome();
    const note = this.resolveForm.controls.adminNote.value.trim();
    this.resolving.set(true);
    this.actionError.set(null);

    try {
      await firstValueFrom(
        this.abuseService.resolve(current.id, {
          outcome,
          adminNote: note.length > 0 ? note : null,
          banTargetUserId:
            outcome === 'ban'
              ? this.resolveForm.controls.banTargetUserId.value
              : null,
        }),
      );
      this.showResolveConfirm.set(false);
      this.actionSuccess.set(
        this.translate.instant(this.doneMessageKey(outcome)),
      );
      this.resolveForm.controls.adminNote.setValue('');
      this.clearInvestigation();
      await this.refreshDetail(current.id);
    } catch (error) {
      this.showResolveConfirm.set(false);
      this.actionError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.resolving.set(false);
    }
  }

  private async loadDetail(id: string): Promise<void> {
    this.loading.set(true);
    this.loadError.set(null);

    try {
      await this.refreshDetail(id);
    } finally {
      this.loading.set(false);
    }
  }

  private async refreshDetail(id: string): Promise<void> {
    try {
      const detail = await firstValueFrom(this.abuseService.getReport(id));
      if (this.destroyed) {
        return;
      }
      this.detail.set(detail);
      this.resolveForm.controls.banTargetUserId.setValue(
        detail.listing.listingOwnerUserId,
      );

      if (detail.status === 'open') {
        void this.loadInvestigation(this.investigationTab());
      } else {
        this.clearInvestigation();
      }
    } catch (error) {
      this.detail.set(null);
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.loadError.set(
          this.translate.instant('admin.abuse.detail.not_found'),
        );
      } else {
        this.loadError.set(this.apiErrors.messageFromHttpError(error));
      }
    }
  }

  private isInvestigationLoaded(tab: InvestigationTab): boolean {
    switch (tab) {
      case 'chat':
        return this.chatThreads() !== null;
      case 'claims':
        return this.claims() !== null;
      case 'photos':
        return this.photos() !== null;
    }
  }

  private async loadInvestigation(tab: InvestigationTab): Promise<void> {
    const reportId = this.detail()?.listing.id;
    if (!reportId || this.detail()?.status !== 'open') {
      return;
    }

    if (this.isInvestigationLoaded(tab)) {
      return;
    }

    const generation = ++this.investigationGeneration;
    this.investigationLoading.set(true);
    this.investigationError.set(null);

    try {
      if (tab === 'chat') {
        const response = await firstValueFrom(
          this.abuseService.getInvestigationChat(reportId),
        );
        if (generation !== this.investigationGeneration) {
          return;
        }
        this.chatThreads.set(response.threads);
      } else if (tab === 'claims') {
        const response = await firstValueFrom(
          this.abuseService.getInvestigationClaims(reportId),
        );
        if (generation !== this.investigationGeneration) {
          return;
        }
        this.claims.set(response.items);
      } else {
        const response = await firstValueFrom(
          this.abuseService.getInvestigationPhotos(reportId),
        );
        if (generation !== this.investigationGeneration) {
          return;
        }
        this.photos.set(response.photos);
      }
    } catch (error) {
      if (generation !== this.investigationGeneration) {
        return;
      }
      this.investigationError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      if (generation === this.investigationGeneration) {
        this.investigationLoading.set(false);
      }
    }
  }

  private clearInvestigationTab(tab: InvestigationTab): void {
    this.investigationGeneration += 1;
    if (tab === 'chat') {
      this.chatThreads.set(null);
    } else if (tab === 'claims') {
      this.claims.set(null);
    } else {
      this.photos.set(null);
    }
  }

  private clearInvestigation(): void {
    this.investigationGeneration += 1;
    this.chatThreads.set(null);
    this.claims.set(null);
    this.photos.set(null);
    this.investigationLoading.set(false);
    this.investigationError.set(null);
  }
}
