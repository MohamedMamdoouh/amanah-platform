import { Component, inject, input, signal, viewChild } from '@angular/core';
import { RouterLink } from '@angular/router';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorService } from '../../i18n/api-error.service';
import { ReportType } from '../../reports/models/report.models';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { PhotoUploadComponent } from '../../reports/photo-upload/photo-upload.component';
import { ClaimService } from '../claim.service';

const MIN_LENGTH = 10;
const MAX_LENGTH = 500;

@Component({
  selector: 'app-claim-form',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    EmptyStateComponent,
    PhotoUploadComponent,
    ReactiveFormsModule,
    RouterLink,
    TranslateModule,
  ],
  templateUrl: './claim-form.component.html',
  styleUrl: './claim-form.component.scss',
})
export class ClaimFormComponent {
  private readonly fb = inject(FormBuilder);
  private readonly claimService = inject(ClaimService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);
  private readonly photoUpload = viewChild(PhotoUploadComponent);

  readonly reportId = input.required<string>();
  readonly reportType = input.required<ReportType>();

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly submittedId = signal<string | null>(null);
  readonly summaryError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly selectedPhoto = signal<File | null>(null);

  readonly form = this.fb.nonNullable.group({
    submittedAnswer: [
      '',
      [
        Validators.required,
        Validators.minLength(MIN_LENGTH),
        Validators.maxLength(MAX_LENGTH),
      ],
    ],
  });

  isFound(): boolean {
    return this.reportType() === 'found';
  }

  promptKey(): string {
    return this.isFound()
      ? 'claims.form.prompt_found'
      : 'claims.form.prompt_lost';
  }

  photoFieldError(): string | null {
    return this.fieldErrors()['photo']?.[0] ?? null;
  }

  onPhotoChange(photos: File[]): void {
    this.selectedPhoto.set(photos[0] ?? null);
  }

  fieldError(name: string): string | null {
    const apiError = this.fieldErrors()[name]?.[0];
    if (apiError) {
      return apiError;
    }

    const control = this.form.get(name);
    if (!control?.touched && !control?.dirty) {
      return null;
    }

    if (control.hasError('required')) {
      return this.translate.instant('claims.form.answer_required');
    }

    if (control.hasError('minlength')) {
      return this.translate.instant('claims.form.answer_min_length', {
        min: MIN_LENGTH,
      });
    }

    if (control.hasError('maxlength')) {
      return this.translate.instant('claims.form.answer_max_length', {
        max: MAX_LENGTH,
      });
    }

    return null;
  }

  async submit(): Promise<void> {
    if (this.form.invalid || this.submitting()) {
      this.form.markAllAsTouched();
      return;
    }

    this.clearErrors();
    this.submitting.set(true);

    const answer = this.form.controls.submittedAnswer.value.trim();

    try {
      const response = await firstValueFrom(
        this.claimService.submit(
          this.reportId(),
          { submittedAnswer: answer },
          this.selectedPhoto() ?? undefined,
        ),
      );
      this.submittedId.set(response.id);
      this.submitted.set(true);
    } catch (error) {
      this.summaryError.set(this.apiErrors.messageFromHttpError(error));
      const errors = this.apiErrors.formErrorsFromHttpError(error);
      this.fieldErrors.set(errors);

      if (errors['photo']) {
        this.selectedPhoto.set(null);
        this.photoUpload()?.clear();
      }
    } finally {
      this.submitting.set(false);
    }
  }

  private clearErrors(): void {
    this.summaryError.set(null);
    this.fieldErrors.set({});
  }
}
