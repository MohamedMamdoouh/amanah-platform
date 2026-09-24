import { Component, inject, OnInit, signal } from '@angular/core';
import { AppDatePipe } from '../../i18n/app-date.pipe';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { ApiErrorService } from '../../i18n/api-error.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { CardComponent } from '../../shared/ui/card/card.component';
import { ConfirmDialogComponent } from '../../shared/ui/confirm-dialog/confirm-dialog.component';
import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { AdminUserDetail, AdminUsersService } from '../admin-users.service';

@Component({
  selector: 'app-user-detail',
  standalone: true,
  imports: [
    AppDatePipe,
    AlertComponent,
    BadgeComponent,
    ButtonComponent,
    CardComponent,
    ConfirmDialogComponent,
    FormFieldComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    TranslateModule,
  ],
  templateUrl: './user-detail.component.html',
  styleUrl: './user-detail.component.scss',
})
export class UserDetailComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly usersService = inject(AdminUsersService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly loadError = signal<string | null>(null);
  readonly user = signal<AdminUserDetail | null>(null);
  readonly actionError = signal<string | null>(null);
  readonly actionSuccess = signal<string | null>(null);

  readonly showBanConfirm = signal(false);
  readonly showUnbanConfirm = signal(false);
  readonly banning = signal(false);
  readonly unbanning = signal(false);

  readonly banReasonControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(3)],
  });

  ngOnInit(): void {
    void this.loadUser();
  }

  openBanConfirm(): void {
    if (this.user()?.isBanned || this.banning()) {
      return;
    }

    this.actionError.set(null);
    this.actionSuccess.set(null);
    this.banReasonControl.markAsTouched();
    if (this.banReasonControl.invalid) {
      return;
    }

    this.showBanConfirm.set(true);
  }

  closeBanConfirm(): void {
    this.showBanConfirm.set(false);
  }

  openUnbanConfirm(): void {
    if (!this.user()?.isBanned || this.unbanning()) {
      return;
    }

    this.actionError.set(null);
    this.actionSuccess.set(null);
    this.showUnbanConfirm.set(true);
  }

  closeUnbanConfirm(): void {
    this.showUnbanConfirm.set(false);
  }

  async confirmBan(): Promise<void> {
    const detail = this.user();
    if (!detail || detail.isBanned || this.banning()) {
      return;
    }

    this.banning.set(true);
    this.actionError.set(null);

    try {
      await firstValueFrom(
        this.usersService.ban(detail.id, this.banReasonControl.value.trim()),
      );
      this.showBanConfirm.set(false);
      this.actionSuccess.set(this.translate.instant('admin.users.ban_done'));
      await this.loadUser();
    } catch (error) {
      this.actionError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.banning.set(false);
    }
  }

  async confirmUnban(): Promise<void> {
    const detail = this.user();
    if (!detail || !detail.isBanned || this.unbanning()) {
      return;
    }

    this.unbanning.set(true);
    this.actionError.set(null);

    try {
      await firstValueFrom(this.usersService.unban(detail.id));
      this.showUnbanConfirm.set(false);
      this.actionSuccess.set(this.translate.instant('admin.users.unban_done'));
      await this.loadUser();
    } catch (error) {
      this.actionError.set(this.apiErrors.messageFromHttpError(error));
    } finally {
      this.unbanning.set(false);
    }
  }

  private async loadUser(): Promise<void> {
    const userId = this.route.snapshot.paramMap.get('id');
    if (!userId) {
      this.loadError.set(this.translate.instant('error.internal.error'));
      this.loading.set(false);
      return;
    }

    this.loading.set(true);
    this.loadError.set(null);

    try {
      const detail = await firstValueFrom(this.usersService.getUser(userId));
      this.user.set(detail);
    } catch (error) {
      this.loadError.set(this.apiErrors.messageFromHttpError(error));
      this.user.set(null);
    } finally {
      this.loading.set(false);
    }
  }
}
