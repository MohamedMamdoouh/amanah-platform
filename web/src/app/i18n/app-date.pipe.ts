import { formatDate, registerLocaleData } from '@angular/common';
import localeArEg from '@angular/common/locales/ar-EG';
import { Pipe, PipeTransform } from '@angular/core';

registerLocaleData(localeArEg);

export const APP_LOCALE = 'ar-EG';

export const APP_TIMEZONE = 'Africa/Cairo';

/** Eastern Arabic-Indic (٠-٩) and Extended Arabic-Indic (۰-۹) → Latin 0-9. */
export function toLatinDigits(value: string): string {
  return value.replace(/[٠-٩۰-۹]/g, (digit) => {
    const code = digit.charCodeAt(0);
    if (code >= 0x0660 && code <= 0x0669) {
      return String(code - 0x0660);
    }
    return String(code - 0x06f0);
  });
}

@Pipe({
  name: 'appDate',
  standalone: true,
  pure: true,
})
export class AppDatePipe implements PipeTransform {
  transform(
    value: Date | string | number | null | undefined,
    format = 'medium',
  ): string | null {
    if (value == null || value === '') {
      return null;
    }

    try {
      const normalized =
        typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)
          ? `${value}T12:00:00`
          : value;

      return toLatinDigits(
        formatDate(normalized, format, APP_LOCALE, APP_TIMEZONE),
      );
    } catch {
      return null;
    }
  }
}
