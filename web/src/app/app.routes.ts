import { Routes } from '@angular/router';

import { AbuseDetailComponent } from './admin/abuse/abuse-detail.component';
import { AbuseQueueComponent } from './admin/abuse/abuse-queue.component';
import { AdminShellComponent } from './admin/admin-shell/admin-shell.component';
import { ModerationQueueComponent } from './admin/moderation/moderation-queue.component';
import { ModerationReviewComponent } from './admin/moderation/moderation-review.component';
import { CategoriesAdminComponent } from './admin/categories/categories-admin.component';
import { UserDetailComponent } from './admin/users/user-detail.component';
import { UsersLookupComponent } from './admin/users/users-lookup.component';
import {
  authGuard,
  guestGuard,
  adminGuard,
  reactivationGuard,
} from './auth/auth.guards';
import { ReactivateAccountComponent } from './auth/reactivate-account/reactivate-account.component';
import { LoginComponent } from './auth/login/login.component';
import { BrowseComponent } from './browse/browse.component';
import { PublicReportDetailComponent } from './browse/public-report-detail.component';
import { HomeComponent } from './home/home.component';
import { AppShellComponent } from './layout/app-shell/app-shell.component';
import { NotificationsComponent } from './notifications/notifications.component';
import { NotFoundComponent } from './pages/not-found/not-found.component';
import { PrivacyComponent } from './pages/privacy/privacy.component';
import { SafetyComponent } from './pages/safety/safety.component';
import { SupportComponent } from './pages/support/support.component';
import { TermsComponent } from './pages/terms/terms.component';
import { UnavailableComponent } from './pages/unavailable/unavailable.component';
import { ChatThreadComponent } from './chats/chat-thread/chat-thread.component';
import { MyChatsComponent } from './chats/my-chats/my-chats.component';
import { MyClaimsComponent } from './claims/my-claims/my-claims.component';
import { MyReportsComponent } from './reports/my-reports/my-reports.component';
import { ReportDetailComponent } from './reports/report-detail/report-detail.component';
import { ReportFormComponent } from './reports/report-form/report-form.component';
import { AccountComponent } from './settings/account/account.component';

export const routes: Routes = [
  {
    path: '',
    component: AppShellComponent,
    children: [
      { path: '', component: HomeComponent },
      { path: 'browse', component: BrowseComponent },
      {
        path: 'lost/:id',
        component: PublicReportDetailComponent,
        data: { type: 'lost' },
      },
      {
        path: 'found/:id',
        component: PublicReportDetailComponent,
        data: { type: 'found' },
      },
      { path: 'not-found', component: NotFoundComponent },
      { path: 'unavailable', component: UnavailableComponent },
      { path: 'login', component: LoginComponent, canActivate: [guestGuard] },
      {
        path: 'account/reactivate',
        component: ReactivateAccountComponent,
        canActivate: [reactivationGuard],
      },
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
        path: 'my/claims',
        component: MyClaimsComponent,
        canActivate: [authGuard],
      },
      {
        path: 'my/chats',
        component: MyChatsComponent,
        canActivate: [authGuard],
      },
      {
        path: 'my/chats/:threadId',
        component: ChatThreadComponent,
        canActivate: [authGuard],
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
        path: 'settings/account',
        component: AccountComponent,
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
          { path: 'abuse', component: AbuseQueueComponent },
          { path: 'abuse/:id', component: AbuseDetailComponent },
          { path: 'users', component: UsersLookupComponent },
          { path: 'users/:id', component: UserDetailComponent },
          { path: 'categories', component: CategoriesAdminComponent },
        ],
      },
      { path: 'terms', component: TermsComponent },
      { path: 'privacy', component: PrivacyComponent },
      { path: 'safety', component: SafetyComponent },
      { path: 'support', component: SupportComponent },
    ],
  },
];
