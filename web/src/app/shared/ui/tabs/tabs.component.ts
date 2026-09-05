import { Component, input, output } from '@angular/core';

export interface TabItem {
  id: string;
  label: string;
}

@Component({
  selector: 'app-tabs',
  standalone: true,
  templateUrl: './tabs.component.html',
  styleUrl: './tabs.component.scss',
})
export class TabsComponent {
  readonly tabs = input.required<TabItem[]>();
  readonly activeId = input.required<string>();
  readonly ariaLabel = input<string>('');

  readonly tabChange = output<string>();

  selectTab(id: string): void {
    if (id !== this.activeId()) {
      this.tabChange.emit(id);
    }
  }
}
