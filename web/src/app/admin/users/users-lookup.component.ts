import { HttpErrorResponse } from '@angular/common/http';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { EMPTY, firstValueFrom, Observable, of } from 'rxjs';
import {
  catchError,
  debounceTime,
  distinctUntilChanged,
  filter,
  finalize,
  switchMap,
  tap,
} from 'rxjs/operators';

import { ApiErrorService } from '../../i18n/api-error.service';
import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { FormFieldComponent } from '../../shared/ui/form-field/form-field.component';
import { ListingCardComponent } from '../../shared/ui/listing-card/listing-card.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { SearchInputComponent } from '../../shared/ui/search-input/search-input.component';
import {
  AdminUserSearchBy,
  AdminUsersService,
  AdminUserSummary,
} from '../admin-users.service';

@Component({
  selector: 'app-users-lookup',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    EmptyStateComponent,
    FormFieldComponent,
    ListingCardComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    ReactiveFormsModule,
    SearchInputComponent,
    TranslateModule,
  ],
  templateUrl: './users-lookup.component.html',
  styleUrl: './users-lookup.component.scss',
})
export class UsersLookupComponent {
  private readonly usersService = inject(AdminUsersService);
  private readonly apiErrors = inject(ApiErrorService);
  private readonly translate = inject(TranslateService);
  private readonly destroyRef = inject(DestroyRef);

  readonly searchMode = signal<AdminUserSearchBy | null>(null);
  readonly results = signal<AdminUserSummary[]>([]);
  readonly searchLoading = signal(false);
  readonly searchError = signal<string | null>(null);
  readonly fieldErrors = signal<Record<string, string[]>>({});
  readonly phoneSearchAttempted = signal(false);
  readonly emailSearchAttempted = signal(false);

  readonly nameControl = new FormControl('', { nonNullable: true });
  readonly phoneControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.minLength(10)],
  });
  readonly emailControl = new FormControl('', {
    nonNullable: true,
    validators: [Validators.required, Validators.email],
  });

  constructor() {
    this.nameControl.valueChanges
      .pipe(
        debounceTime(300),
        distinctUntilChanged(),
        filter(() => this.searchMode() === 'name'),
        switchMap((raw) => {
          const query = raw.trim();
          if (query.length === 0) {
            this.clearSearchState();
            return of(undefined);
          }

          return this.searchRequest('name', query);
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe();
  }

  selectMode(mode: AdminUserSearchBy): void {
    this.searchMode.set(mode);
    this.clearSearchState();
    this.nameControl.setValue('', { emitEvent: false });
    this.phoneControl.setValue('', { emitEvent: false });
    this.emailControl.setValue('', { emitEvent: false });
    this.phoneSearchAttempted.set(false);
    this.emailSearchAttempted.set(false);
  }

  submitPhoneSearch(): void {
    if (this.searchMode() !== 'phone') {
      return;
    }

    this.phoneControl.markAsTouched();
    if (this.phoneControl.invalid) {
      return;
    }

    this.phoneSearchAttempted.set(true);
    void firstValueFrom(this.searchRequest('phone', this.phoneControl.value.trim()));
  }

  onPhoneEnter(event: Event): void {
    event.preventDefault();
    this.submitPhoneSearch();
  }

  submitEmailSearch(): void {
    if (this.searchMode() !== 'email') {
      return;
    }

    this.emailControl.markAsTouched();
    if (this.emailControl.invalid) {
      return;
    }

    this.emailSearchAttempted.set(true);
    void firstValueFrom(this.searchRequest('email', this.emailControl.value.trim()));
  }

  onEmailEnter(event: Event): void {
    event.preventDefault();
    this.submitEmailSearch();
  }

  fieldError(field: string): string | null {
    const messages = this.fieldErrors()[field];
    return messages?.[0] ?? null;
  }

  hasActiveSearch(): boolean {
    const mode = this.searchMode();
    if (mode === 'name') {
      return this.nameControl.value.trim().length > 0;
    }

    if (mode === 'phone') {
      return this.phoneSearchAttempted();
    }

    if (mode === 'email') {
      return this.emailSearchAttempted();
    }

    return false;
  }

  private searchRequest(
    searchBy: AdminUserSearchBy,
    query: string,
  ): Observable<unknown> {
    this.searchError.set(null);
    this.fieldErrors.set({});
    this.searchLoading.set(true);

    return this.usersService.searchUsers(searchBy, query).pipe(
      tap((response) => {
        this.results.set(response.items);
      }),
      catchError((error) => this.handleSearchFailure(error)),
      finalize(() => {
        this.searchLoading.set(false);
      }),
    );
  }

  private handleSearchFailure(error: unknown): Observable<null> {
    if (this.isCancelledRequest(error)) {
      return EMPTY;
    }

    const body = this.apiErrors.extractBody(error);
    if (body) {
      this.searchError.set(this.apiErrors.summary(body));
      this.fieldErrors.set(this.apiErrors.fieldErrors(body));
    } else {
      this.searchError.set(this.translate.instant('error.internal.error'));
    }

    this.results.set([]);
    return of(null);
  }

  private clearSearchState(): void {
    this.results.set([]);
    this.searchError.set(null);
    this.fieldErrors.set({});
  }

  private isCancelledRequest(error: unknown): boolean {
    if (error instanceof Error && error.name === 'AbortError') {
      return true;
    }

    return error instanceof HttpErrorResponse && error.status === 0;
  }
}
