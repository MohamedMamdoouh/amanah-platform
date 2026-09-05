import { Routes } from '@angular/router';

import { AdminShellComponent } from './admin/admin-shell/admin-shell.component';
import { ModerationQueueComponent } from './admin/moderation/moderation-queue.component';
import { ModerationReviewComponent } from './admin/moderation/moderation-review.component';
import { authGuard, guestGuard, adminGuard } from './auth/auth.guards';
import { LoginComponent } from './auth/login/login.component';
import { HomeComponent } from './home/home.component';
import { AppShellComponent } from './layout/app-shell/app-shell.component';
import { NotificationsComponent } from './notifications/notifications.component';
import { PrivacyComponent } from './pages/privacy/privacy.component';
import { SafetyComponent } from './pages/safety/safety.component';
import { SupportComponent } from './pages/support/support.component';
import { TermsComponent } from './pages/terms/terms.component';
import { MyReportsComponent } from './reports/my-reports/my-reports.component';
import { ReportDetailComponent } from './reports/report-detail/report-detail.component';
import { ReportFormComponent } from './reports/report-form/report-form.component';

export const routes: Routes = [
  {
    path: '',
    component: AppShellComponent,
    children: [
      { path: '', component: HomeComponent },
      { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
      {
        path: 'report/lost',
        component: ReportFormComponent,
        canActivate: [authGuard],
        data: { type: 'lost' },
      },
      {
        path: 'report/found',
        component: ReportFormComponent,
        canActivate: [authGuard],
        data: { type: 'found' },
      },
      {
        path: 'my/reports',
        component: MyReportsComponent,
        canActivate: [authGuard],
      },
      {
        path: 'my/reports/:id',
        component: ReportDetailComponent,
        canActivate: [authGuard],
      },
      {
        path: 'notifications',
        component: NotificationsComponent,
        canActivate: [authGuard],
      },
      {
        path: 'admin',
        component: AdminShellComponent,
        canActivate: [adminGuard],
        children: [
          { path: '', redirectTo: 'moderation', pathMatch: 'full' },
          { path: 'moderation', component: ModerationQueueComponent },
          { path: 'moderation/:id', component: ModerationReviewComponent },
        ],
      },
      { path: 'terms', component: TermsComponent },
      { path: 'privacy', component: PrivacyComponent },
      { path: 'safety', component: SafetyComponent },
      { path: 'support', component: SupportComponent },
    ],
  },
];
