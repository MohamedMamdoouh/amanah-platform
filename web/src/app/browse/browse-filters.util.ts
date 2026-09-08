import { ParamMap } from '@angular/router';

import { ReportType } from '../reports/models/report.models';
import { BrowseFilters, BrowseReportsQuery } from './models/browse.models';

export function filtersFromParams(params: ParamMap): BrowseFilters {
  const page = Number(params.get('page') ?? '1');
  const type = params.get('type') ?? '';

  return {
    q: params.get('q') ?? '',
    category: params.get('category') ?? '',
    governorate: params.get('governorate') ?? '',
    type: type === 'lost' || type === 'found' ? type : '',
    dateFrom: params.get('dateFrom') ?? '',
    dateTo: params.get('dateTo') ?? '',
    page: Number.isFinite(page) && page > 0 ? page : 1,
  };
}

export function toBrowseQuery(filters: BrowseFilters): BrowseReportsQuery {
  return {
    q: filters.q || undefined,
    category: filters.category || undefined,
    governorate: filters.governorate || undefined,
    type: (filters.type as ReportType) || undefined,
    dateFrom: filters.dateFrom || undefined,
    dateTo: filters.dateTo || undefined,
    page: filters.page,
    pageSize: 20,
  };
}
