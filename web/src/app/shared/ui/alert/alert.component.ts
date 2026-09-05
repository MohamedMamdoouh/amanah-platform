import { Component, input } from '@angular/core';

export type AlertVariant = 'error' | 'info' | 'success';

@Component({
  selector: 'app-alert',
  standalone: true,
  template: `
    <div
      class="alert"
      [class.alert--error]="variant() === 'error'"
      [class.alert--info]="variant() === 'info'"
      [class.alert--success]="variant() === 'success'"
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
