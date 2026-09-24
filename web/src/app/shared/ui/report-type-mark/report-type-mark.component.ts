import { Component, computed, inject, input } from '@angular/core';

import { DomainLabelService } from '../../../i18n/domain-label.service';
import { IconComponent, IconName } from '../icon/icon.component';

export type ReportTypeMarkKind = 'lost' | 'found';

@Component({
  selector: 'app-report-type-mark',
  standalone: true,
  imports: [IconComponent],
  template: `
    <span
      class="report-type-mark"
      [class.report-type-mark--lost]="kind() === 'lost'"
      [class.report-type-mark--found]="kind() === 'found'"
    >
      <app-icon [name]="iconName()" size="sm" />
      <span class="report-type-mark__label">{{ label() }}</span>
    </span>
  `,
  styleUrl: './report-type-mark.component.scss',
})
export class ReportTypeMarkComponent {
  private readonly domainLabels = inject(DomainLabelService);

  readonly type = input.required<string>();

  readonly kind = computed<ReportTypeMarkKind>(() =>
    this.type() === 'found' ? 'found' : 'lost',
  );

  readonly iconName = computed<IconName>(() => this.kind());

  readonly label = computed(() => this.domainLabels.reportType(this.kind()));
}
