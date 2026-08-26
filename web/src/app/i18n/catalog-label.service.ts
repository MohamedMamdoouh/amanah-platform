import { inject, Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

@Injectable({ providedIn: 'root' })
export class CatalogLabelService {
  private readonly translate = inject(TranslateService);

  category(code: string): string {
    return this.translateWithFallback(`category.${code}`, code);
  }

  field(categoryCode: string, fieldKey: string): string {
    return this.translateWithFallback(
      `category.${categoryCode}.fields.${fieldKey}`,
      fieldKey,
    );
  }

  fieldHint(categoryCode: string, fieldKey: string): string | null {
    const key = `category.${categoryCode}.fields.${fieldKey}.hint`;
    const translated = this.translate.instant(key);
    return translated === key ? null : translated;
  }

  governorate(code: string): string {
    return this.translateWithFallback(`governorate.${code}`, code);
  }

  rejectionReason(code: string): string {
    return this.translateWithFallback(`rejection.${code}`, code);
  }

  private translateWithFallback(key: string, fallback: string): string {
    const translated = this.translate.instant(key);
    return translated === key ? fallback : translated;
  }
}
