import {
  ReportPhoto,
  ReportStatus,
  ReportType,
} from '../../reports/models/report.models';

export interface PaginatedResponse<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PublicReportSummary {
  id: string;
  type: ReportType;
  status: ReportStatus;
  title: string;
  categoryCode: string;
  governorateCode: string;
  publishedAt?: string | null;
  hasReward: boolean;
  rewardAmount?: number | null;
  reporterDisplayName: string;
  thumbnailUrl?: string | null;
  areaText?: string | null;
}

export interface PublicReportDetail {
  id: string;
  type: ReportType;
  status: ReportStatus;
  title: string;
  categoryCode: string;
  governorateCode: string;
  publishedAt?: string | null;
  description: string;
  dateLostOrFound: string;
  areaText?: string | null;
  heldLocation?: string | null;
  hasReward: boolean;
  rewardAmount?: number | null;
  reporterDisplayName: string;
  categoryFields: Record<string, string>;
  photos: ReportPhoto[];
}

export interface BrowseReportsQuery {
  q?: string;
  category?: string;
  governorate?: string;
  type?: ReportType;
  dateFrom?: string;
  dateTo?: string;
  page?: number;
  pageSize?: number;
}
