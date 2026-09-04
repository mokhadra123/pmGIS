import { DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  computed,
  effect,
  HostListener,
  inject,
  signal,
} from '@angular/core';
import { Router } from '@angular/router';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

import { LookupsApi } from '@core/api/lookups-api';
import { ProjectsApi } from '@core/api/projects-api';
import { toApiFailure } from '@core/api/problem-details';
import { PROJECT_SORT, type ProjectSortKey } from '@core/api/project-query';
import { APP_CONFIG } from '@core/config/app-config';
import type { ProjectListItem } from '@core/models/project';
import { MapBridge } from '@core/state/map-bridge';
import { ProjectsStore } from '@core/state/projects-store';
import { DeleteConfirm } from '@shared/confirm/delete-confirm';
import { Notifications } from '@shared/notifications/notifications';

import { ProjectFilters } from '../project-filters/project-filters';

interface PendingDelete {
  readonly ids: readonly number[];
  // The text the user must type: a project code, or the count for a bulk delete.
  readonly challenge: string;
  readonly title: string;
  readonly body: string;
}

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [DatePipe, DecimalPipe, ProjectFilters, DeleteConfirm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-list.html',
  styleUrl: './project-list.scss',
})
export class ProjectList {
  protected readonly store = inject(ProjectsStore);
  protected readonly mapBridge = inject(MapBridge);
  private readonly api = inject(ProjectsApi);
  private readonly lookups = inject(LookupsApi);
  private readonly notifications = inject(Notifications);
  private readonly router = inject(Router);

  protected readonly SORT = PROJECT_SORT;

