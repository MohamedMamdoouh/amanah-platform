export type ReportType = 'lost' | 'found';

export type ReportStatus =
  | 'pending_review'
  | 'rejected'
  | 'published'
  | 'claim_in_progress'
  | 'resolved'
  | 'withdrawn'
  | 'removed_by_admin';

export type WithdrawalReason =
  | 'recovered_outside'
  | 'no_longer_needed'
  | 'posted_by_mistake'
  | 'other';

export interface CreateReportRequest {
  type: ReportType;
  categoryCode: string;
  title: string;
  description: string;
  dateLostOrFound: string;
  governorateCode: string;
  areaText?: string | null;
  heldLocation?: string | null;
  hasReward: boolean;
  rewardAmount?: number | null;
  hiddenDetail: string;
  categoryFields: Record<string, string>;
}

export interface UpdateReportRequest {
  categoryCode: string;
  title: string;
  description: string;
  dateLostOrFound: string;
  governorateCode: string;
  areaText?: string | null;
  heldLocation?: string | null;
  hasReward: boolean;
  rewardAmount?: number | null;
  hiddenDetail: string;
  categoryFields: Record<string, string>;
}

export interface CreateReportResponse {
  id: string;
  status: ReportStatus;
}

export interface ReportSummary {
  id: string;
  type: ReportType;
  status: ReportStatus;
  title: string;
  categoryCode: string;
  governorateCode: string;
  createdAt: string;
  hasReward: boolean;
  rewardAmount?: number | null;
}

export interface ReportListResponse {
  items: ReportSummary[];
}

export interface ReportPhoto {
  id: string;
  thumbnailUrl?: string | null;
  sortOrder: number;
}

export interface ReportDetail extends ReportSummary {
  description: string;
  dateLostOrFound: string;
  areaText?: string | null;
  heldLocation?: string | null;
  categoryFields: Record<string, string>;
  hiddenDetail?: string | null;
  withdrawalReason?: string | null;
  rejectionReasonCode?: string | null;
  rejectionNote?: string | null;
  photos: ReportPhoto[];
}

export interface WithdrawReportRequest {
  reason?: WithdrawalReason | null;
}
