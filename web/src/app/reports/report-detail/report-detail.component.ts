import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorBody, ApiErrorService } from '../../i18n/api-error.service';
import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { ReportPhotoUploadService } from '../../uploads/report-photo-upload.service';
import { ReportDetail, WithdrawalReason } from '../models/report.models';
import { ReportService } from '../report.service';

interface DisplayPhoto {
  id: string;
  url: string | null;
  loading: boolean;
}

@Component({
  selector: 'app-report-detail',
  standalone: true,
  imports: [DatePipe, ReactiveFormsModule, RouterLink, TranslateModule],
  templateUrl: './report-detail.component.html',
  styleUrl: './report-detail.component.scss',
})
export class ReportDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly reportService = inject(ReportService);
  private readonly uploadService = inject(ReportPhotoUploadService);
  private readonly catalogLabels = inject(CatalogLabelService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly report = signal<ReportDetail | null>(null);
  readonly photos = signal<DisplayPhoto[]>([]);
  readonly showWithdraw = signal(false);
  readonly withdrawing = signal(false);
  readonly withdrawError = signal<string | null>(null);
  readonly withdrawn = signal(false);

  readonly withdrawForm = this.fb.nonNullable.group({
    reason: ['' as WithdrawalReason | '', Validators.required],
  });

  readonly withdrawalReasons: WithdrawalReason[] = [
    'recovered_outside',
    'no_longer_needed',
    'posted_by_mistake',
    'other',
  ];

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set(this.translate.instant('error.internal.error'));
      this.loading.set(false);
      return;
    }

    void this.loadReport(id);
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  fieldLabel(fieldKey: string): string {
    const report = this.report();
    if (!report) {
      return fieldKey;
    }
    return this.catalogLabels.field(report.categoryCode, fieldKey);
  }

  typeLabel(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  statusLabel(status: string): string {
    return this.translate.instant(`reports.status.${status}`);
  }

  withdrawalReasonLabel(reason: WithdrawalReason): string {
    return this.translate.instant(`reports.withdraw.reasons.${reason}`);
  }

  categoryFieldEntries(): [string, string][] {
    const report = this.report();
    if (!report) {
      return [];
    }
    return Object.entries(report.categoryFields).sort(([a], [b]) => a.localeCompare(b));
  }

  canWithdraw(): boolean {
    return this.report()?.status === 'pending_review' && !this.withdrawn();
  }

  openWithdraw(): void {
    this.withdrawError.set(null);
    this.showWithdraw.set(true);
  }

  closeWithdraw(): void {
    this.showWithdraw.set(false);
    this.withdrawError.set(null);
    this.withdrawForm.reset();
  }

  async confirmWithdraw(): Promise<void> {
    const report = this.report();
    if (!report || this.withdrawForm.invalid) {
      return;
    }

    this.withdrawing.set(true);
    this.withdrawError.set(null);

    try {
      await firstValueFrom(
        this.reportService.withdraw(report.id, {
          reason: this.withdrawForm.controls.reason.value as WithdrawalReason,
        }),
      );
      this.withdrawn.set(true);
      this.showWithdraw.set(false);
      this.withdrawForm.reset();
      await this.router.navigate(['/my/reports']);
    } catch (error) {
      this.withdrawError.set(this.parseError(error));
    } finally {
      this.withdrawing.set(false);
    }
  }

  private async loadReport(id: string): Promise<void> {
    try {
      const report = await firstValueFrom(this.reportService.getById(id));
      this.report.set(report);
      this.loading.set(false);
      void this.loadPhotos(report);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.error.set(this.translate.instant('reports.detail.not_found'));
      } else {
        this.error.set(this.translate.instant('error.internal.error'));
      }
      this.loading.set(false);
    }
  }

  private async loadPhotos(report: ReportDetail): Promise<void> {
    const displayPhotos: DisplayPhoto[] = report.photos.map((photo) => ({
      id: photo.id,
      url: photo.thumbnailUrl ?? null,
      loading: !photo.thumbnailUrl,
    }));
    this.photos.set(displayPhotos);

    await Promise.all(
      displayPhotos.map(async (photo) => {
        if (photo.url) {
          return;
        }

        try {
          const presign = await firstValueFrom(
            this.uploadService.getPresignedUrl(photo.id),
          );
          this.updatePhoto(photo.id, { url: presign.url, loading: false });
        } catch {
          this.updatePhoto(photo.id, { loading: false });
        }
      }),
    );
  }

  private updatePhoto(
    photoId: string,
    patch: Partial<Pick<DisplayPhoto, 'url' | 'loading'>>,
  ): void {
    this.photos.update((current) =>
      current.map((item) =>
        item.id === photoId ? { ...item, ...patch } : item,
      ),
    );
  }

  private parseError(error: unknown): string {
    if (error instanceof HttpErrorResponse) {
      const apiError = error.error as ApiErrorBody | null;
      if (apiError?.code) {
        return this.apiErrors.summary(apiError);
      }
    }

    return this.translate.instant('error.internal.error');
  }
}
