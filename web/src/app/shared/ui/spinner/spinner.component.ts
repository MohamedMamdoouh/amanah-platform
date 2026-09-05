import { Component, input } from '@angular/core';

@Component({
  selector: 'app-spinner',
  standalone: true,
  template: `
    <span
      class="spinner"
      [class.spinner--sm]="size() === 'sm'"
      [class.spinner--md]="size() === 'md'"
      [attr.aria-hidden]="decorative() ? 'true' : null"
      [attr.role]="decorative() ? null : 'status'"
    ></span>
  `,
  styleUrl: './spinner.component.scss',
})
export class SpinnerComponent {
  readonly size = input<'sm' | 'md'>('md');
  readonly decorative = input(true);
}
