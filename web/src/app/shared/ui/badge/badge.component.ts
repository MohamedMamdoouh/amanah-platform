import { Component, input } from '@angular/core';

export type BadgeVariant = 'pending' | 'approved' | 'rejected' | 'neutral';

@Component({
  selector: 'app-badge',
  standalone: true,
  template: `
    <span
      class="badge"
      [class.badge--pending]="variant() === 'pending'"
      [class.badge--approved]="variant() === 'approved'"
      [class.badge--rejected]="variant() === 'rejected'"
      [class.badge--neutral]="variant() === 'neutral'"
      [class.badge--dot]="dot()"
    >
      <ng-content />
    </span>
  `,
  styleUrl: './badge.component.scss',
})
export class BadgeComponent {
  readonly variant = input<BadgeVariant>('neutral');
  readonly dot = input(false);
}
