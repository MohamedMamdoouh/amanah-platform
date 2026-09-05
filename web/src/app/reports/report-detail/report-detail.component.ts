import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
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
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { CatalogService } from '../../catalog/catalog.service';
import { Category, CategoryFieldDefinition } from '../../catalog/models/catalog.models';
import { ApiErrorBody, ApiErrorService } from '../../i18n/api-error.service';
import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { ReportPhotoUploadService } from '../../uploads/report-photo-upload.service';
import {
  ReportDetail,
  UpdateReportRequest,
  WithdrawalReason,
} from '../models/report.models';
import { PhotoUploadComponent } from '../photo-upload/photo-upload.component';
import { ReportService } from '../report.service';

interface DisplayPhoto {
  id: string;
  url: string | null;
  loading: boolean;
}

@Component({
  selector: 'app-report-detail',
  standalone: true,
  imports: [
    DatePipe,
    ReactiveFormsModule,
    RouterLink,
    TranslateModule,
    PhotoUploadComponent,
  ],
  templateUrl: './report-detail.component.html',
  styleUrl: './report-detail.component.scss',
})
export class ReportDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly fb = inject(FormBuilder);
  private readonly reportService = inject(ReportService);
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
  readonly showWithdraw = signal(false);
  readonly withdrawing = signal(false);
  readonly withdrawError = signal<string | null>(null);
  readonly withdrawn = signal(false);
  readonly catalogLoading = signal(false);
  readonly resubmitting = signal(false);
  readonly resubmitError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});

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
    hiddenDetail: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
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

  typeLabel(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  statusLabel(status: string): string {
    return this.translate.instant(`reports.status.${status}`);
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

  canEdit(): boolean {
    return this.report()?.status === 'rejected';
  }

  isFound(): boolean {
    return this.report()?.type === 'found';
  }

  fieldDefinitions(): CategoryFieldDefinition[] {
    return [...(this.selectedCategory()?.fieldDefinitions ?? [])].sort(
      (a, b) => a.sortOrder - b.sortOrder,
    );
  }

  categoryFieldsGroup(): FormGroup {
    return this.editForm.controls.categoryFields;
  }

  fieldError(name: string): string | null {
    const errors = this.fieldErrors()[name];
    return errors?.[0] ?? null;
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

  async resubmit(): Promise<void> {
    const report = this.report();
    if (!report || this.editForm.invalid || this.resubmitting()) {
      this.editForm.markAllAsTouched();
      return;
    }

    this.resubmitting.set(true);
    this.resubmitError.set(null);
    this.fieldErrors.set({});

    const request = this.buildUpdateRequest();

    try {
      await firstValueFrom(
        this.reportService.update(report.id, request, this.selectedPhotos()),
      );
      await firstValueFrom(this.reportService.resubmit(report.id));
      await this.router.navigate(['/my/reports']);
    } catch (error) {
      this.handleSubmitError(error);
    } finally {
      this.resubmitting.set(false);
    }
  }

  private async loadReport(id: string): Promise<void> {
    try {
      const report = await firstValueFrom(this.reportService.getById(id));
      this.report.set(report);
      this.loading.set(false);

      if (report.status === 'rejected') {
        await this.loadCatalog();
        this.populateEditForm(report);
      }

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
      hiddenDetail: report.hiddenDetail ?? '',
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
    this.rebuildCategoryFields(category, existingValues);
  }

  private rebuildCategoryFields(
    category: Category | null,
    existingValues: Record<string, string>,
  ): void {
    const group = this.fb.group({});

    for (const definition of category?.fieldDefinitions ?? []) {
      const validators = this.buildFieldValidators(definition);
      const existing = existingValues[definition.fieldKey] ?? '';
      group.addControl(definition.fieldKey, this.fb.control(existing, validators));
    }

    this.editForm.setControl('categoryFields', group);
  }

  private buildFieldValidators(definition: CategoryFieldDefinition) {
    const validators = [];

    if (definition.required) {
      validators.push(Validators.required);
    }

    if (definition.type === 'Text') {
      if (definition.minLength != null) {
        validators.push(Validators.minLength(definition.minLength));
      }
      if (definition.maxLength != null) {
        validators.push(Validators.maxLength(definition.maxLength));
      }
    }

    if (definition.type === 'Integer') {
      validators.push(Validators.pattern(/^-?\d+$/));
      if (definition.minInt != null) {
        validators.push(Validators.min(definition.minInt));
      }
      if (definition.maxInt != null) {
        validators.push(Validators.max(definition.maxInt));
      }
    }

    return validators;
  }

  private buildUpdateRequest(): UpdateReportRequest {
    const value = this.editForm.getRawValue();
    const categoryFields: Record<string, string> = {};

    for (const [key, fieldValue] of Object.entries(value.categoryFields)) {
      if (typeof fieldValue === 'string' && fieldValue.trim().length > 0) {
        categoryFields[key] = fieldValue.trim();
      }
    }

    return {
      categoryCode: value.categoryCode,
      title: value.title.trim(),
      description: value.description.trim(),
      dateLostOrFound: value.dateLostOrFound,
      governorateCode: value.governorateCode,
      areaText: value.areaText.trim() || null,
      heldLocation: this.isFound() ? value.heldLocation.trim() : null,
      hasReward: value.hasReward,
      rewardAmount: value.hasReward ? parseRewardAmount(value.rewardAmount) : null,
      hiddenDetail: value.hiddenDetail.trim(),
      categoryFields,
    };
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

  private handleSubmitError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.resubmitError.set(this.translate.instant('error.internal.error'));
      return;
    }

    const apiError = error.error as ApiErrorBody | null;
    if (!apiError?.code) {
      this.resubmitError.set(this.translate.instant('error.internal.error'));
      return;
    }

    this.resubmitError.set(this.apiErrors.summary(apiError));
    this.fieldErrors.set(this.apiErrors.fieldErrors(apiError));
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

function parseRewardAmount(raw: unknown): number | null {
  if (typeof raw === 'number' && Number.isFinite(raw)) {
    return Math.trunc(raw);
  }

  if (typeof raw === 'string' && raw.trim().length > 0) {
    const parsed = Number.parseInt(raw, 10);
    return Number.isFinite(parsed) ? parsed : null;
  }

  return null;
}
