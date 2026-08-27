import { Routes } from '@angular/router';
import { HomeComponent } from './home/home.component';
import { AppShellComponent } from './layout/app-shell.component';
import { PrivacyComponent } from './pages/privacy.component';
import { SafetyComponent } from './pages/safety.component';
import { SupportComponent } from './pages/support.component';
import { TermsComponent } from './pages/terms.component';

export const routes: Routes = [
  {
    path: '',
    component: AppShellComponent,
    children: [
      { path: '', component: HomeComponent },
      { path: 'terms', component: TermsComponent },
      { path: 'privacy', component: PrivacyComponent },
      { path: 'safety', component: SafetyComponent },
      { path: 'support', component: SupportComponent },
    ],
  },
];
