import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { DomainLabelService } from '../../i18n/domain-label.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { ListingCardComponent } from '../../shared/ui/listing-card/listing-card.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { AbuseQueueItem, AdminAbuseService } from '../admin-abuse.service';

@Component({
  selector: 'app-abuse-queue',
  standalone: true,
  imports: [
    AlertComponent,
    BadgeComponent,
    DatePipe,
    EmptyStateComponent,
    ListingCardComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    TranslateModule,
  ],
  templateUrl: './abuse-queue.component.html',
  styleUrl: './abuse-queue.component.scss',
})
export class AbuseQueueComponent implements OnInit {
  private readonly abuseService = inject(AdminAbuseService);
  private readonly translate = inject(TranslateService);
  protected readonly domainLabels = inject(DomainLabelService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<AbuseQueueItem[]>([]);
  readonly openCount = signal(0);

  ngOnInit(): void {
    void this.loadQueue();
  }

  queueSubtitle(item: AbuseQueueItem): string {
    const reason = this.translate.instant(item.reason);
    const flagger = this.translate.instant('admin.abuse.queue.flagger', {
      name: item.abuseReporterDisplayName,
    });
    return `${reason} · ${flagger}`;
  }

  private async loadQueue(): Promise<void> {
    try {
      const queue = await firstValueFrom(this.abuseService.getQueue());
      this.items.set(queue.items);
      this.openCount.set(queue.openCount);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }
}
