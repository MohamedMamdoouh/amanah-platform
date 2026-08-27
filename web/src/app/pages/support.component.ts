import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-support',
  standalone: true,
  imports: [TranslateModule],
  template: `
    <article class="legal-page">
      <header class="legal-page__masthead">
        <h1>{{ 'pages.support.title' | translate }}</h1>
        <p class="legal-page__lead">{{ 'pages.support.lead' | translate }}</p>
      </header>

      <section class="legal-page__section">
        <h2>{{ 'pages.support.email.title' | translate }}</h2>
        <p>
          <a href="mailto:support@amanah.example" class="support-email">
            {{ 'pages.support.email.address' | translate }}
          </a>
        </p>
        <p>{{ 'pages.support.email.body' | translate }}</p>
      </section>

      <section class="legal-page__section">
        <h2>{{ 'pages.support.response.title' | translate }}</h2>
        <p>{{ 'pages.support.response.body' | translate }}</p>
      </section>
    </article>
  `,
  styles: `
    .support-email {
      font-family: var(--font-body);
      font-size: 1.125rem;
      font-weight: 600;
      text-decoration: none;
    }

    .support-email:hover {
      text-decoration: underline;
    }
  `,
})
export class SupportComponent {}
