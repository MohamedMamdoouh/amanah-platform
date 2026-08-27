import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  template: `
    <header class="header">
      <a
        routerLink="/"
        class="header__brand"
        [attr.aria-label]="'nav.home' | translate"
      >
        <span class="header__mark" aria-hidden="true"></span>
        <span class="header__name">{{ 'app.name' | translate }}</span>
      </a>
      <nav class="header__nav">
        <span class="header__login-placeholder">{{
          'nav.login' | translate
        }}</span>
      </nav>
    </header>
  `,
  styles: `
    .header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: var(--space-md);
      padding: var(--space-md) var(--space-lg);
      background: var(--color-surface);
      border-bottom: 1px solid var(--color-border);
    }

    .header__brand {
      display: flex;
      align-items: center;
      gap: var(--space-sm);
      text-decoration: none;
      color: var(--color-text);
    }

    .header__mark {
      width: 2rem;
      height: 2rem;
      border-radius: var(--radius-sm);
      background: linear-gradient(135deg, var(--color-accent) 0%, #0a5c44 100%);
      box-shadow: inset 0 -2px 0 rgba(0, 0, 0, 0.12);
    }

    .header__name {
      font-family: var(--font-display);
      font-weight: 800;
      font-size: 1.25rem;
    }

    .header__nav {
      display: flex;
      align-items: center;
    }

    .header__login-placeholder {
      font-size: 0.9375rem;
      font-weight: 500;
      color: var(--color-text-muted);
      padding: var(--space-xs) var(--space-md);
      border: 1px dashed var(--color-border);
      border-radius: var(--radius-sm);
    }
  `,
})
export class HeaderComponent {}
