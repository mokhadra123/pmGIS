import { HttpParams } from '@angular/common/http';

import { APP_CONFIG } from '@core/config/app-config';
import type { IsoDate } from '@core/models/project';

// Sortable columns. Mirrors the server's ProjectSort allow-list exactly.
export const PROJECT_SORT = {
  name: 'name',
  projectCode: 'projectCode',
  projectType: 'projectTypeName',
  status: 'status',
  startDate: 'startDate',
  endDate: 'endDate',
  activityCount: 'activityCount',
  durationDays: 'durationDays',
  lastModifiedOn: 'lastModifiedOn',
  lastModifiedBy: 'lastModifiedByName',
} as const;

export type ProjectSortKey = (typeof PROJECT_SORT)[keyof typeof PROJECT_SORT];
export type SortDirection = 'asc' | 'desc';

// A map extent as [minLon, minLat, maxLon, maxLat] in WGS84.
export type Bbox = readonly [number, number, number, number];

// Every input the Projects List can vary, in one object. Twin of the server's ProjectQuery.
export interface ProjectQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly sort: ProjectSortKey;
  readonly dir: SortDirection;
  readonly search: string;
  readonly typeIds: readonly number[];
  // Status *names* (`InProgress`), which is what the server parses.
  readonly statuses: readonly string[];
  readonly dateFrom: IsoDate | null;
  readonly dateTo: IsoDate | null;
  // Set by the "only projects in current map extent" toggle.
  readonly bbox: Bbox | null;
  // Set by the Sketch spatial filter. WKT polygon in WGS84.
  readonly polygonWkt: string | null;
}

export const EMPTY_QUERY: ProjectQuery = {
  page: 1,
  pageSize: APP_CONFIG.list.pageSize,
  sort: PROJECT_SORT.lastModifiedOn,
  dir: 'desc',
  search: '',
  typeIds: [],
  statuses: [],
  dateFrom: null,
  dateTo: null,
  bbox: null,
  polygonWkt: null,
};

// True when anything beyond paging and sorting is narrowing the result set.
export function hasActiveFilters(query: ProjectQuery): boolean {
  return (
    query.search.trim().length > 0 ||
    query.typeIds.length > 0 ||
    query.statuses.length > 0 ||
    query.dateFrom !== null ||
    query.dateTo !== null ||
    query.bbox !== null ||
    query.polygonWkt !== null
  );
}

// The single place a query becomes parameters, so the export cannot drift from the grid.
export function toHttpParams(query: ProjectQuery, includePaging = true): HttpParams {
  let params = new HttpParams().set('sort', query.sort).set('dir', query.dir);

  if (includePaging) {
    params = params.set('page', query.page).set('pageSize', query.pageSize);
  }

  const search = query.search.trim();
  if (search) {
    params = params.set('search', search);
  }

  for (const id of query.typeIds) {
    params = params.append('typeIds', id);
  }

  for (const status of query.statuses) {
    params = params.append('statuses', status);
  }

  if (query.dateFrom) params = params.set('dateFrom', query.dateFrom);
  if (query.dateTo) params = params.set('dateTo', query.dateTo);

  if (query.bbox) {
    const [minLon, minLat, maxLon, maxLat] = query.bbox;
    params = params
      .set('minLon', minLon)
      .set('minLat', minLat)
      .set('maxLon', maxLon)
      .set('maxLat', maxLat);
  }

  if (query.polygonWkt) {
    params = params.set('polygonWkt', query.polygonWkt);
  }

  return params;
}
