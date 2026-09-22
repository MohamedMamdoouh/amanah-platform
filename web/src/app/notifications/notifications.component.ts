import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { NotificationItem, NotificationService } from './notification.service';
import { LoadingIndicatorComponent } from '../shared/ui/loading-indicator/loading-indicator.component';
import { AlertComponent } from '../shared/ui/alert/alert.component';
import { ButtonComponent } from '../shared/ui/button/button.component';
import { EmptyStateComponent } from '../shared/ui/empty-state/empty-state.component';
import { PageHeaderComponent } from '../shared/ui/page-header/page-header.component';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    DatePipe,
    EmptyStateComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    TranslateModule,
  ],
  templateUrl: './notifications.component.html',
  styleUrl: './notifications.component.scss',
})
export class NotificationsComponent implements OnInit {
  private readonly notificationService = inject(NotificationService);
  private readonly router = inject(Router);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<NotificationItem[]>([]);
  readonly markingAll = signal(false);

  ngOnInit(): void {
    void this.loadNotifications();
  }

  typeLabel(type: string): string {
    return this.translate.instant(`notifications.type.${type}`);
  }

  linkLabel(item: NotificationItem): string {
    if (item.payload.deepLink.startsWith('/my/chats/')) {
      return this.translate.instant('notifications.open_chat');
    }

    return this.translate.instant('notifications.open_report');
  }

  reasonLabel(code: string | null | undefined): string {
    if (!code) {
      return '';
    }

    if (code === 'no_action' || code === 'takedown' || code === 'ban') {
      return this.translate.instant(`notifications.outcome.${code}`);
    }

    return this.translate.instant(code);
  }

  async markAllRead(): Promise<void> {
    this.markingAll.set(true);
    try {
      await firstValueFrom(this.notificationService.markAllRead());
      this.items.update((current) =>
        current.map((item) => ({ ...item, isRead: true })),
      );
      this.notificationService.clearUnreadCount();
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.markingAll.set(false);
    }
  }

  async openNotification(item: NotificationItem): Promise<void> {
    if (!item.isRead) {
      try {
        await firstValueFrom(this.notificationService.markRead(item.id));
        this.items.update((current) =>
          current.map((entry) =>
            entry.id === item.id ? { ...entry, isRead: true } : entry,
          ),
        );
        this.notificationService.decrementUnreadCount();
      } catch {
        // Still navigate even if mark-read fails.
      }
    }

    let deepLink = item.payload.deepLink;

    if (
      (item.payload.type === 'ClaimWithdrawnByClaimant' ||
        item.payload.type === 'NewClaimSubmitted') &&
      deepLink.startsWith('/my/reports/') &&
      !deepLink.includes('#')
    ) {
      deepLink = `${deepLink}#claims-section`;
    }

    await this.router.navigateByUrl(deepLink);
  }

  private async loadNotifications(): Promise<void> {
    try {
      const response = await firstValueFrom(this.notificationService.getAll());
      this.items.set(response.items);
      await this.notificationService.refreshUnreadCount();
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }
}
