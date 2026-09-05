import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { CatalogLabelService } from '../../i18n/catalog-label.service';
import {
  AdminModerationService,
  ModerationQueueItem,
} from '../admin-moderation.service';

@Component({
  selector: 'app-moderation-queue',
  standalone: true,
  imports: [DatePipe, RouterLink, TranslateModule],
  templateUrl: './moderation-queue.component.html',
  styleUrl: './moderation-queue.component.scss',
})
export class ModerationQueueComponent implements OnInit {
  private readonly moderationService = inject(AdminModerationService);
  private readonly catalogLabels = inject(CatalogLabelService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly items = signal<ModerationQueueItem[]>([]);
  readonly pendingCount = signal(0);

  ngOnInit(): void {
    void this.loadQueue();
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  typeLabel(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  private async loadQueue(): Promise<void> {
    try {
      const queue = await firstValueFrom(this.moderationService.getQueue());
      this.items.set(queue.items);
      this.pendingCount.set(queue.pendingCount);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }
}
