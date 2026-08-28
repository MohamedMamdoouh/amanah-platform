import { Routes } from '@angular/router';

import { AdminShellComponent } from './admin/admin-shell/admin-shell.component';
import { guestGuard, adminGuard } from './auth/auth.guards';
import { LoginComponent } from './auth/login/login.component';
import { HomeComponent } from './home/home.component';
import { AppShellComponent } from './layout/app-shell/app-shell.component';
import { PrivacyComponent } from './pages/privacy/privacy.component';
import { SafetyComponent } from './pages/safety/safety.component';
import { SupportComponent } from './pages/support/support.component';
import { TermsComponent } from './pages/terms/terms.component';

export const routes: Routes = [
  {
    path: '',
    component: AppShellComponent,
    children: [
      { path: '', component: HomeComponent },
      { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
      { path: 'admin', component: AdminShellComponent, canActivate: [adminGuard] },
      { path: 'terms', component: TermsComponent },
      { path: 'privacy', component: PrivacyComponent },
      { path: 'safety', component: SafetyComponent },
      { path: 'support', component: SupportComponent },
    ],
  },
];
