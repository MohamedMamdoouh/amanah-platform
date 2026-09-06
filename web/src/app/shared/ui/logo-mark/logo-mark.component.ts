import { Component, computed, input } from '@angular/core';

const LOGO_ASPECT = 89.6 / 100;

@Component({
  selector: 'app-logo-mark',
  standalone: true,
  template: `
    <img
      class="logo-mark"
      [class.logo-mark--on-dark]="variant() === 'on-dark'"
      src="assets/images/logo-mark.svg"
      [style.width.px]="width()"
      [style.height.px]="height()"
      alt=""
      aria-hidden="true"
    />
  `,
  styles: [
    `
      .logo-mark {
        display: block;
        flex-shrink: 0;
      }

      .logo-mark--on-dark {
        padding: 0.25rem 0.3125rem;
        background: var(--color-surface);
        border-radius: var(--radius-sm);
        box-shadow: 0 1px 3px rgba(0, 0, 0, 0.12);
      }
    `,
  ],
})
export class LogoMarkComponent {
  readonly size = input(36);
  readonly variant = input<'default' | 'on-dark'>('default');

  readonly width = computed(() => this.size());
  readonly height = computed(() => Math.round(this.size() * LOGO_ASPECT));
}
