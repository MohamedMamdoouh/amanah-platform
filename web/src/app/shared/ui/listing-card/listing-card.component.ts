import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

import { BadgeComponent, BadgeVariant } from '../badge/badge.component';
import { IconComponent } from '../icon/icon.component';

@Component({
  selector: 'app-listing-card',
  standalone: true,
  imports: [RouterLink, BadgeComponent, IconComponent],
  templateUrl: './listing-card.component.html',
  styleUrl: './listing-card.component.scss',
})
export class ListingCardComponent {
  readonly title = input.required<string>();
  readonly subtitle = input<string | null>(null);
  readonly location = input<string | null>(null);
  readonly date = input<string | null>(null);
  readonly typeLabel = input<string | null>(null);
  readonly badgeLabel = input<string | null>(null);
  readonly badgeVariant = input<BadgeVariant>('neutral');
  readonly imageUrl = input<string | null>(null);
  readonly routerLink = input<string | string[] | null>(null);
  readonly actionLabel = input<string | null>(null);
  readonly unread = input(false);
}
