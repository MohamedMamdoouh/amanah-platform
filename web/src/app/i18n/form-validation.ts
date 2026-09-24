import { AbstractControl } from '@angular/forms';
import { TranslateService } from '@ngx-translate/core';

export function clientControlError(
  control: AbstractControl | null | undefined,
  translate: TranslateService,
): string | null {
  if (!control || (!control.touched && !control.dirty)) {
    return null;
  }

  if (control.hasError('required')) {
    return translate.instant('common.form.field_required');
  }

  if (control.hasError('email')) {
    return translate.instant('common.form.field_email');
  }

  if (control.hasError('minlength')) {
    const min = control.getError('minlength')?.requiredLength as
      | number
      | undefined;
    return translate.instant('common.form.field_min_length', {
      min: min ?? '',
    });
  }

  if (control.hasError('maxlength')) {
    const max = control.getError('maxlength')?.requiredLength as
      | number
      | undefined;
    return translate.instant('common.form.field_max_length', {
      max: max ?? '',
    });
  }

  if (control.hasError('min')) {
    const min = control.getError('min')?.min as number | undefined;
    return translate.instant('common.form.field_min', { min: min ?? '' });
  }

  if (control.hasError('max')) {
    const max = control.getError('max')?.max as number | undefined;
    return translate.instant('common.form.field_max', { max: max ?? '' });
  }

  return null;
}

export function validationSummaryMessage(translate: TranslateService): string {
  return translate.instant('common.form.validation_summary');
}
