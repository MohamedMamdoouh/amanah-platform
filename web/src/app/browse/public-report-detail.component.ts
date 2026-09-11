import { DatePipe } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../auth/auth.service';
import { CatalogLabelService } from '../i18n/catalog-label.service';
import { ReportType } from '../reports/models/report.models';
import { AlertComponent } from '../shared/ui/alert/alert.component';
import {
  BadgeComponent,
  BadgeVariant,
} from '../shared/ui/badge/badge.component';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { CardComponent } from '../shared/ui/card/card.component';
import { LoadingIndicatorComponent } from '../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../shared/ui/page-header/page-header.component';
import { ClaimFormComponent } from '../claims/claim-form/claim-form.component';
import { BrowseService, mapBrowseError } from './browse.service';
import { PublicReportDetail } from './models/browse.models';

@Component({
  selector: 'app-public-report-detail',
  standalone: true,
  imports: [
    AlertComponent,
    BadgeComponent,
    ButtonComponent,
    CardComponent,
    ClaimFormComponent,
    DatePipe,
    LoadingIndicatorComponent,
    PageHeaderComponent,
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
  private readonly auth = inject(AuthService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly report = signal<PublicReportDetail | null>(null);

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

  typeLabel(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  statusLabel(status: string): string {
    return this.translate.instant(`reports.status.${status}`);
  }

  statusBadgeVariant(status: string): BadgeVariant {
    return status === 'claim_in_progress' ? 'claim' : 'published';
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

  canClickClaim(): boolean {
    return this.isPublished() && !this.auth.isLoggedIn();
  }

  showClaimForm(): boolean {
    return this.isPublished() && this.auth.isLoggedIn();
  }

  isClaimDisabled(): boolean {
    return this.isClaimInProgress();
  }

  canClickMessage(): boolean {
    return !this.auth.isLoggedIn();
  }

  isMessageDisabled(): boolean {
    return this.auth.isLoggedIn();
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

    return this.translate.instant('browse.detail.message_coming_soon');
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
    if (!this.canClickMessage()) {
      return;
    }

    void this.router.navigate(['/login'], {
      queryParams: { returnUrl: this.router.url },
    });
  }

  private async loadReport(id: string, type: ReportType): Promise<void> {
    const request$ =
      type === 'lost'
        ? this.browseService.getLostDetail(id)
        : this.browseService.getFoundDetail(id);

    try {
      const detail = await firstValueFrom(request$);
      this.report.set(detail);
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
}
