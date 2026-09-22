import { DatePipe } from '@angular/common';
import { Component, inject, input, output, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorService } from '../i18n/api-error.service';
import { AlertComponent } from '../shared/ui/alert/alert.component';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { FormFieldComponent } from '../shared/ui/form-field/form-field.component';
import {
  ABUSE_FLAG_REASONS,
  AbuseFlagService,
  FlagListingResponse,
} from './abuse-flag.service';

const NOTE_MAX_LENGTH = 500;

@Component({
  selector: 'app-flag-listing-dialog',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    DatePipe,
    FormFieldComponent,
    ReactiveFormsModule,
    TranslateModule,
  ],
  templateUrl: './flag-listing-dialog.component.html',
  styleUrl: './flag-listing-dialog.component.scss',
})
export class FlagListingDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly abuseFlagService = inject(AbuseFlagService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);

  readonly reportId = input.required<string>();
  readonly existingFlag = input<FlagListingResponse | null>(null);

  readonly closed = output<void>();
  readonly submitted = output<FlagListingResponse>();

  readonly reasons = ABUSE_FLAG_REASONS;
  readonly submitting = signal(false);
  readonly summaryError = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    reason: ['', Validators.required],
    note: ['', Validators.maxLength(NOTE_MAX_LENGTH)],
  });

  isViewOnly(): boolean {
    return this.existingFlag() !== null;
  }

  reasonLabel(code: string): string {
    const key = code;
    const translated = this.translate.instant(key);
    return translated === key ? code : translated;
  }

  noteFieldError(): string | null {
    const control = this.form.controls.note;
    if (!control.touched && !control.dirty) {
      return null;
    }

    if (control.hasError('maxlength')) {
      return this.translate.instant('abuse.flag.note_max_length', {
        max: NOTE_MAX_LENGTH,
      });
    }

    return null;
  }

  reasonFieldError(): string | null {
    const control = this.form.controls.reason;
    if (!control.touched && !control.dirty) {
      return null;
    }

    if (control.hasError('required')) {
      return this.translate.instant('abuse.flag.reason_required');
    }

    return null;
  }

  onBackdropClick(): void {
    if (!this.submitting()) {
      this.closed.emit();
    }
  }

  onCancel(): void {
    if (!this.submitting()) {
      this.closed.emit();
    }
  }

  async onSubmit(): Promise<void> {
    if (this.isViewOnly()) {
      this.closed.emit();
      return;
    }

    this.form.markAllAsTouched();
    if (this.form.invalid) {
      return;
    }

    const { reason, note } = this.form.getRawValue();
    const selectedReason = ABUSE_FLAG_REASONS.find((code) => code === reason);
    if (!selectedReason) {
      return;
    }

    const trimmedNote = note.trim();

    this.submitting.set(true);
    this.summaryError.set(null);

    try {
      const response = await firstValueFrom(
        this.abuseFlagService.createFlag(this.reportId(), {
          reason: selectedReason,
          note: trimmedNote.length > 0 ? trimmedNote : null,
        }),
      );
      this.submitted.emit(response);
    } catch (error) {
      this.summaryError.set(
        this.apiErrors.messageFromHttpError(error, {
          conflictKey: 'abuse.flag.duplicate_conflict',
          fallbackKey: 'abuse.flag.submit_error',
        }),
      );
    } finally {
      this.submitting.set(false);
    }
  }
}
