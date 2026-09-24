import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { registerLocaleData } from '@angular/common';
import localeArEg from '@angular/common/locales/ar-EG';
import {
  importProvidersFrom,
  inject,
  LOCALE_ID,
  provideAppInitializer,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideRouter } from '@angular/router';
import { TranslateLoader, TranslateModule } from '@ngx-translate/core';

import { routes } from './app.routes';
import { AuthService } from './auth/auth.service';
import { provideI18nInitializer } from './i18n/i18n.initializer';
import { multiTranslateLoaderFactory } from './i18n/multi-translate.loader';
import { authInterceptor } from './interceptors/auth.interceptor';

registerLocaleData(localeArEg);

export const appConfig = {
  providers: [
    { provide: LOCALE_ID, useValue: 'ar-EG' },
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withInterceptors([authInterceptor])),
    provideRouter(routes),
    provideI18nInitializer(),
    provideAppInitializer(() => inject(AuthService).initialize()),
    importProvidersFrom(
      TranslateModule.forRoot({
        loader: {
          provide: TranslateLoader,
          useFactory: multiTranslateLoaderFactory,
          deps: [HttpClient],
        },
      }),
    ),
  ],
};
