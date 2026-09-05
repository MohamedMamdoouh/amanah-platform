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
import { ActivatedRoute, RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { CatalogService } from '../../catalog/catalog.service';
import { Category, CategoryFieldDefinition } from '../../catalog/models/catalog.models';
import { ApiErrorBody, ApiErrorService } from '../../i18n/api-error.service';
import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { CreateReportRequest, ReportType } from '../models/report.models';
import { PhotoUploadComponent } from '../photo-upload/photo-upload.component';
import { ReportService } from '../report.service';

@Component({
  selector: 'app-report-form',
  standalone: true,
  imports: [
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
    title: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(80)]],
    description: ['', [Validators.required, Validators.minLength(20), Validators.maxLength(1000)]],
    dateLostOrFound: [this.today, Validators.required],
    governorateCode: ['', Validators.required],
    areaText: ['', Validators.maxLength(120)],
    heldLocation: ['', Validators.maxLength(120)],
    hasReward: [false],
    rewardAmount: [null as number | null],
    hiddenDetail: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]],
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

  fieldDefinitions(): CategoryFieldDefinition[] {
    return [...(this.selectedCategory()?.fieldDefinitions ?? [])].sort(
      (a, b) => a.sortOrder - b.sortOrder,
    );
  }

  categoryFieldsGroup(): FormGroup {
    return this.form.controls.categoryFields;
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
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    const value = this.form.getRawValue();
    const categoryFields: Record<string, string> = {};
    for (const [key, fieldValue] of Object.entries(value.categoryFields)) {
      const serialized = categoryFieldToString(fieldValue);
      if (serialized !== null) {
        categoryFields[key] = serialized;
      }
    }

    const request: CreateReportRequest = {
      type: this.reportType(),
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

    try {
      const response = await firstValueFrom(
        this.reportService.create(request, this.selectedPhotos()),
      );
      this.submittedId.set(response.id);
      this.submitted.set(true);
    } catch (error) {
      this.handleError(error);
    } finally {
      this.submitting.set(false);
    }
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
    const category = this.categories().find((item) => item.code === code) ?? null;
    this.selectedCategory.set(category);
    this.rebuildCategoryFields(category);
  }

  private rebuildCategoryFields(category: Category | null): void {
    const group = this.fb.group({});

    for (const definition of category?.fieldDefinitions ?? []) {
      const validators = this.buildFieldValidators(definition);
      group.addControl(definition.fieldKey, this.fb.control('', validators));
    }

    this.form.setControl('categoryFields', group);
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

  private handleError(error: unknown): void {
    if (!(error instanceof HttpErrorResponse)) {
      this.summaryError.set(this.translate.instant('error.internal.error'));
      return;
    }

    const apiError = error.error as ApiErrorBody | null;
    if (!apiError?.code) {
      this.summaryError.set(this.translate.instant('error.internal.error'));
      return;
    }

    this.summaryError.set(this.apiErrors.summary(apiError));
    this.fieldErrors.set(this.apiErrors.fieldErrors(apiError));
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

// Angular NumberValueAccessor stores <input type="number"> as a number.
function categoryFieldToString(raw: unknown): string | null {
  if (typeof raw === 'number' && Number.isFinite(raw)) {
    return String(raw);
  }

  if (typeof raw === 'string' && raw.trim().length > 0) {
    return raw.trim();
  }

  return null;
}
