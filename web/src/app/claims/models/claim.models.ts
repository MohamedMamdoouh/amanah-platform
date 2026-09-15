export interface SubmitClaimRequest {
  submittedAnswer: string;
}

export interface SubmitClaimResponse {
  id: string;
  status: string;
}

export type ClaimStatus =
  | 'pending'
  | 'approved'
  | 'rejected'
  | 'withdrawn'
  | 'cancelled';

export interface MyClaimSummary {
  id: string;
  status: ClaimStatus;
  submittedAt: string;
  reviewedAt?: string | null;
  decisionReason?: string | null;
  reportId: string;
  reportType: 'lost' | 'found';
  reportTitle: string;
  reporterDisplayName: string;
}

export interface ReportClaimSummary {
  id: string;
  status: ClaimStatus;
  submittedAnswer: string;
  hasPhoto: boolean;
  submittedAt: string;
  reviewedAt?: string | null;
  decisionReason?: string | null;
  attemptNumber: number;
  claimantDisplayName: string;
}

export interface ResolutionState {
  reporterConfirmedAt?: string | null;
  claimantConfirmedAt?: string | null;
  resolvedAt?: string | null;
  currentUserHasConfirmed: boolean;
  currentUserCanCancel: boolean;
}

export interface ClaimDetail {
  id: string;
  status: ClaimStatus;
  submittedAnswer: string;
  hasPhoto: boolean;
  submittedAt: string;
  reviewedAt?: string | null;
  reviewerDecision?: string | null;
  decisionReason?: string | null;
  attemptNumber: number;
  chatThreadId?: string | null;
  reportId: string;
  reportType: 'lost' | 'found';
  reportStatus: string;
  reportTitle: string;
  claimantDisplayName: string;
  reporterDisplayName: string;
  resolution?: ResolutionState | null;
}

export interface ClaimPhotoPresignResponse {
  url: string;
}
