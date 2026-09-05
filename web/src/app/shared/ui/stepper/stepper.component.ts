import { Component, input } from '@angular/core';

export interface StepItem {
  label: string;
}

@Component({
  selector: 'app-stepper',
  standalone: true,
  templateUrl: './stepper.component.html',
  styleUrl: './stepper.component.scss',
})
export class StepperComponent {
  readonly steps = input.required<StepItem[]>();
  readonly currentStep = input.required<number>();
}
