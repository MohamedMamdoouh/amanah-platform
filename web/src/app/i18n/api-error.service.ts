import { inject, Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export interface ApiErrorBody {
  code: string;
  message: string;
  errors?: Record<string, string[]>;
}

@Injectable({ providedIn: 'root' })
export class ApiErrorService {
  private readonly translate = inject(TranslateService);

  summary(error: ApiErrorBody): string {
    return this.translateCode(error.code, error.message);
  }

  fieldErrors(error: ApiErrorBody): Record<string, string[]> {
    return error.errors ?? {};
  }

  private translateCode(code: string, fallback?: string): string {
    const key = `error.${code}`;
    const translated = this.translate.instant(key);
    return translated === key ? fallback ?? code : translated;
  }
}
