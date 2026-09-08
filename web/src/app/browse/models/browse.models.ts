import { Category, Governorate } from '../../catalog/models/catalog.models';
import {
  ReportPhoto,
  ReportStatus,
  ReportType,
} from '../../reports/models/report.models';

export type BrowseViewState =
  | { status: 'loading' }
  | { status: 'error' }
  | { status: 'ready'; response: PaginatedResponse<PublicReportSummary> };

export interface BrowseCatalog {
  categories: Category[];
  governorates: Governorate[];
}

export const EMPTY_BROWSE_CATALOG: BrowseCatalog = {
  categories: [],
  governorates: [],
};

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

export interface BrowseFilters {
  q: string;
  category: string;
  governorate: string;
  type: string;
  dateFrom: string;
  dateTo: string;
  page: number;
}

export const EMPTY_BROWSE_FILTERS: BrowseFilters = {
  q: '',
  category: '',
  governorate: '',
  type: '',
  dateFrom: '',
  dateTo: '',
  page: 1,
};

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
