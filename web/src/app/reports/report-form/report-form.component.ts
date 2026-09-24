import { Component, DestroyRef, inject, OnInit, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { CatalogService } from '../../catalog/catalog.service';
import { Category } from '../../catalog/models/catalog.models';
import { ApiErrorService } from '../../i18n/api-error.service';
import { CatalogLabelService } from '../../i18n/catalog-label.service';
import {
  clientControlError,
  validationSummaryMessage,
} from '../../i18n/form-validation';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { ReportType } from '../models/report.models';
import { PhotoUploadComponent } from '../photo-upload/photo-upload.component';
import { ReportService } from '../report.service';
import {
  buildCategoryFieldsGroup,
  buildCreateReportRequest,
} from '../shared/report-form.helpers';

@Component({
  selector: 'app-report-form',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    EmptyStateComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    TranslateModule,
    PhotoUploadComponent,
  ],
  templateUrl: './report-form.component.html',
  styleUrl: './report-form.component.scss',
})
export class ReportFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly fb = inject(FormBuilder);
  private readonly catalogService = inject(CatalogService);
  private readonly reportService = inject(ReportService);
  private readonly catalogLabels = inject(CatalogLabelService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  readonly reportType = signal<ReportType>('lost');
  readonly loading = signal(true);
  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly submittedId = signal<string | null>(null);
  readonly summaryError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});

  readonly categories = signal<Category[]>([]);
  readonly governorates = signal<{ code: string; sortOrder: number }[]>([]);
  readonly selectedCategory = signal<Category | null>(null);
  readonly selectedPhotos = signal<File[]>([]);
  readonly today = this.formatDate(new Date());

  readonly form = this.fb.nonNullable.group({
    categoryCode: ['', Validators.required],
    title: [
      '',
      [Validators.required, Validators.minLength(10), Validators.maxLength(80)],
    ],
    description: [
      '',
      [
        Validators.required,
        Validators.minLength(20),
        Validators.maxLength(1000),
      ],
    ],
    dateLostOrFound: [this.today, Validators.required],
    governorateCode: ['', Validators.required],
    areaText: ['', Validators.maxLength(120)],
    heldLocation: ['', Validators.maxLength(120)],
    hasReward: [false],
    rewardAmount: [null as number | null],
    categoryFields: this.fb.group({}),
  });

  ngOnInit(): void {
    const type = this.route.snapshot.data['type'] as ReportType;
    this.reportType.set(type);

    if (type === 'found') {
      this.form.controls.heldLocation.setValidators([
        Validators.required,
        Validators.maxLength(120),
      ]);
    } else {
      this.form.controls.heldLocation.clearValidators();
      this.form.controls.heldLocation.setValue('');
    }
    this.form.controls.heldLocation.updateValueAndValidity();

    this.form.controls.hasReward.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((hasReward) => this.updateRewardValidators(hasReward));

    this.form.controls.categoryCode.valueChanges
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe((code) => this.onCategoryChanged(code));

    void this.loadCatalog();
  }

  isFound(): boolean {
    return this.reportType() === 'found';
  }

  fieldDefinitions() {
    return [...(this.selectedCategory()?.fieldDefinitions ?? [])].sort(
      (a, b) => a.sortOrder - b.sortOrder,
    );
  }

  categoryFieldsGroup(): FormGroup {
    return this.form.controls.categoryFields;
  }

  fieldError(name: string): string | null {
    const apiError = this.fieldErrors()[name]?.[0];
    if (apiError) {
      return apiError;
    }

    return this.clientControlError(name);
  }

  categoryFieldError(fieldKey: string): string | null {
    return (
      this.fieldError(fieldKey) ?? this.fieldError(`categoryFields.${fieldKey}`)
    );
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

  isControlInvalid(name: string): boolean {
    const control = this.form.get(name);
    return !!control && control.invalid && (control.touched || control.dirty);
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  fieldLabel(fieldKey: string): string {
    const categoryCode = this.form.controls.categoryCode.value;
    return this.catalogLabels.field(categoryCode, fieldKey);
  }

  fieldHint(fieldKey: string): string | null {
    const categoryCode = this.form.controls.categoryCode.value;
    return this.catalogLabels.fieldHint(categoryCode, fieldKey);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  onPhotosChange(photos: File[]): void {
    this.selectedPhotos.set(photos);
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      this.summaryError.set(validationSummaryMessage(this.translate));
      this.scrollToSummary();
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    const request = buildCreateReportRequest(
      this.reportType(),
      this.form.getRawValue(),
    );

    try {
      const response = await firstValueFrom(
        this.reportService.create(request, this.selectedPhotos()),
      );
      this.submittedId.set(response.id);
      this.submitted.set(true);
    } catch (error) {
      this.summaryError.set(this.apiErrors.messageFromHttpError(error));
      this.fieldErrors.set(this.apiErrors.formErrorsFromHttpError(error));
      this.scrollToSummary();
    } finally {
      this.submitting.set(false);
    }
  }

  private clientControlError(path: string): string | null {
    return clientControlError(this.form.get(path), this.translate);
  }

  private scrollToSummary(): void {
    queueMicrotask(() => {
      document
        .querySelector('.report-page app-alert, .report-form .form-error')
        ?.scrollIntoView({ behavior: 'smooth', block: 'center' });
    });
  }

  private async loadCatalog(): Promise<void> {
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
      this.summaryError.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }

  private onCategoryChanged(code: string): void {
    const category =
      this.categories().find((item) => item.code === code) ?? null;
    this.selectedCategory.set(category);
    this.form.setControl(
      'categoryFields',
      buildCategoryFieldsGroup(this.fb, category),
    );
  }

  private updateRewardValidators(hasReward: boolean): void {
    const control = this.form.controls.rewardAmount;
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

  private clearErrors(): void {
    this.summaryError.set(null);
    this.fieldErrors.set({});
  }

  private formatDate(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }
}
