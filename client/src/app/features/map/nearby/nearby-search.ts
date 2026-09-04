import { DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';

import { ProjectsApi } from '@core/api/projects-api';
import { toApiFailure } from '@core/api/problem-details';
import { APP_CONFIG } from '@core/config/app-config';
import type { NearbyProject } from '@core/models/project';
import { MapBridge } from '@core/state/map-bridge';

// Projects within a chosen distance of a clicked point, nearest first.
@Component({
  selector: 'app-nearby-search',
  standalone: true,
  imports: [DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './nearby-search.html',
  styleUrl: './nearby-search.scss',
})
export class NearbySearch {
  private readonly api = inject(ProjectsApi);
  private readonly router = inject(Router);
  protected readonly bridge = inject(MapBridge);

  protected readonly open = signal(false);
  protected readonly radiusKm = signal(10);
  protected readonly results = signal<readonly NearbyProject[]>([]);
  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);
  protected readonly searched = signal(false);

  protected readonly point = computed(() => this.bridge.nearbyPoint());
  protected readonly isEmpty = computed(
    () => this.searched() && !this.loading() && this.results().length === 0,
  );

  constructor() {
    // Picking a point on the map is what triggers the query, so the user never has to
    // press a second button after clicking.
    effect(() => {
      const point = this.bridge.nearbyPoint();
      if (point) {
        void this.run(point.latitude, point.longitude);
      }
    });
  }

  protected toggle(): void {
    const next = !this.open();
    this.open.set(next);
    next ? this.bridge.startNearbyPicking() : this.reset();
  }

  protected pickAgain(): void {
    this.results.set([]);
    this.searched.set(false);
    this.failure.set(null);
    this.bridge.nearbyPoint.set(null);
    this.bridge.startNearbyPicking();
  }

  protected setRadius(value: string): void {
    const parsed = Number(value);
    if (!Number.isFinite(parsed)) {
      return;
    }

    this.radiusKm.set(Math.min(500, Math.max(1, Math.round(parsed))));

    // Re-run against the point already chosen, so changing the radius is immediate.
    const point = this.bridge.nearbyPoint();
    if (point) {
      void this.run(point.latitude, point.longitude);
    }
  }

  // Centres the map on a result and selects it, reusing the existing selection channel.
  protected goTo(project: NearbyProject): void {
    if (project.latitude === null || project.longitude === null) {
      return;
    }

    this.bridge.selectFromList(project.id);
    this.bridge.goTo({
      latitude: project.latitude,
      longitude: project.longitude,
      zoom: APP_CONFIG.map.projectZoom,
    });
  }

  protected view(project: NearbyProject): void {
    void this.router.navigate(['/projects', project.id]);
  }

  private reset(): void {
    this.results.set([]);
    this.searched.set(false);
    this.failure.set(null);
    this.bridge.clearNearby();
  }

  private run(latitude: number, longitude: number): void {
    this.loading.set(true);
    this.failure.set(null);

    this.api.nearby(latitude, longitude, this.radiusKm(), 25).subscribe({
      next: (rows) => {
        this.results.set(rows);
        this.searched.set(true);
        this.loading.set(false);
      },
      error: (error: unknown) => {
        this.failure.set(toApiFailure(error).message);
        this.searched.set(true);
        this.loading.set(false);
      },
    });
  }
}
