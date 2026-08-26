import { Component } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, TranslateModule],
  template: `
    <div class="shell">
      <header class="shell__header">
        <a routerLink="/" class="shell__brand">{{ 'app.name' | translate }}</a>
        <nav class="shell__nav">
          <a routerLink="/login">{{ 'nav.login' | translate }}</a>
        </nav>
      </header>
      <main class="shell__main">
        <router-outlet />
      </main>
      <footer class="shell__footer">
        <a routerLink="/terms">{{ 'footer.terms' | translate }}</a>
        <a routerLink="/privacy">{{ 'footer.privacy' | translate }}</a>
        <a routerLink="/safety">{{ 'footer.safety' | translate }}</a>
        <a routerLink="/support">{{ 'footer.support' | translate }}</a>
      </footer>
    </div>
  `,
  styles: `
    .shell {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
    }

    .shell__header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 1rem 1.5rem;
      border-bottom: 1px solid #e5e5e5;
    }

    .shell__brand {
      font-weight: 700;
      text-decoration: none;
      color: inherit;
    }

    .shell__nav a {
      text-decoration: none;
      color: inherit;
    }

    .shell__main {
      flex: 1;
      padding: 1.5rem;
    }

    .shell__footer {
      display: flex;
      flex-wrap: wrap;
      gap: 1rem;
      padding: 1rem 1.5rem;
      border-top: 1px solid #e5e5e5;
      font-size: 0.9rem;
    }

    .shell__footer a {
      color: inherit;
    }
  `,
})
export class AppShellComponent {}
