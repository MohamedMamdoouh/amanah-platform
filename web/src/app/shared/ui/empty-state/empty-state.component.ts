import { Component, input } from '@angular/core';

export type EmptyStateVariant = 'default' | 'success';

@Component({
  selector: 'app-empty-state',
  standalone: true,
  templateUrl: './empty-state.component.html',
  styleUrl: './empty-state.component.scss',
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly description = input<string | null>(null);
  readonly variant = input<EmptyStateVariant>('default');
}
