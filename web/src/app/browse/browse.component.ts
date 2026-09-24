import { Component, computed, effect, inject, OnInit, signal } from '@angular/core';
import { AppDatePipe } from '../i18n/app-date.pipe';
import {
  takeUntilDestroyed,
  toObservable,
  toSignal,
} from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom, of } from 'rxjs';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  map,
  startWith,
  switchMap,
} from 'rxjs/operators';

import { CatalogService } from '../catalog/catalog.service';
import { CatalogLabelService } from '../i18n/catalog-label.service';
import { DomainLabelService } from '../i18n/domain-label.service';
import { AlertComponent } from '../shared/ui/alert/alert.component';
import { EmptyStateComponent } from '../shared/ui/empty-state/empty-state.component';
import { FormFieldComponent } from '../shared/ui/form-field/form-field.component';
import { ListingCardComponent } from '../shared/ui/listing-card/listing-card.component';
import { LoadingIndicatorComponent } from '../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../shared/ui/page-header/page-header.component';
import { SearchInputComponent } from '../shared/ui/search-input/search-input.component';
import { filtersFromParams, toBrowseQuery } from './browse-filters.util';
import { BrowseService } from './browse.service';
import {
  BrowseViewState,
  EMPTY_BROWSE_CATALOG,
  EMPTY_BROWSE_FILTERS,
  PaginatedResponse,
  PublicReportSummary,
} from './models/browse.models';

type BrowseFilterKey = Exclude<keyof typeof EMPTY_BROWSE_FILTERS, 'q' | 'page'>;

@Component({
  selector: 'app-browse',
  standalone: true,
  imports: [
    AppDatePipe,
    AlertComponent,
    EmptyStateComponent,
    FormFieldComponent,
    ListingCardComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    SearchInputComponent,
    TranslateModule,
  ],
  templateUrl: './browse.component.html',
  styleUrl: './browse.component.scss',
})
export class BrowseComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly browseService = inject(BrowseService);
  private readonly catalogService = inject(CatalogService);
  private readonly catalogLabels = inject(CatalogLabelService);
  protected readonly domainLabels = inject(DomainLabelService);
  private readonly translate = inject(TranslateService);

  private readonly catalogFailed = signal(false);

  readonly filters = toSignal(
    this.route.queryParamMap.pipe(map(filtersFromParams)),
    { initialValue: EMPTY_BROWSE_FILTERS },
  );
  readonly searchControl = new FormControl('', { nonNullable: true });
  readonly catalog = signal(EMPTY_BROWSE_CATALOG);

  readonly browseView = toSignal(
    toObservable(this.filters).pipe(
      switchMap((filters) =>
        this.browseService.getReports(toBrowseQuery(filters)).pipe(
          map((response) => ({ status: 'ready' as const, response })),
          catchError(() => of({ status: 'error' as const })),
          startWith({ status: 'loading' as const }),
        ),
      ),
    ),
    { initialValue: { status: 'loading' } as BrowseViewState },
  );

  readonly loading = computed(() => this.browseView().status === 'loading');
  readonly error = computed(() =>
    this.browseView().status === 'error' || this.catalogFailed()
      ? this.translate.instant('error.internal.error')
      : null,
  );
  readonly response = computed(
    (): PaginatedResponse<PublicReportSummary> | null => {
      const view = this.browseView();
      return view.status === 'ready' ? view.response : null;
    },
  );
  readonly items = computed(() => this.response()?.items ?? []);
  readonly totalPages = computed(() => this.response()?.totalPages ?? 0);
  readonly totalCount = computed(() => this.response()?.totalCount ?? 0);
  readonly currentPage = computed(() => this.response()?.page ?? 1);
  readonly pageNumbers = computed(() =>
    Array.from({ length: this.totalPages() }, (_, index) => index + 1),
  );

  constructor() {
    effect(() => {
      this.syncSearchControl(this.filters().q);
    });

    this.searchControl.valueChanges
      .pipe(debounceTime(300), distinctUntilChanged(), takeUntilDestroyed())
      .subscribe((query) => {
        this.navigateWithParams({ q: query.trim() || null, page: null });
      });
  }

  ngOnInit(): void {
    void this.loadCatalog();
  }

  categoryLabel(code: string): string {
    return this.catalogLabels.category(code);
  }

  governorateLabel(code: string): string {
    return this.catalogLabels.governorate(code);
  }

  detailRoute(report: PublicReportSummary): string[] {
    return report.type === 'lost'
      ? ['/lost', report.id]
      : ['/found', report.id];
  }

  onFilterChange(key: BrowseFilterKey, value: string): void {
    this.navigateWithParams({ [key]: value || null, page: null });
  }

  goToPage(page: number): void {
    if (page < 1 || page > this.totalPages() || page === this.currentPage()) {
      return;
    }

    this.navigateWithParams({ page: page === 1 ? null : page });
  }

  private syncSearchControl(query: string): void {
    if (this.searchControl.value !== query) {
      this.searchControl.setValue(query, { emitEvent: false });
    }
  }

  private navigateWithParams(
    patch: Partial<Record<keyof typeof EMPTY_BROWSE_FILTERS, string | number | null>>,
  ): void {
    const current = this.filters();
    const next = { ...current, ...patch };

    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams: {
        q: next.q || null,
        category: next.category || null,
        governorate: next.governorate || null,
        type: next.type || null,
        dateFrom: next.dateFrom || null,
        dateTo: next.dateTo || null,
        page: next.page === 1 ? null : next.page,
      },
      queryParamsHandling: 'merge',
    });
  }

  private async loadCatalog(): Promise<void> {
    try {
      const [categories, governorates] = await Promise.all([
        firstValueFrom(this.catalogService.getCategories()),
        firstValueFrom(this.catalogService.getGovernorates()),
      ]);

      this.catalog.set({
        categories: [...categories.items].sort(
          (a, b) => a.sortOrder - b.sortOrder,
        ),
        governorates: [...governorates.items].sort(
          (a, b) => a.sortOrder - b.sortOrder,
        ),
      });
    } catch {
      this.catalogFailed.set(true);
    }
  }
}
