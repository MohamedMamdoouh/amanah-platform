import { Component, input } from '@angular/core';
import { RouterLink } from '@angular/router';

export type CardVariant = 'default' | 'interactive' | 'inset';

@Component({
  selector: 'app-card',
  standalone: true,
  imports: [RouterLink],
  templateUrl: './card.component.html',
  styleUrl: './card.component.scss',
})
export class CardComponent {
  readonly variant = input<CardVariant>('default');
  readonly routerLink = input<string | string[] | null>(null);
  readonly padding = input<'md' | 'lg'>('lg');
}
