import { inject, provideAppInitializer } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

export function provideI18nInitializer() {
  return provideAppInitializer(() => {
    const translate = inject(TranslateService);
    translate.setDefaultLang('ar');
    return translate.use('ar');
  });
}
