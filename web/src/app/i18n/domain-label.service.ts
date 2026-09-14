import { inject, Injectable } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';

import { ClaimStatus } from '../claims/models/claim.models';
import { BadgeVariant } from '../shared/ui/badge/badge.component';

export type ReportBadgeContext = 'browse' | 'mine';

@Injectable({ providedIn: 'root' })
export class DomainLabelService {
  private readonly translate = inject(TranslateService);

  reportType(type: string): string {
    return this.translate.instant(`reports.type.${type}`);
  }

  reportStatus(status: string): string {
    return this.translate.instant(`reports.status.${status}`);
  }

  reportBadgeVariant(
    status: string,
    context: ReportBadgeContext = 'browse',
  ): BadgeVariant {
    if (context === 'browse') {
      return status === 'claim_in_progress' ? 'claim' : 'published';
    }

    if (status === 'published') {
      return 'published';
    }
    if (status === 'rejected') {
      return 'rejected';
    }
    if (status === 'claim_in_progress') {
      return 'claim';
    }

    return 'pending';
  }

  claimStatus(status: ClaimStatus): string {
    return this.translate.instant(`claims.status.${status}`);
  }

  claimBadgeVariant(status: ClaimStatus): BadgeVariant {
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
}
