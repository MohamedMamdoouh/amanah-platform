import { DatePipe } from '@angular/common';
import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { firstValueFrom } from 'rxjs';

import { AlertComponent } from '../../shared/ui/alert/alert.component';
import { BadgeVariant } from '../../shared/ui/badge/badge.component';
import { ButtonComponent } from '../../shared/ui/button/button.component';
import { EmptyStateComponent } from '../../shared/ui/empty-state/empty-state.component';
import { ListingCardComponent } from '../../shared/ui/listing-card/listing-card.component';
import { LoadingIndicatorComponent } from '../../shared/ui/loading-indicator/loading-indicator.component';
import { PageHeaderComponent } from '../../shared/ui/page-header/page-header.component';
import { ClaimService } from '../claim.service';
import { ClaimStatus, MyClaimSummary } from '../models/claim.models';

@Component({
  selector: 'app-my-claims',
  standalone: true,
  imports: [
    AlertComponent,
    ButtonComponent,
    DatePipe,
    EmptyStateComponent,
    ListingCardComponent,
    LoadingIndicatorComponent,
    PageHeaderComponent,
    RouterLink,
    TranslateModule,
  ],
  templateUrl: './my-claims.component.html',
  styleUrl: './my-claims.component.scss',
})
export class MyClaimsComponent implements OnInit {
  private readonly claimService = inject(ClaimService);
  private readonly translate = inject(TranslateService);

  readonly loading = signal(true);
  readonly error = signal<string | null>(null);
  readonly claims = signal<MyClaimSummary[]>([]);
  readonly withdrawingId = signal<string | null>(null);
  readonly actionError = signal<string | null>(null);

  ngOnInit(): void {
    void this.loadClaims();
  }

  typeLabel(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  statusLabel(status: ClaimStatus): string {
    return this.translate.instant(`claims.status.${status}`);
  }

  badgeVariant(status: ClaimStatus): BadgeVariant {
    switch (status) {
      case 'approved':
        return 'approved';
      case 'rejected':
        return 'rejected';
      case 'pending':
        return 'pending';
      default:
        return 'neutral';
    }
  }

  reportLink(claim: MyClaimSummary): string[] {
    return [`/${claim.reportType}`, claim.reportId];
  }

  canWithdraw(claim: MyClaimSummary): boolean {
    return claim.status === 'pending';
  }

  async withdraw(claim: MyClaimSummary): Promise<void> {
    if (this.withdrawingId()) {
      return;
    }

    this.withdrawingId.set(claim.id);
    this.actionError.set(null);

    try {
      await firstValueFrom(this.claimService.withdraw(claim.id));
      this.claims.update((current) =>
        current.map((item) =>
          item.id === claim.id ? { ...item, status: 'withdrawn' } : item,
        ),
      );
    } catch (error) {
      this.actionError.set(this.parseError(error));
    } finally {
      this.withdrawingId.set(null);
    }
  }

  private async loadClaims(): Promise<void> {
    this.loading.set(true);
    this.error.set(null);

    try {
      const response = await firstValueFrom(this.claimService.getMine());
      this.claims.set(response.items);
    } catch {
      this.error.set(this.translate.instant('error.internal.error'));
    } finally {
      this.loading.set(false);
    }
  }

  private parseError(error: unknown): string {
    if (error instanceof HttpErrorResponse && error.status === 409) {
      return this.translate.instant('claims.review.invalid_status');
    }

    return this.translate.instant('error.internal.error');
  }
}
