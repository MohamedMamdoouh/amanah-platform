import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { AppShellComponent } from './layout/app-shell.component';
import { StaticPageComponent } from './pages/static-page.component';

export const routes: Routes = [
  {
    path: '',
    component: AppShellComponent,
    children: [
      { path: '', component: HomeComponent },
      {
        path: 'terms',
        component: StaticPageComponent,
        data: {
          titleKey: 'pages.terms.title',
          bodyKey: 'pages.terms.body',
        },
      },
      {
        path: 'privacy',
        component: StaticPageComponent,
        data: {
          titleKey: 'pages.privacy.title',
          bodyKey: 'pages.privacy.body',
        },
      },
      {
        path: 'safety',
        component: StaticPageComponent,
        data: {
          titleKey: 'pages.safety.title',
          bodyKey: 'pages.safety.body',
        },
      },
      {
        path: 'support',
        component: StaticPageComponent,
        data: {
          titleKey: 'pages.support.title',
          bodyKey: 'pages.support.body',
        },
      },
    ],
  },
];
