import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import type { Observable } from 'rxjs';

import { APP_CONFIG } from '@core/config/app-config';
import type { ReconciliationReport } from '@core/models/gis';

// The single data-access point for /api/gis.
@Injectable({ providedIn: 'root' })
export class GisApi {
  private readonly http = inject(HttpClient);
  private readonly base = `${APP_CONFIG.api.baseUrl}/gis`;

  // Compares the feature layer against the database. Not cached: the whole point is to
  // report the state at the moment it is asked for.
  reconciliation(): Observable<ReconciliationReport> {
    return this.http.get<ReconciliationReport>(`${this.base}/reconciliation`);
  }
}
