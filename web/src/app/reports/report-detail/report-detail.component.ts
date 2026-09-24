import { HttpErrorResponse } from '@angular/common/http';
import { AppDatePipe } from '../../i18n/app-date.pipe';
import {
  Component,
  DestroyRef,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { CatalogService } from '../../catalog/catalog.service';
import { Category } from '../../catalog/models/catalog.models';
import { ClaimResolutionActionsComponent } from '../../claims/claim-resolution-actions/claim-resolution-actions.component';
import { ClaimService } from '../../claims/claim.service';
import { ReportClaimsSectionComponent } from '../../claims/report-claims-section/report-claims-section.component';
import {
  loadReporterApprovedClaimId,
  showResolutionActions,
} from '../../claims/resolution/resolution.helpers';
import { ApiErrorService } from '../../i18n/api-error.service';
import { clientControlError } from '../../i18n/form-validation';
import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { PhotoLightboxComponent } from '../../shared/ui/photo-lightbox/photo-lightbox.component';
import { ReportDossierComponent } from '../../shared/ui/report-dossier/report-dossier.component';
import { ReportTypeMarkComponent } from '../../shared/ui/report-type-mark/report-type-mark.component';
import { SpinnerComponent } from '../../shared/ui/spinner/spinner.component';
import {
  DisplayPhoto,
  initialDisplayPhotos,
  loadDisplayPhotos,
} from '../../uploads/photo-loader.util';
import { ReportPhotoUploadService } from '../../uploads/report-photo-upload.service';
import {
  ReportDetail,
  WithdrawalReason,
} from '../models/report.models';
import { PhotoUploadComponent } from '../photo-upload/photo-upload.component';
import { ReportService } from '../report.service';
import {
  buildCategoryFieldsGroup,
  buildUpdateReportRequest,
} from '../shared/report-form.helpers';

@Component({
  selector: 'app-report-detail',
  standalone: true,
  imports: [
    AppDatePipe,
    ButtonComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    PhotoLightboxComponent,
    ReactiveFormsModule,
    ReportDossierComponent,
    SpinnerComponent,
    TranslateModule,
    PhotoUploadComponent,
    ClaimResolutionActionsComponent,
    ReportClaimsSectionComponent,
    ReportTypeMarkComponent,
  ],
  templateUrl: './report-detail.component.html',
  styleUrl: './report-detail.component.scss',
})
export class ReportDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly reportService = inject(ReportService);
  private readonly claimService = inject(ClaimService);
  private readonly catalogService = inject(CatalogService);
  private readonly uploadService = inject(ReportPhotoUploadService);
  private readonly catalogLabels = inject(CatalogLabelService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly report = signal<ReportDetail | null>(null);
  readonly photos = signal<DisplayPhoto[]>([]);
  readonly lightboxUrl = signal<string | null>(null);
  readonly showWithdraw = signal(false);
  readonly withdrawing = signal(false);
  readonly withdrawError = signal<string | null>(null);
  readonly withdrawn = signal(false);
  readonly catalogLoading = signal(false);
  readonly resubmitting = signal(false);
  readonly resubmitError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly approvedClaimId = signal<string | null>(null);

  readonly categories = signal<Category[]>([]);
  readonly governorates = signal<{ code: string; sortOrder: number }[]>([]);
  readonly selectedCategory = signal<Category | null>(null);
  readonly selectedPhotos = signal<File[]>([]);

  readonly withdrawForm = this.fb.nonNullable.group({
    reason: ['' as WithdrawalReason | '', Validators.required],
  });

  readonly editForm = this.fb.nonNullable.group({
    categoryCode: ['', Validators.required],
    title: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(80)]],
    description: ['', [Validators.required, Validators.minLength(20), Validators.maxLength(1000)]],
    dateLostOrFound: ['', Validators.required],
    governorateCode: ['', Validators.required],
    areaText: ['', Validators.maxLength(120)],
    heldLocation: ['', Validators.maxLength(120)],
    hasReward: [false],
    rewardAmount: [null as number | null],
    categoryFields: this.fb.group({}),
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

    this.editForm.controls.hasReward.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((hasReward) => this.updateRewardValidators(hasReward));

    this.editForm.controls.categoryCode.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((code) => this.onCategoryChanged(code));

    void this.loadReport(id);
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  fieldLabel(fieldKey: string): string {
    const categoryCode = this.canEdit()
      ? this.editForm.controls.categoryCode.value
      : this.report()?.categoryCode ?? '';
    return this.catalogLabels.field(categoryCode, fieldKey);
  }

  fieldHint(fieldKey: string): string | null {
    const categoryCode = this.editForm.controls.categoryCode.value;
    return this.catalogLabels.fieldHint(categoryCode, fieldKey);
  }

  rejectionReasonLabel(code: string | null | undefined): string {
    if (!code) {
      return '';
    }
    return this.translate.instant(code);
  }

  withdrawalReasonLabel(reason: WithdrawalReason): string {
    return this.translate.instant(`reports.withdraw.reasons.${reason}`);
  }

  canWithdraw(): boolean {
    const status = this.report()?.status;
    return (
      (status === 'pending_review' || status === 'published') &&
      !this.withdrawn()
    );
  }

  withdrawBlockedByClaim(): boolean {
    return this.report()?.status === 'claim_in_progress';
  }

  canEdit(): boolean {
    return this.report()?.status === 'rejected';
  }

  showClaimsSection(): boolean {
    const status = this.report()?.status;
    return status === 'published' || status === 'claim_in_progress';
  }

  showResolutionActions(): boolean {
    const report = this.report();
    if (!report) {
      return false;
    }

    return showResolutionActions({
      reportStatus: report.status,
      approvedClaimId: this.approvedClaimId(),
    });
  }

  canReviewClaims(): boolean {
    return this.report()?.status === 'published';
  }

  async onClaimReviewed(): Promise<void> {
    const report = this.report();
    if (!report) {
      return;
    }

    await this.loadReport(report.id);
  }

  async onResolutionChanged(): Promise<void> {
    const report = this.report();
    if (!report) {
      return;
    }

    await this.loadReport(report.id);
  }

  isFound(): boolean {
    return this.report()?.type === 'found';
  }

  fieldDefinitions() {
    return [...(this.selectedCategory()?.fieldDefinitions ?? [])].sort(
      (a, b) => a.sortOrder - b.sortOrder,
    );
  }

  categoryFieldsGroup(): FormGroup {
    return this.editForm.controls.categoryFields;
  }

  fieldError(name: string): string | null {
    const apiError = this.fieldErrors()[name]?.[0];
    if (apiError) {
      return apiError;
    }

    return clientControlError(this.editForm.get(name), this.translate);
  }

  categoryFieldError(fieldKey: string): string | null {
    return this.fieldError(fieldKey) ?? this.fieldError(`categoryFields.${fieldKey}`);
  }

  photosFieldError(): string | null {
    const direct = this.fieldError('photos');
    if (direct) {
      return direct;
    }

    for (const [key, messages] of Object.entries(this.fieldErrors())) {
      if (key.startsWith('photos[') && messages[0]) {
        return messages[0];
      }
    }

    return null;
  }

  onPhotosChange(photos: File[]): void {
    this.selectedPhotos.set(photos);
  }

  openPhoto(url: string): void {
    this.lightboxUrl.set(url);
  }

  closePhoto(): void {
    this.lightboxUrl.set(null);
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
      this.withdrawError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.withdrawing.set(false);
    }
  }

  async resubmit(): Promise<void> {
    const report = this.report();
    if (!report || this.editForm.invalid || this.resubmitting()) {
      this.editForm.markAllAsTouched();
      this.resubmitError.set(
        this.translate.instant('common.form.validation_summary'),
      );
      return;
    }

    this.resubmitting.set(true);
    this.resubmitError.set(null);
    this.fieldErrors.set({});

    const request = buildUpdateReportRequest(report.type, this.editForm.getRawValue());

    try {
      await firstValueFrom(
        this.reportService.update(report.id, request, this.selectedPhotos()),
      );
      await firstValueFrom(this.reportService.resubmit(report.id));
      await this.router.navigate(['/my/reports']);
    } catch (error) {
      this.resubmitError.set(this.apiErrors.messageFromHttpError(error));
      this.fieldErrors.set(this.apiErrors.formErrorsFromHttpError(error));
    } finally {
      this.resubmitting.set(false);
    }
  }

  private scrollToClaimsSectionIfNeeded(): void {
    if (this.route.snapshot.fragment !== 'claims-section') {
      return;
    }

    queueMicrotask(() => {
      document.getElementById('claims-section')?.scrollIntoView({
        behavior: 'smooth',
        block: 'start',
      });
    });
  }

  private async loadReport(id: string): Promise<void> {
    try {
      const report = await firstValueFrom(this.reportService.getById(id));
      this.report.set(report);
      this.approvedClaimId.set(
        await loadReporterApprovedClaimId(
          this.claimService,
          id,
          report.status,
        ),
      );
      this.loading.set(false);

      if (report.status === 'rejected') {
        await this.loadCatalog();
        this.populateEditForm(report);
      }

      void this.loadPhotos(report);
      this.scrollToClaimsSectionIfNeeded();
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 404) {
        this.error.set(this.translate.instant('reports.detail.not_found'));
      } else {
        this.error.set(this.translate.instant('error.internal.error'));
      }
      this.loading.set(false);
    }
  }

  private async loadCatalog(): Promise<void> {
    this.catalogLoading.set(true);

    try {
      const [categories, governorates] = await Promise.all([
        firstValueFrom(this.catalogService.getCategories()),
        firstValueFrom(this.catalogService.getGovernorates()),
      ]);

      this.categories.set(
        [...categories.items].sort((a, b) => a.sortOrder - b.sortOrder),
      );
      this.governorates.set(
        [...governorates.items].sort((a, b) => a.sortOrder - b.sortOrder),
      );
    } catch {
      this.resubmitError.set(this.translate.instant('error.internal.error'));
    } finally {
      this.catalogLoading.set(false);
    }
  }

  private populateEditForm(report: ReportDetail): void {
    if (report.type === 'found') {
      this.editForm.controls.heldLocation.setValidators([
        Validators.required,
        Validators.maxLength(120),
      ]);
    } else {
      this.editForm.controls.heldLocation.clearValidators();
    }
    this.editForm.controls.heldLocation.updateValueAndValidity();

    this.editForm.patchValue({
      categoryCode: report.categoryCode,
      title: report.title,
      description: report.description,
      dateLostOrFound: report.dateLostOrFound,
      governorateCode: report.governorateCode,
      areaText: report.areaText ?? '',
      heldLocation: report.heldLocation ?? '',
      hasReward: report.hasReward,
      rewardAmount: report.rewardAmount ?? null,
    });

    this.updateRewardValidators(report.hasReward);
    this.onCategoryChanged(report.categoryCode, report.categoryFields);
  }

  private onCategoryChanged(
    code: string,
    existingValues: Record<string, string> = {},
  ): void {
    const category = this.categories().find((item) => item.code === code) ?? null;
    this.selectedCategory.set(category);
    this.editForm.setControl(
      'categoryFields',
      buildCategoryFieldsGroup(this.fb, category, existingValues),
    );
  }

  private updateRewardValidators(hasReward: boolean): void {
    const control = this.editForm.controls.rewardAmount;
    if (hasReward) {
      control.setValidators([
        Validators.required,
        Validators.min(50),
        Validators.max(50_000),
      ]);
    } else {
      control.clearValidators();
      control.setValue(null);
    }
    control.updateValueAndValidity();
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
