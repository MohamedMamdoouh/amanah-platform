import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-footer',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  template: `
    <footer class="footer">
      <p class="footer__tagline">{{ 'footer.tagline' | translate }}</p>
      <nav class="footer__nav">
        <a routerLink="/terms">{{ 'footer.terms' | translate }}</a>
        <a routerLink="/privacy">{{ 'footer.privacy' | translate }}</a>
        <a routerLink="/safety">{{ 'footer.safety' | translate }}</a>
        <a routerLink="/support">{{ 'footer.support' | translate }}</a>
      </nav>
    </footer>
  `,
  styles: `
    .footer {
      padding: var(--space-lg);
      background: var(--color-surface);
      border-top: 1px solid var(--color-border);
    }

    .footer__tagline {
      margin: 0 0 var(--space-md);
      font-size: 0.875rem;
      color: var(--color-text-muted);
      text-align: center;
    }

    .footer__nav {
      display: flex;
      flex-wrap: wrap;
      justify-content: center;
      gap: var(--space-sm) var(--space-lg);
    }

    .footer__nav a {
      font-size: 0.9375rem;
      font-weight: 500;
      color: var(--color-text);
      text-decoration: none;
    }

    .footer__nav a:hover {
      color: var(--color-accent);
      text-decoration: underline;
    }
  `,
})
export class FooterComponent {}
