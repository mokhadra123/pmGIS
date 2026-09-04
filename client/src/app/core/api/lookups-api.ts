import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { catchError, shareReplay, throwError, type Observable } from 'rxjs';

import { APP_CONFIG } from '@core/config/app-config';
import type { LookupItem, UserLookupItem } from '@core/models/lookup';

@Injectable({ providedIn: 'root' })
export class LookupsApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${APP_CONFIG.api.baseUrl}/lookups`;

  private readonly cached = new Map<string, Observable<unknown>>();

  // Fetched once per session and replayed. A failure is evicted rather than cached, or a
  // single blip would replay that error to every later caller for the app's lifetime.
  private cache<T>(path: string): Observable<T> {
    const cached = this.cached.get(path);
    if (cached) {
      return cached as Observable<T>;
    }

    const request = this.http.get<T>(`${this.base}/${path}`).pipe(
      catchError((error: unknown) => {
        this.cached.delete(path);
        return throwError(() => error);
      }),
      shareReplay({ bufferSize: 1, refCount: false }),
    );

    this.cached.set(path, request as Observable<unknown>);
    return request;
  }

  projectTypes(): Observable<readonly LookupItem[]> {
    return this.cache<readonly LookupItem[]>('project-types');
  }

  projectStatuses(): Observable<readonly LookupItem[]> {
    return this.cache<readonly LookupItem[]>('project-statuses');
  }

  activityStatuses(): Observable<readonly LookupItem[]> {
    return this.cache<readonly LookupItem[]>('activity-statuses');
  }

  users(): Observable<readonly UserLookupItem[]> {
    return this.cache<readonly UserLookupItem[]>('users');
  }
}
