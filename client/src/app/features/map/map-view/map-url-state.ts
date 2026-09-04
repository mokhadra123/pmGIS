import { Injectable, inject } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';

import type { LatLng } from '@core/state/map-bridge';

export interface RestoredMapState {
  readonly centre: LatLng | null;
  readonly zoom: number | null;
  readonly selectedProjectId: number | null;
}

// Map state in the URL query string, so a view can be bookmarked and shared.
@Injectable({ providedIn: 'root' })
export class MapUrlState {
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  read(): RestoredMapState {
    const params = this.route.snapshot.queryParamMap;

    const lat = toNumber(params.get('lat'));
    const lon = toNumber(params.get('lon'));
    const zoom = toNumber(params.get('z'));
    const selected = toNumber(params.get('sel'));

    return {
      centre: lat !== null && lon !== null ? { latitude: lat, longitude: lon } : null,
      zoom,
      selectedProjectId: selected,
    };
  }

  writeView(centre: LatLng, zoom: number): void {
    this.merge({
      lat: centre.latitude.toFixed(5),
      lon: centre.longitude.toFixed(5),
      z: zoom,
    });
  }

  writeSelected(projectId: number | null): void {
    this.merge({ sel: projectId });
  }

  private merge(queryParams: Record<string, string | number | null>): void {
    void this.router.navigate([], {
      relativeTo: this.route,
      queryParams,
      queryParamsHandling: 'merge',
      replaceUrl: true,
    });
  }
}

function toNumber(value: string | null): number | null {
  if (value === null || value.trim() === '') return null;
  const parsed = Number(value);
  return Number.isFinite(parsed) ? parsed : null;
}
