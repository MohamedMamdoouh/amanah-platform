import { Component, input } from '@angular/core';

import { SpinnerComponent } from '../spinner/spinner.component';

@Component({
  selector: 'app-loading-indicator',
  standalone: true,
  imports: [SpinnerComponent],
  templateUrl: './loading-indicator.component.html',
  styleUrl: './loading-indicator.component.scss',
})
export class LoadingIndicatorComponent {
  readonly label = input.required<string>();
}
