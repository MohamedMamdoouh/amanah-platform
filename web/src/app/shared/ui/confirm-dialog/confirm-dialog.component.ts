import { Component, input, output } from '@angular/core';

import { ButtonComponent, ButtonVariant } from '../button/button.component';

@Component({
  selector: 'app-confirm-dialog',
  standalone: true,
  imports: [ButtonComponent],
  templateUrl: './confirm-dialog.component.html',
  styleUrl: './confirm-dialog.component.scss',
})
export class ConfirmDialogComponent {
  readonly titleId = input.required<string>();
  readonly title = input.required<string>();
  readonly intro = input.required<string>();
  readonly cancelLabel = input.required<string>();
  readonly confirmLabel = input.required<string>();
  readonly confirmVariant = input<ButtonVariant>('primary');
  readonly loading = input(false);
  readonly disabled = input(false);

  readonly cancelled = output<void>();
  readonly confirmed = output<void>();
}
