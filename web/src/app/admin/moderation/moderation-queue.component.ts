import { DatePipe } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';
import { debounceTime, distinctUntilChanged } from 'rxjs/operators';

import { CatalogLabelService } from '../../i18n/catalog-label.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { BadgeComponent } from '../../shared/ui/badge/badge.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';
import { ListingCardComponent } from '../../shared/ui/listing-card/listing-card.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { SearchInputComponent } from '../../shared/ui/search-input/search-input.component';
import {
  AdminModerationService,
  ModerationQueueItem,
} from '../admin-moderation.service';

@Component({
  selector: 'app-moderation-queue',
  standalone: true,
  imports: [
    AlertComponent,
    BadgeComponent,
    DatePipe,
    EmptyStateComponent,
    FormFieldComponent,
    ListingCardComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    RouterLink,
    SearchInputComponent,
    TranslateModule,
  ],
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
  readonly searchResults = signal<ModerationQueueItem[]>([]);
  readonly searchLoading = signal(false);
  readonly searchControl = new FormControl('', { nonNullable: true });

  ngOnInit(): void {
    void this.loadQueue();

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged())
      .subscribe((query) => void this.runSearch(query));
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  typeLabel(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  statusLabel(status: string): string {
    return this.translate.instant(`reports.status.${status}`);
  }

  hasSearchQuery(): boolean {
    return this.searchControl.value.trim().length > 0;
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

  private async runSearch(query: string): Promise<void> {
    const trimmed = query.trim();
    if (!trimmed) {
      this.searchResults.set([]);
      this.searchLoading.set(false);
      return;
    }

    this.searchLoading.set(true);
    this.error.set(null);

    try {
      const result = await firstValueFrom(this.moderationService.search(trimmed));
      this.searchResults.set(result.items);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
      this.searchResults.set([]);
    } finally {
      this.searchLoading.set(false);
    }
  }
}
