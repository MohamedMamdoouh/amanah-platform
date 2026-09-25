import { Pipe, PipeTransform } from '@angular/core';

const LOCALE = 'ar-EG';

const dateTimeFormat = new Intl.DateTimeFormat(LOCALE, {
  numberingSystem: 'latn',
  dateStyle: 'short',
  timeStyle: 'short',
});

const dateFormat = new Intl.DateTimeFormat(LOCALE, {
  numberingSystem: 'latn',
  dateStyle: 'short',
});

const DATE_ONLY = /^\d{4}-\d{2}-\d{2}$/;

@Pipe({
  name: 'appDate',
  standalone: true,
  pure: true,
})
export class AppDatePipe implements PipeTransform {
  transform(value: Date | string | number | null | undefined): string | null {
    if (value == null || value === '') {
      return null;
    }

    const dateOnly = typeof value === 'string' && DATE_ONLY.test(value);
    const date = new Date(dateOnly ? `${value}T00:00:00` : value);
    if (Number.isNaN(date.getTime())) {
      return null;
    }

    return (dateOnly ? dateFormat : dateTimeFormat).format(date);
  }
}
