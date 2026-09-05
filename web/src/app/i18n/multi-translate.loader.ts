import { HttpClient } from '@angular/common/http';
import { TranslateLoader } from '@ngx-translate/core';
import { forkJoin, map, Observable } from 'rxjs';

const translationFiles = [
  'common',
  'categories',
  'governorates',
  'errors',
  'rejection-reasons',
  'admin-moderation',
  'notifications',
  'pages',
  'reports',
];

export class MultiTranslateHttpLoader implements TranslateLoader {
  constructor(
    private readonly http: HttpClient,
    private readonly prefix = './assets/i18n/ar/',
    private readonly suffix = '.json',
  ) {}

  getTranslation(_lang: string): Observable<Record<string, string>> {
    return forkJoin(
      translationFiles.map((file) =>
        this.http.get<Record<string, string>>(
          `${this.prefix}${file}${this.suffix}`,
        ),
      ),
    ).pipe(map((parts) => Object.assign({}, ...parts)));
  }
}

export function multiTranslateLoaderFactory(
  http: HttpClient,
): MultiTranslateHttpLoader {
  return new MultiTranslateHttpLoader(http);
}
