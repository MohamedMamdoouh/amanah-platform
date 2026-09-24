import { Component, inject, OnInit, signal } from '@angular/core';
import { AppDatePipe } from '../../i18n/app-date.pipe';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { DomainLabelService } from '../../i18n/domain-label.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { ListingCardComponent } from '../../shared/ui/listing-card/listing-card.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { TabItem, TabsComponent } from '../../shared/ui/tabs/tabs.component';
import { ReportStatus, ReportSummary } from '../models/report.models';
import { ReportService } from '../report.service';

type MyReportsTab =
  | 'pending_review'
  | 'rejected'
  | 'published'
  | 'claim_in_progress';

@Component({
  selector: 'app-my-reports',
  standalone: true,
  imports: [
    AppDatePipe,
    AlertComponent,
    ButtonComponent,
    EmptyStateComponent,
    ListingCardComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    RouterLink,
    TabsComponent,
    TranslateModule,
  ],
  templateUrl: './my-reports.component.html',
  styleUrl: './my-reports.component.scss',
})
export class MyReportsComponent implements OnInit {
  private readonly reportService = inject(ReportService);
  private readonly catalogLabels = inject(CatalogLabelService);
  protected readonly domainLabels = inject(DomainLabelService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly reports = signal<ReportSummary[]>([]);
  readonly activeTab = signal<MyReportsTab>('pending_review');

  readonly tabs: MyReportsTab[] = [
    'pending_review',
    'rejected',
    'published',
    'claim_in_progress',
  ];

  ngOnInit(): void {
    void this.loadReports(this.activeTab());
  }

  tabItems(): TabItem[] {
    return this.tabs.map((tab) => ({
      id: tab,
      label: this.tabLabel(tab),
    }));
  }

  onTabChange(id: string): void {
    this.selectTab(id as MyReportsTab);
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  tabLabel(tab: MyReportsTab): string {
    return this.translate.instant(`reports.mine.tab_${tab}`);
  }

  emptyMessage(): string {
    return this.translate.instant(`reports.mine.empty_${this.activeTab()}`);
  }

  selectTab(tab: MyReportsTab): void {
    if (tab === this.activeTab()) {
      return;
    }

    this.activeTab.set(tab);
    void this.loadReports(tab);
  }

  private async loadReports(status: ReportStatus): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const response = await firstValueFrom(this.reportService.getMine(status));
      this.reports.set(response.items);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }
}
