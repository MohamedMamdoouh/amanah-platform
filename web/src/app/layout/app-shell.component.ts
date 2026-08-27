import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { FooterComponent } from './footer.component';
import { HeaderComponent } from './header.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, HeaderComponent, FooterComponent],
  template: `
    <div class="shell">
      <app-header />
      <main class="shell__main" id="main-content">
        <router-outlet />
      </main>
      <app-footer />
    </div>
  `,
  styles: `
    .shell {
      min-height: 100vh;
      display: flex;
      flex-direction: column;
    }

    .shell__main {
      flex: 1;
      width: 100%;
      max-width: var(--shell-width);
      margin-inline: auto;
      padding: var(--space-lg);
    }

    @media (min-width: 48rem) {
      .shell__main {
        padding: var(--space-xl) var(--space-lg);
      }
    }
  `,
})
export class AppShellComponent {}
