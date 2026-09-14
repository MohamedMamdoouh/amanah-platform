import { firstValueFrom } from 'rxjs';

import { ClaimService } from '../claim.service';
import { ClaimDetail } from '../models/claim.models';

export function isResolutionEligibleReportStatus(status: string): boolean {
  return status === 'claim_in_progress' || status === 'resolved';
}

export function showResolutionActions(params: {
  reportStatus: string;
  approvedClaimId: string | null;
  isLoggedIn?: boolean;
}): boolean {
  if (!params.approvedClaimId) {
    return false;
  }

  if (params.isLoggedIn === false) {
    return false;
  }

  return isResolutionEligibleReportStatus(params.reportStatus);
}

export function showClaimResolutionSection(detail: ClaimDetail | null): boolean {
  if (!detail || detail.status !== 'approved') {
    return false;
  }

  return isResolutionEligibleReportStatus(detail.reportStatus);
}

export function canConfirmResolution(detail: ClaimDetail | null): boolean {
  return (
    detail?.reportStatus === 'claim_in_progress' &&
    detail.resolution?.currentUserHasConfirmed !== true
  );
}

export function canCancelResolution(detail: ClaimDetail | null): boolean {
  return (
    detail?.reportStatus === 'claim_in_progress' &&
    detail.resolution?.currentUserCanCancel === true
  );
}

export function resolutionStatusMessageKey(
  detail: ClaimDetail | null,
): string | null {
  if (!detail?.resolution) {
    if (detail?.reportStatus === 'claim_in_progress') {
      return 'claims.resolution.status_pending';
    }

    return null;
  }

  const resolution = detail.resolution;

  if (resolution.resolvedAt) {
    return 'claims.resolution.status_resolved';
  }

  if (resolution.currentUserHasConfirmed) {
    return 'claims.resolution.status_awaiting_counterparty';
  }

  const counterpartyConfirmed =
    resolution.reporterConfirmedAt ?? resolution.claimantConfirmedAt;
  if (counterpartyConfirmed) {
    return 'claims.resolution.status_awaiting_you';
  }

  return 'claims.resolution.status_pending';
}

export async function loadReporterApprovedClaimId(
  claimService: ClaimService,
  reportId: string,
  reportStatus: string,
): Promise<string | null> {
  if (!isResolutionEligibleReportStatus(reportStatus)) {
    return null;
  }

  try {
    const claims = await firstValueFrom(claimService.getByReport(reportId));
    return claims.find((claim) => claim.status === 'approved')?.id ?? null;
  } catch {
    return null;
  }
}

export async function loadClaimantApprovedClaimId(
  claimService: ClaimService,
  reportId: string,
  reportStatus: string,
): Promise<string | null> {
  if (!isResolutionEligibleReportStatus(reportStatus)) {
    return null;
  }

  try {
    const claim = await firstValueFrom(
      claimService.findApprovedClaimForReport(reportId),
    );
    return claim?.id ?? null;
  } catch {
    return null;
  }
}
