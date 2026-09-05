import { Component, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';

import { SpinnerComponent } from '../spinner/spinner.component';

export type ButtonVariant = 'primary' | 'secondary' | 'ghost' | 'danger';
export type ButtonSize = 'sm' | 'md';

@Component({
  selector: 'app-button',
  standalone: true,
  imports: [RouterLink, SpinnerComponent],
  templateUrl: './button.component.html',
  styleUrl: './button.component.scss',
})
export class ButtonComponent {
  readonly variant = input<ButtonVariant>('primary');
  readonly size = input<ButtonSize>('md');
  readonly type = input<'button' | 'submit'>('button');
  readonly disabled = input(false);
  readonly loading = input(false);
  readonly routerLink = input<string | string[] | null>(null);
  readonly ariaDisabled = input(false);

  readonly clicked = output<MouseEvent>();

  onClick(event: MouseEvent): void {
    if (this.disabled() || this.loading() || this.ariaDisabled()) {
      event.preventDefault();
      return;
    }
    this.clicked.emit(event);
  }
}
