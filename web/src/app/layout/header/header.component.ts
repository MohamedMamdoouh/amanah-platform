import { Component, inject, OnInit, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../notifications/notification.service';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, TranslateModule, BadgeComponent, ButtonComponent],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly notifications = inject(NotificationService);
  readonly loggingOut = signal(false);

  private readonly router = inject(Router);

  ngOnInit(): void {
    void this.notifications.refreshUnreadCount();
  }

  async logout(): Promise<void> {
    if (this.loggingOut()) {
      return;
    }

    this.loggingOut.set(true);
    try {
      await firstValueFrom(this.auth.logout());
    } catch {
      // Session is cleared in AuthService even when the API call fails.
    }

    this.notifications.clearUnreadCount();
    await this.router.navigate(['/']);
    this.loggingOut.set(false);
  }
}
