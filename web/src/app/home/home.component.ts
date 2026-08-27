import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';

@Component({
  selector: 'app-home',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  template: `
    <section class="hero" aria-labelledby="hero-title">
      <div class="hero__pattern" aria-hidden="true"></div>
      <div class="hero__content">
        <p class="hero__eyebrow">{{ 'home.eyebrow' | translate }}</p>
        <h1 id="hero-title" class="hero__title">{{ 'home.title' | translate }}</h1>
        <p class="hero__subtitle">{{ 'home.subtitle' | translate }}</p>
        <div class="hero__actions">
          <span class="hero__cta-placeholder">{{ 'home.cta_browse' | translate }}</span>
          <span class="hero__cta-placeholder hero__cta-placeholder--secondary">
            {{ 'home.cta_report' | translate }}
          </span>
        </div>
      </div>
      <aside class="hero__aside" [attr.aria-label]="'home.aside_label' | translate">
        <ul class="hero__principles">
          <li>
            <span class="hero__principle-label">{{ 'home.principle_moderation' | translate }}</span>
            <span class="hero__principle-desc">{{ 'home.principle_moderation_desc' | translate }}</span>
          </li>
          <li>
            <span class="hero__principle-label">{{ 'home.principle_verify' | translate }}</span>
            <span class="hero__principle-desc">{{ 'home.principle_verify_desc' | translate }}</span>
          </li>
          <li>
            <span class="hero__principle-label">{{ 'home.principle_chat' | translate }}</span>
            <span class="hero__principle-desc">{{ 'home.principle_chat_desc' | translate }}</span>
          </li>
        </ul>
      </aside>
    </section>
    <section class="home-links" aria-labelledby="home-links-title">
      <h2 id="home-links-title" class="home-links__title">{{ 'home.links_title' | translate }}</h2>
      <div class="home-links__grid">
        <a routerLink="/safety" class="home-links__card">
          <span class="home-links__card-title">{{ 'footer.safety' | translate }}</span>
          <span class="home-links__card-desc">{{ 'home.safety_card' | translate }}</span>
        </a>
        <a routerLink="/support" class="home-links__card">
          <span class="home-links__card-title">{{ 'footer.support' | translate }}</span>
          <span class="home-links__card-desc">{{ 'home.support_card' | translate }}</span>
        </a>
      </div>
    </section>
  `,
  styles: `
    .hero {
      position: relative;
      display: grid;
      gap: var(--space-xl);
      padding: var(--space-xl);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-lg);
      box-shadow: var(--shadow-soft);
      overflow: hidden;
    }

    .hero__pattern {
      position: absolute;
      inset: 0;
      opacity: 0.04;
      background-image:
        repeating-linear-gradient(
          45deg,
          var(--color-accent) 0,
          var(--color-accent) 1px,
          transparent 1px,
          transparent 12px
        ),
        repeating-linear-gradient(
          -45deg,
          var(--color-warm) 0,
          var(--color-warm) 1px,
          transparent 1px,
          transparent 12px
        );
      pointer-events: none;
    }

    .hero__content {
      position: relative;
      z-index: 1;
    }

    .hero__eyebrow {
      margin: 0 0 var(--space-sm);
      font-size: 0.8125rem;
      font-weight: 600;
      letter-spacing: 0.04em;
      color: var(--color-warm);
    }

    .hero__title {
      font-size: clamp(2rem, 5vw, 2.75rem);
      font-weight: 800;
      margin-bottom: var(--space-md);
      max-width: 28rem;
    }

    .hero__subtitle {
      margin: 0 0 var(--space-lg);
      font-size: 1.0625rem;
      color: var(--color-text-muted);
      max-width: 32rem;
    }

    .hero__actions {
      display: flex;
      flex-wrap: wrap;
      gap: var(--space-sm);
    }

    .hero__cta-placeholder {
      display: inline-block;
      padding: var(--space-sm) var(--space-lg);
      font-size: 0.9375rem;
      font-weight: 600;
      border-radius: var(--radius-sm);
      background: var(--color-accent);
      color: #fff;
      opacity: 0.85;
    }

    .hero__cta-placeholder--secondary {
      background: var(--color-accent-soft);
      color: var(--color-accent);
      border: 1px solid var(--color-border);
    }

    .hero__aside {
      position: relative;
      z-index: 1;
    }

    .hero__principles {
      list-style: none;
      margin: 0;
      padding: 0;
      display: grid;
      gap: var(--space-md);
    }

    .hero__principles li {
      padding: var(--space-md);
      background: var(--color-accent-soft);
      border-radius: var(--radius-md);
      border-inline-start: 3px solid var(--color-accent);
    }

    .hero__principle-label {
      display: block;
      font-family: var(--font-display);
      font-weight: 700;
      font-size: 0.9375rem;
      margin-bottom: var(--space-xs);
    }

    .hero__principle-desc {
      display: block;
      font-size: 0.875rem;
      color: var(--color-text-muted);
    }

    .home-links {
      margin-top: var(--space-xl);
    }

    .home-links__title {
      font-size: 1.125rem;
      font-weight: 700;
      margin-bottom: var(--space-md);
    }

    .home-links__grid {
      display: grid;
      gap: var(--space-md);
    }

    .home-links__card {
      display: block;
      padding: var(--space-lg);
      background: var(--color-surface);
      border: 1px solid var(--color-border);
      border-radius: var(--radius-md);
      text-decoration: none;
      color: inherit;
      transition: border-color 0.15s ease, box-shadow 0.15s ease;
    }

    .home-links__card:hover {
      border-color: var(--color-accent);
      box-shadow: var(--shadow-soft);
    }

    .home-links__card-title {
      display: block;
      font-family: var(--font-display);
      font-weight: 700;
      margin-bottom: var(--space-xs);
    }

    .home-links__card-desc {
      display: block;
      font-size: 0.875rem;
      color: var(--color-text-muted);
    }

    @media (min-width: 56rem) {
      .hero {
        grid-template-columns: 1.2fr 1fr;
        align-items: start;
      }

      .home-links__grid {
        grid-template-columns: 1fr 1fr;
      }
    }

    @media (prefers-reduced-motion: no-preference) {
      .hero__content {
        animation: hero-enter 0.6s ease both;
      }

      .hero__aside {
        animation: hero-enter 0.6s 0.1s ease both;
      }
    }

    @keyframes hero-enter {
      from {
        opacity: 0;
        transform: translateY(0.5rem);
      }

      to {
        opacity: 1;
        transform: translateY(0);
      }
    }
  `,
})
export class HomeComponent {}
