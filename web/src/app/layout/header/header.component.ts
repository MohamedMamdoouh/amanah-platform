import {
  Component,
  ElementRef,
  HostListener,
  inject,
  OnInit,
  signal,
} from '@angular/core';
import { Router, RouterLink, RouterLinkActive } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AuthService } from '../../auth/auth.service';
import { NotificationService } from '../../notifications/notification.service';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { IconComponent } from '../../shared/ui/icon/icon.component';
import { LogoMarkComponent } from '../../shared/ui/logo-mark/logo-mark.component';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [
    RouterLink,
    RouterLinkActive,
    TranslateModule,
    BadgeComponent,
    ButtonComponent,
    IconComponent,
    LogoMarkComponent,
  ],
  templateUrl: './header.component.html',
  styleUrl: './header.component.scss',
})
export class HeaderComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly notifications = inject(NotificationService);
  readonly loggingOut = signal(false);
  readonly mobileNavOpen = signal(false);
  readonly userMenuOpen = signal(false);

  private readonly router = inject(Router);
  private readonly host = inject(ElementRef<HTMLElement>);

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (!this.userMenuOpen()) {
      return;
    }

    const target = event.target;
    if (target instanceof Node && !this.host.nativeElement.contains(target)) {
      this.userMenuOpen.set(false);
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.userMenuOpen.set(false);
  }

  ngOnInit(): void {
    void this.notifications.refreshUnreadCount();
  }

  toggleMobileNav(): void {
    this.mobileNavOpen.update((open) => !open);
  }

  closeMobileNav(): void {
    this.mobileNavOpen.set(false);
  }

  toggleUserMenu(event: MouseEvent): void {
    event.stopPropagation();
    this.userMenuOpen.update((open) => !open);
  }

  closeUserMenu(): void {
    this.userMenuOpen.set(false);
  }

  async logout(): Promise<void> {
    if (this.loggingOut()) {
      return;
    }

    this.loggingOut.set(true);
    this.closeMobileNav();
    this.closeUserMenu();
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
