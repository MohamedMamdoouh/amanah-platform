import { Component, inject, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../notifications/notification.service';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly notifications = inject(NotificationService);

  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.notifications.refreshUnreadCount();
  }

  async logout(): Promise<void> {
    try {
      await firstValueFrom(this.auth.logout());
    } catch {
      // Session is cleared in AuthService even when the API call fails.
    }

    this.notifications.clearUnreadCount();
    await this.router.navigate(['/']);
  }
}