  // Status display names, keyed by enum value, for the Status column.
  private readonly statusLookup = toSignal(
    this.lookups.projectStatuses().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly statusNames = computed(() => {
    const map = new Map<number, string>();
    for (const item of this.statusLookup()) {
      map.set(item.id, item.name);
    }
    return map;
  });

  protected readonly pending = signal<PendingDelete | null>(null);
  protected readonly deleting = signal(false);

  protected readonly columns = [
    { key: PROJECT_SORT.name, label: 'Project Name' },
    { key: PROJECT_SORT.projectCode, label: 'Code' },
    { key: PROJECT_SORT.projectType, label: 'Type' },
    { key: PROJECT_SORT.startDate, label: 'Start' },
    { key: PROJECT_SORT.endDate, label: 'End' },
    { key: PROJECT_SORT.status, label: 'Status' },
    { key: PROJECT_SORT.activityCount, label: 'Activities' },
    { key: PROJECT_SORT.durationDays, label: 'Duration' },
    { key: PROJECT_SORT.lastModifiedBy, label: 'Modified By' },
    { key: PROJECT_SORT.lastModifiedOn, label: 'Modified On' },
  ] as const;

  constructor() {
    // When the map reports a new extent and the extent filter is on, refilter the list.
    effect(() => {
      const extent = this.mapBridge.extent();
      if (this.mapBridge.extentFilterEnabled() && extent) {
        this.store.patch({ bbox: extent, page: 1 });
      }
    });

    // A shape drawn on the map filters the list; erasing it restores the full set.
    effect(() => {
      const wkt = this.mapBridge.sketchWkt();
      if (wkt !== this.store.query().polygonWkt) {
        this.store.patch({ polygonWkt: wkt, page: 1 });
      }
    });
  }

  // ----- selection -----------------------------------------------------------

  protected select(row: ProjectListItem): void {
    const alreadySelected = this.mapBridge.selectedProjectId() === row.id;
    this.mapBridge.selectFromList(alreadySelected ? null : row.id);
  }

  protected isSelected(row: ProjectListItem): boolean {
    return this.mapBridge.selectedProjectId() === row.id;
  }

  // ----- sorting and paging --------------------------------------------------

  protected sortIndicator(key: ProjectSortKey): string {
    const q = this.store.query();
    if (q.sort !== key) return '';
    return q.dir === 'asc' ? '▲' : '▼';
  }

  // Value for the header cell's aria-sort.
  protected ariaSort(key: ProjectSortKey): 'ascending' | 'descending' | 'none' {
    const q = this.store.query();
    if (q.sort !== key) return 'none';
    return q.dir === 'asc' ? 'ascending' : 'descending';
  }

  // Row whose overflow menu is open, or null.
  protected readonly openMenuId = signal<number | null>(null);

  // Viewport coordinates for the open menu.
  protected readonly menuPosition = signal<{ top: number; right: number }>({
    top: 0,
    right: 0,
  });

  // The row the open menu belongs to.
  protected readonly menuRow = signal<ProjectListItem | null>(null);

  protected toggleMenu(row: ProjectListItem, event: Event): void {
    event.stopPropagation();

    if (this.openMenuId() === row.id) {
      this.closeMenu();
      return;
    }

    this.menuRow.set(row);

    const button = event.currentTarget as HTMLElement;
    const rect = button.getBoundingClientRect();

    this.menuPosition.set({
      top: rect.bottom + 4,
      right: Math.max(8, window.innerWidth - rect.right),
    });
    this.openMenuId.set(row.id);
  }

  protected closeMenu(): void {
    this.openMenuId.set(null);
    this.menuRow.set(null);
  }

  // A fixed menu would float away from its row, so any scroll dismisses it.
  @HostListener('window:scroll')
  @HostListener('document:scroll')
  protected onAnyScroll(): void {
    if (this.openMenuId() !== null) {
      this.closeMenu();
    }
  }

  @HostListener('window:resize')
  protected onResize(): void {
    this.closeMenu();
  }

  // A click anywhere else, or Escape, dismisses the open overflow menu.
  @HostListener('document:click')
  protected onDocumentClick(): void {
    this.closeMenu();
  }

  @HostListener('document:keydown.escape')
  protected onEscape(): void {
    this.closeMenu();
  }

  // Placeholder rows shown while a page is loading, sized to the requested page.
  protected readonly skeletonRows = computed(() =>
    Array.from({ length: Math.min(this.store.query().pageSize, 12) }, (_, i) => i),
  );

  protected onSearch(value: string): void {
    this.store.search(value);
  }

  // ----- row actions ---------------------------------------------------------

  protected view(row: ProjectListItem): void {
    void this.router.navigate(['/projects', row.id]);
  }

  protected edit(row: ProjectListItem): void {
    void this.router.navigate(['/projects', row.id, 'edit']);
  }

  protected add(): void {
    void this.router.navigate(['/projects/new']);
  }

  // Keys off the coordinates, not hasLocation, which means "has a feature in the layer".
  protected canZoom(row: ProjectListItem): boolean {
    return row.latitude !== null && row.longitude !== null;
  }

  protected zoomTo(row: ProjectListItem): void {
    if (row.latitude === null || row.longitude === null) {
      return;
    }
    this.mapBridge.selectFromList(row.id);
    this.mapBridge.goTo({
      latitude: row.latitude,
      longitude: row.longitude,
      zoom: APP_CONFIG.map.projectZoom,
    });
  }

  protected confirmDelete(row: ProjectListItem): void {
    this.pending.set({
      ids: [row.id],
      challenge: row.projectCode,
      title: `Delete ${row.name}?`,
      body:
        'This removes the project, its activities and its point on the map. ' +
        'The three are deleted as one unit — if any part fails, nothing is removed.',
    });
  }

  protected confirmBulkDelete(): void {
    const ids = [...this.store.checked()];
    if (ids.length === 0) return;

    this.pending.set({
      ids,
      challenge: String(ids.length),
      title: `Delete ${ids.length} projects?`,
      body:
        `This removes ${ids.length} projects, their activities and their points on the map. ` +
        'Each project is deleted transactionally and the result is reported per project.',
    });
  }

  protected cancelDelete(): void {
    this.pending.set(null);
  }

  // Optimistic delete: the rows leave the table at once and are put back if the server refuses.
  protected runDelete(): void {
    const request = this.pending();
    if (!request) return;

    this.deleting.set(true);
    const rollback = this.store.removeOptimistically(request.ids);

    const finish = () => {
      this.deleting.set(false);
      this.pending.set(null);
      this.store.clearChecked();
    };

    if (request.ids.length === 1) {
      const [id] = request.ids;
      this.api.delete(id).subscribe({
        next: () => {
          finish();
          this.notifications.success('Project deleted.');
          this.clearSelectionIfDeleted(request.ids);
          this.store.reload();
        },
        error: (error: unknown) => {
          rollback();
          finish();
          this.notifications.error(toApiFailure(error).message, () =>
            this.api.delete(id).subscribe(() => this.store.reload()),
          );
        },
      });
      return;
    }

    this.api.bulkDelete(request.ids).subscribe({
      next: (result) => {
        finish();

        if (result.failed.length > 0) {
          // Put back only what survived, then say exactly which ones and why.
          rollback();
          const reasons = result.failed.map((f) => `#${f.projectId}: ${f.reason}`).join('; ');
          this.notifications.error(
            `${result.deleted.length} deleted, ${result.failed.length} failed — ${reasons}`,
          );
        } else {
          this.notifications.success(`${result.deleted.length} projects deleted.`);
        }

        this.clearSelectionIfDeleted(result.deleted);
        this.store.reload();
      },
      error: (error: unknown) => {
        rollback();
        finish();
        this.notifications.error(toApiFailure(error).message);
      },
    });
  }

  private clearSelectionIfDeleted(ids: readonly number[]): void {
    const selected = this.mapBridge.selectedProjectId();
    if (selected !== null && ids.includes(selected)) {
      this.mapBridge.selectFromList(null);
    }
  }

  // ----- export --------------------------------------------------------------

  // The CSV covers the whole filtered, sorted set rather than the page on screen.
  protected exportCsv(): void {
    window.location.href = this.api.exportUrl(this.store.query());
  }
}
