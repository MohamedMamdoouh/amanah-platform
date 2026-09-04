import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { ReportSummary } from '../models/report.models';
import { ReportService } from '../report.service';

@Component({
  selector: 'app-my-reports',
  standalone: true,
  imports: [DatePipe, RouterLink, TranslateModule],
  templateUrl: './my-reports.component.html',
  styleUrl: './my-reports.component.scss',
})
export class MyReportsComponent implements OnInit {
  private readonly reportService = inject(ReportService);
  private readonly catalogLabels = inject(CatalogLabelService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly reports = signal<ReportSummary[]>([]);

  ngOnInit(): void {
    void this.loadReports();
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  typeLabel(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  statusLabel(status: string): string {
    return this.translate.instant(`reports.status.${status}`);
  }

  private async loadReports(): Promise<void> {
    try {
      const response = await firstValueFrom(
        this.reportService.getMine('pending_review'),
      );
      this.reports.set(response.items);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }
}
