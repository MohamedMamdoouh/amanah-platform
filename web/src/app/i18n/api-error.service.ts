import { HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export interface ApiErrorBody {
  code: string;
  message: string;
  errors?: Record<string, string[]>;
}

export interface HttpErrorMessageOptions {
  conflictKey?: string;
  fallbackKey?: string;
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

  extractBody(error: unknown): ApiErrorBody | null {
    if (!(error instanceof HttpErrorResponse)) {
      return null;
    }

    const body = error.error as ApiErrorBody | null;
    return body?.code ? body : null;
  }

  messageFromHttpError(
    error: unknown,
    options: HttpErrorMessageOptions = {},
  ): string {
    const body = this.extractBody(error);
    if (body) {
      return this.summary(body);
    }

    const fallbackKey = options.fallbackKey ?? 'error.internal.error';

    if (
      error instanceof HttpErrorResponse &&
      error.status === 409 &&
      options.conflictKey
    ) {
      return this.translate.instant(options.conflictKey);
    }

    return this.translate.instant(fallbackKey);
  }

  formErrorsFromHttpError(error: unknown): Record<string, string[]> {
    const body = this.extractBody(error);
    return body ? this.fieldErrors(body) : {};
  }

  private translateCode(code: string, fallback?: string): string {
    const key = `error.${code}`;
    const translated = this.translate.instant(key);
    return translated === key ? fallback ?? code : translated;
  }
}
