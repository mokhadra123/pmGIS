import { DestroyRef, Injectable, computed, inject, signal } from '@angular/core';
import {
  EMPTY,
  Subject,
  catchError,
  debounceTime,
  distinctUntilChanged,
  startWith,
  switchMap,
} from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

import { ProjectsApi } from '@core/api/projects-api';
import { toApiFailure, type ApiFailure } from '@core/api/problem-details';
import {
  EMPTY_QUERY,
  hasActiveFilters,
  type ProjectQuery,
  type ProjectSortKey,
  type SortDirection,
} from '@core/api/project-query';
import { APP_CONFIG } from '@core/config/app-config';
import type { ProjectListItem } from '@core/models/project';
import { PagedResult } from '@core/models/paging';

// The Projects List state: one query, one page of rows, and the selection.
@Injectable({ providedIn: 'root' })
export class ProjectsStore {
  private readonly api = inject(ProjectsApi);
  private readonly destroyRef = inject(DestroyRef);

  // ----- query ---------------------------------------------------------------

  private readonly _query = signal<ProjectQuery>(EMPTY_QUERY);
  readonly query = this._query.asReadonly();

  readonly hasFilters = computed(() => hasActiveFilters(this._query()));

  // ----- results -------------------------------------------------------------

  private readonly _rows = signal<readonly ProjectListItem[]>([]);
  private readonly _totalCount = signal(0);
  private readonly _totalPages = signal(0);
  private readonly _loading = signal(false);
  private readonly _failure = signal<ApiFailure | null>(null);

  readonly rows = this._rows.asReadonly();
  readonly totalCount = this._totalCount.asReadonly();
  readonly totalPages = this._totalPages.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly failure = this._failure.asReadonly();

  readonly isEmpty = computed(
    () => !this._loading() && this._failure() === null && this._rows().length === 0,
  );

  // "Showing 1 to 25 of 5,000 projects" — the range currently on screen.
  readonly range = computed(() => {
    const { page, pageSize } = this._query();
    const total = this._totalCount();
    if (total === 0) return { from: 0, to: 0, total };
    const from = (page - 1) * pageSize + 1;
    return { from, to: Math.min(from + pageSize - 1, total), total };
  });

  // ----- bulk selection ------------------------------------------------------

  private readonly _checked = signal<ReadonlySet<number>>(new Set());
  readonly checked = this._checked.asReadonly();
  readonly checkedCount = computed(() => this._checked().size);

  readonly allOnPageChecked = computed(() => {
    const rows = this._rows();
    const checked = this._checked();
    return rows.length > 0 && rows.every((r) => checked.has(r.id));
  });

  // ----- the request pipeline ------------------------------------------------

  // Every query change funnels through here.
  private readonly reload$ = new Subject<void>();

  constructor() {
    this.reload$
      .pipe(
        startWith(undefined),
        switchMap(() => {
          this._loading.set(true);
          this._failure.set(null);
          // The failure is caught inside the inner observable so the outer stream never
          // errors — otherwise the first failed request would tear the pipeline down and
          // Retry would have nothing left to push into.
          return this.api.page(this._query()).pipe(
            catchError((error: unknown) => {
              this._failure.set(toApiFailure(error));
              this._loading.set(false);
              return EMPTY;
            }),
          );
        }),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((result) => this.applyResult(result));

    // The search box writes here; the debounce lives on the stream rather than in the
    // component so every caller gets it.
    this.searchInput$
      .pipe(
        debounceTime(APP_CONFIG.list.searchDebounceMS),
        distinctUntilChanged(),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe((search) => this.patch({ search, page: 1 }));
  }

  private applyResult(result: PagedResult<ProjectListItem>): void {
    this._rows.set(result.items);
    this._totalCount.set(result.totalCount);
    this._totalPages.set(result.totalPages);
    this._loading.set(false);

    // Drop checked ids that are no longer on screen: a bulk action must never act on a
    // row the user can no longer see.
    const visible = new Set(result.items.map((r) => r.id));
    const retained = new Set([...this._checked()].filter((id) => visible.has(id)));
    if (retained.size !== this._checked().size) {
      this._checked.set(retained);
    }
  }

  // ----- mutations -----------------------------------------------------------

  private readonly searchInput$ = new Subject<string>();

  // Called on every keystroke.
  search(term: string): void {
    this.searchInput$.next(term);
  }

  patch(changes: Partial<ProjectQuery>): void {
    this._query.update((q) => ({ ...q, ...changes }));
    this.reload();
  }

  // Toggles a column: same column flips direction, a new column starts ascending.
  sortBy(column: ProjectSortKey): void {
    const { sort, dir } = this._query();
    const next: SortDirection = sort === column && dir === 'asc' ? 'desc' : 'asc';
    this.patch({ sort: column, dir: sort === column ? next : 'asc', page: 1 });
  }

  goToPage(page: number): void {
    const clamped = Math.min(Math.max(1, page), Math.max(1, this._totalPages()));
    if (clamped !== this._query().page) {
      this.patch({ page: clamped });
    }
  }

  clearFilters(): void {
    const { pageSize, sort, dir } = this._query();
    this._query.set({ ...EMPTY_QUERY, pageSize, sort, dir });
    this.reload();
  }

  reload(): void {
    this.reload$.next();
  }

  // ----- selection -----------------------------------------------------------

  toggleChecked(id: number): void {
    this._checked.update((current) => {
      const next = new Set(current);
      next.has(id) ? next.delete(id) : next.add(id);
      return next;
    });
  }

  toggleAllOnPage(): void {
    const rows = this._rows();
    this._checked.update((current) => {
      const next = new Set(current);
      const allChecked = rows.every((r) => next.has(r.id));
      for (const row of rows) {
        allChecked ? next.delete(row.id) : next.add(row.id);
      }
      return next;
    });
  }

  clearChecked(): void {
    this._checked.set(new Set());
  }

  // ----- optimistic updates --------------------------------------------------

  // Removes rows at once and returns a function that puts them back if the server refuses.
  removeOptimistically(ids: readonly number[]): () => void {
    const doomed = new Set(ids);
    const before = this._rows();
    const beforeTotal = this._totalCount();

    const removed = before.filter((r) => doomed.has(r.id));
    if (removed.length === 0) {
      return () => undefined;
    }

    this._rows.set(before.filter((r) => !doomed.has(r.id)));
    this._totalCount.set(Math.max(0, beforeTotal - removed.length));

    return () => {
      this._rows.set(before);
      this._totalCount.set(beforeTotal);
    };
  }

  // Applies an edited row in place so the grid reflects a save before the reload lands.
  patchRowOptimistically(id: number, changes: Partial<ProjectListItem>): () => void {
    const before = this._rows();
    this._rows.set(before.map((r) => (r.id === id ? { ...r, ...changes } : r)));
    return () => this._rows.set(before);
  }
}
