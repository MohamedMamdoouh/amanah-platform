import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorBody, ApiErrorService } from '../../i18n/api-error.service';
import { ReportType } from '../../reports/models/report.models';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
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
    ReactiveFormsModule,
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

  readonly reportId = input.required<string>();
  readonly reportType = input.required<ReportType>();

  readonly submitting = signal(false);
  readonly submitted = signal(false);
  readonly submittedId = signal<string | null>(null);
  readonly summaryError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});

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
        this.claimService.submit(this.reportId(), {
          submittedAnswer: answer,
        }),
      );
      this.submittedId.set(response.id);
      this.submitted.set(true);
    } catch (error) {
      this.handleError(error);
    } finally {
      this.submitting.set(false);
    }
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
}
