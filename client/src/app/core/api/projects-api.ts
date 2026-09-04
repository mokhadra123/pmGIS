import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { APP_CONFIG } from '@core/config/app-config';
import type {
  BulkDeleteResult,
  CodeAvailability,
  NearbyProject,
  ProjectDetail,
  ProjectListItem,
  SaveProjectPayload,
  SaveProjectResult,
} from '@core/models/project';

import { toHttpParams, type ProjectQuery } from './project-query';
import { type PagedResult } from '@core/models/paging';

// The single data-access point for /api/projects. Components never build a URL.
@Injectable({ providedIn: 'root' })
export class ProjectsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${APP_CONFIG.api.baseUrl}/projects`;

  page(query: ProjectQuery): Observable<PagedResult<ProjectListItem>> {
    return this.http.get<PagedResult<ProjectListItem>>(this.base, {
      params: toHttpParams(query),
    });
  }

  get(id: number): Observable<ProjectDetail> {
    return this.http.get<ProjectDetail>(`${this.base}/${id}`);
  }

  create(payload: SaveProjectPayload): Observable<SaveProjectResult> {
    return this.http.post<SaveProjectResult>(this.base, payload);
  }

  update(id: number, payload: SaveProjectPayload): Observable<SaveProjectResult> {
    return this.http.put<SaveProjectResult>(`${this.base}/${id}`, payload);
  }

  delete(id: number): Observable<void> {
    return this.http.delete<void>(`${this.base}/${id}`);
  }

  bulkDelete(projectIds: readonly number[]): Observable<BulkDeleteResult> {
    return this.http.post<BulkDeleteResult>(`${this.base}/bulk-delete`, { projectIds });
  }

  // Backs the form's uniqueness check on blur; the server checks again at submit.
  checkCode(code: string, excludeProjectId?: number | null): Observable<CodeAvailability> {
    let params: Record<string, string | number> = { code };
    if (excludeProjectId != null) {
      params = { ...params, excludeProjectId };
    }
    return this.http.get<CodeAvailability>(`${this.base}/code-available`, { params });
  }

  // Projects within a radius of a clicked point, nearest first.
  nearby(
    latitude: number,
    longitude: number,
    radiusKm: number,
    limit = 25,
  ): Observable<readonly NearbyProject[]> {
    return this.http.get<readonly NearbyProject[]>(`${this.base}/nearby`, {
      params: { latitude, longitude, radiusKm, limit },
    });
  }

  exportUrl(query: ProjectQuery): string {
    const params = toHttpParams(query, false).toString();
    return `${this.base}/export?${params}`;
  }
}
