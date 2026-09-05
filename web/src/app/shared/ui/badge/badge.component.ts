import { Component, input } from '@angular/core';

export type BadgeVariant =
  | 'pending'
  | 'approved'
  | 'rejected'
  | 'neutral'
  | 'published'
  | 'lost'
  | 'found'
  | 'claim'
  | 'resolved';

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
      [class.badge--published]="variant() === 'published'"
      [class.badge--lost]="variant() === 'lost'"
      [class.badge--found]="variant() === 'found'"
      [class.badge--claim]="variant() === 'claim'"
      [class.badge--resolved]="variant() === 'resolved'"
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
