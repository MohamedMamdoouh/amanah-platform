import { HttpErrorResponse } from '@angular/common/http';
import { AppDatePipe } from '../../i18n/app-date.pipe';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorService } from '../../i18n/api-error.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { ReportDossierComponent } from '../../shared/ui/report-dossier/report-dossier.component';
import { ReportTypeMarkComponent } from '../../shared/ui/report-type-mark/report-type-mark.component';
import { ReportDetail } from '../../reports/models/report.models';
import {
  DisplayPhoto,
  initialDisplayPhotos,
  loadDisplayPhotos,
} from '../../uploads/photo-loader.util';
import { ReportPhotoUploadService } from '../../uploads/report-photo-upload.service';
import { AdminModerationService } from '../admin-moderation.service';

const REJECTION_REASON_CODES = [
  'rejection.unclear_photos',
  'rejection.spam_or_scam',
  'rejection.duplicate_report',
  'rejection.insufficient_description',
  'rejection.contact_info',
  'rejection.prohibited_item',
  'rejection.wrong_category',
  'rejection.raw_id_number',
] as const;

@Component({
  selector: 'app-moderation-review',
  standalone: true,
  imports: [
    AppDatePipe,
    AlertComponent,
    ButtonComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    ReportDossierComponent,
    ReportTypeMarkComponent,
    TranslateModule,
  ],
  templateUrl: './moderation-review.component.html',
  styleUrl: './moderation-review.component.scss',
})
export class ModerationReviewComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly moderationService = inject(AdminModerationService);
  private readonly uploadService = inject(ReportPhotoUploadService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly report = signal<ReportDetail | null>(null);
  readonly photos = signal<DisplayPhoto[]>([]);
  readonly showReject = signal(false);
  readonly actionError = signal<string | null>(null);
  readonly approving = signal(false);
  readonly rejecting = signal(false);
  readonly doneMessage = signal<string | null>(null);

  readonly rejectionReasons = REJECTION_REASON_CODES;

  readonly rejectForm = this.fb.nonNullable.group({
    reasonCode: ['', Validators.required],
    note: [''],
  });

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (!id) {
      this.error.set(this.translate.instant('error.internal.error'));
      this.loading.set(false);
      return;
    }

    void this.loadReport(id);
  }

  reasonLabel(code: string): string {
    return this.translate.instant(code);
  }

  openReject(): void {
    this.actionError.set(null);
    this.showReject.set(true);
  }

  closeReject(): void {
    this.showReject.set(false);
    this.rejectForm.reset();
  }

  async approveReport(): Promise<void> {
    const report = this.report();
    if (!report || this.approving()) {
      return;
    }

    this.approving.set(true);
    this.actionError.set(null);

    try {
      await firstValueFrom(this.moderationService.approve(report.id));
      this.doneMessage.set(
        this.translate.instant('admin.moderation.done_approved'),
      );
      await this.router.navigate(['/admin/moderation']);
    } catch (error) {
      this.actionError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.approving.set(false);
    }
  }

  async submitReject(): Promise<void> {
    const report = this.report();
    if (!report || this.rejectForm.invalid || this.rejecting()) {
      return;
    }

    this.rejecting.set(true);
    this.actionError.set(null);

    try {
      await firstValueFrom(
        this.moderationService.reject(report.id, {
          reasonCode: this.rejectForm.controls.reasonCode.value,
          note: this.rejectForm.controls.note.value || null,
        }),
      );
      this.doneMessage.set(
        this.translate.instant('admin.moderation.done_rejected'),
      );
      await this.router.navigate(['/admin/moderation']);
    } catch (error) {
      this.actionError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.rejecting.set(false);
    }
  }

  private async loadReport(id: string): Promise<void> {
    try {
      const report = await firstValueFrom(this.moderationService.getReport(id));
      this.report.set(report);
      void this.loadPhotos(report);
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.error.set(this.translate.instant('reports.detail.not_found'));
      } else {
        this.error.set(this.translate.instant('error.internal.error'));
      }
    } finally {
      this.loading.set(false);
    }
  }

  private async loadPhotos(report: ReportDetail): Promise<void> {
    const displayPhotos = initialDisplayPhotos(report.photos);
    this.photos.set(displayPhotos);
    await loadDisplayPhotos(displayPhotos, this.uploadService, (photoId, patch) =>
      this.updatePhoto(photoId, patch),
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
}
