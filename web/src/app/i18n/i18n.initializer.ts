import { inject, provideAppInitializer } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

function applyPageMeta(
  translate: TranslateService,
  title: Title,
  meta: Meta,
): void {
  title.setTitle(translate.instant('meta.title'));
  meta.updateTag({
    name: 'description',
    content: translate.instant('meta.description'),
  });
}

export function provideI18nInitializer() {
  return provideAppInitializer(() => {
    const translate = inject(TranslateService);
    const title = inject(Title);
    const meta = inject(Meta);

    translate.setDefaultLang('ar');

    return firstValueFrom(translate.use('ar')).then(() => {
      applyPageMeta(translate, title, meta);
      translate.onLangChange.subscribe(() =>
        applyPageMeta(translate, title, meta),
      );
    });
  });
}
