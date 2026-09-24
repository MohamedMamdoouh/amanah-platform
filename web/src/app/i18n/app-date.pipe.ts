import { Pipe, PipeTransform } from '@angular/core';

export const APP_LOCALE = 'ar-EG';

export const APP_TIMEZONE = 'Africa/Cairo';

const TIME_WITH_DAY_PERIOD: Intl.DateTimeFormatOptions = {
  hour: 'numeric',
  minute: '2-digit',
  hour12: true,
  dayPeriod: 'long',
};

/** Named formats → Intl options (Latin digits, Cairo TZ applied below). */
const FORMAT_OPTIONS: Record<string, Intl.DateTimeFormatOptions> = {
  short: {
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
    ...TIME_WITH_DAY_PERIOD,
  },
  medium: {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    ...TIME_WITH_DAY_PERIOD,
  },
  shortDate: {
    year: 'numeric',
    month: 'numeric',
    day: 'numeric',
  },
  mediumDate: {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  },
  shortTime: { ...TIME_WITH_DAY_PERIOD },
  mediumTime: { ...TIME_WITH_DAY_PERIOD },
};

const formatters = new Map<string, Intl.DateTimeFormat>();

function formatterFor(format: string): Intl.DateTimeFormat {
  const key = FORMAT_OPTIONS[format] ? format : 'medium';
  let formatter = formatters.get(key);
  if (!formatter) {
    formatter = new Intl.DateTimeFormat(APP_LOCALE, {
      ...FORMAT_OPTIONS[key],
      numberingSystem: 'latn',
      timeZone: APP_TIMEZONE,
    });
    formatters.set(key, formatter);
  }
  return formatter;
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

      return formatterFor(format).format(new Date(normalized));
    } catch {
      return null;
    }
  }
}
