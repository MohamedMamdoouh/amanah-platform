import { Component, input } from '@angular/core';

export type AlertVariant = 'error' | 'info' | 'success' | 'warning';

@Component({
  selector: 'app-alert',
  standalone: true,
  template: `
    <div
      class="alert"
      [class.alert--error]="variant() === 'error'"
      [class.alert--info]="variant() === 'info'"
      [class.alert--success]="variant() === 'success'"
      [class.alert--warning]="variant() === 'warning'"
      role="alert"
    >
      <ng-content />
    </div>
  `,
  styleUrl: './alert.component.scss',
})
export class AlertComponent {
  readonly variant = input<AlertVariant>('error');
}
