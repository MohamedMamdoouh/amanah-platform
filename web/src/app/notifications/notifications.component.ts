import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { NotificationItem, NotificationService } from './notification.service';

@Component({
  selector: 'app-notifications',
  standalone: true,
  imports: [DatePipe, TranslateModule],
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

  reasonLabel(code: string | null | undefined): string {
    if (!code) {
      return '';
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

    await this.router.navigateByUrl(item.payload.deepLink);
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
