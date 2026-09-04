import { ChangeDetectionStrategy, Component, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

import { LookupsApi } from '@core/api/lookups-api';
import { MapBridge } from '@core/state/map-bridge';
import { ProjectsStore } from '@core/state/projects-store';

// Project Type, Status and a date range, plus the two spatial filters.
@Component({
  selector: 'app-project-filters',
  standalone: true,
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-filters.html',
  styleUrl: './project-filters.scss',
})
export class ProjectFilters {
  private readonly lookups = inject(LookupsApi);
  protected readonly store = inject(ProjectsStore);
  protected readonly mapBridge = inject(MapBridge);

  protected readonly projectTypes = toSignal(
    this.lookups.projectTypes().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly statuses = toSignal(
    this.lookups.projectStatuses().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly expanded = signal(false);

  // Count of narrowing criteria, shown on the toggle so a collapsed panel still informs.
  protected readonly activeCount = computed(() => {
    const q = this.store.query();
    return (
      q.typeIds.length +
      q.statuses.length +
      (q.dateFrom ? 1 : 0) +
      (q.dateTo ? 1 : 0) +
      (q.bbox ? 1 : 0) +
      (q.polygonWkt ? 1 : 0)
    );
  });

  protected toggleType(id: number): void {
    const current = this.store.query().typeIds;
    const next = current.includes(id) ? current.filter((t) => t !== id) : [...current, id];
    this.store.patch({ typeIds: next, page: 1 });
  }

  protected toggleStatus(code: string): void {
    const current = this.store.query().statuses;
    const next = current.includes(code) ? current.filter((s) => s !== code) : [...current, code];
    this.store.patch({ statuses: next, page: 1 });
  }

  protected setDateFrom(value: string): void {
    this.store.patch({ dateFrom: value || null, page: 1 });
  }

  protected setDateTo(value: string): void {
    this.store.patch({ dateTo: value || null, page: 1 });
  }

  // "Only projects in current map extent".
  protected toggleExtentFilter(): void {
    const enabled = !this.mapBridge.extentFilterEnabled();
    this.mapBridge.extentFilterEnabled.set(enabled);
    this.store.patch({ bbox: enabled ? this.mapBridge.extent() : null, page: 1 });
  }

  protected clearSketch(): void {
    this.mapBridge.sketchWkt.set(null);
    this.store.patch({ polygonWkt: null, page: 1 });
  }

  protected clearAll(): void {
    this.mapBridge.extentFilterEnabled.set(false);
    this.mapBridge.sketchWkt.set(null);
    this.store.clearFilters();
  }
}
