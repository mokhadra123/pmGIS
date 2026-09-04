import { CurrencyPipe, DatePipe, DecimalPipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, of } from 'rxjs';

import { LookupsApi } from '@core/api/lookups-api';
import { ProjectsApi } from '@core/api/projects-api';
import { toApiFailure } from '@core/api/problem-details';
import { APP_CONFIG } from '@core/config/app-config';
import type { ActivityStatus, ProjectDetail, ProjectStatus } from '@core/models/project';
import { MapBridge } from '@core/state/map-bridge';

// Read-only project view.
@Component({
  selector: 'app-project-details',
  standalone: true,
  imports: [DatePipe, DecimalPipe, CurrencyPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-details.html',
  styleUrl: './project-details.scss',
})
export class ProjectDetails {
  private readonly api = inject(ProjectsApi);
  private readonly lookups = inject(LookupsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly destroyRef = inject(DestroyRef);
  private readonly bridge = inject(MapBridge);

  protected readonly project = signal<ProjectDetail | null>(null);
  protected readonly loading = signal(false);
  protected readonly failure = signal<string | null>(null);

  private readonly projectStatuses = toSignal(
    this.lookups.projectStatuses().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );
  private readonly activityStatuses = toSignal(
    this.lookups.activityStatuses().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly progressPercent = computed(() => Math.round(this.project()?.progress ?? 0));

  constructor() {
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const id = Number(params.get('id'));
      if (Number.isFinite(id)) {
        this.load(id);
      }
    });
  }

  private load(id: number): void {
    this.loading.set(true);
    this.failure.set(null);

    this.api
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project) => {
          this.project.set(project);
          this.loading.set(false);
          // Opening a project selects it, which highlights its point on the map.
          this.bridge.selectFromList(project.id);
        },
        error: (error: unknown) => {
          this.failure.set(toApiFailure(error).message);
          this.loading.set(false);
        },
      });
  }

  protected statusName(status: ProjectStatus): string {
    return this.projectStatuses().find((s) => s.id === status)?.name ?? String(status);
  }

  protected activityStatusName(status: ActivityStatus): string {
    return this.activityStatuses().find((s) => s.id === status)?.name ?? String(status);
  }

  protected zoomTo(): void {
    const project = this.project();
    if (project?.latitude == null || project.longitude == null) {
      return;
    }
    this.bridge.goTo({
      latitude: project.latitude,
      longitude: project.longitude,
      zoom: APP_CONFIG.map.projectZoom,
    });
  }

  protected edit(): void {
    const project = this.project();
    if (project) {
      void this.router.navigate(['/projects', project.id, 'edit']);
    }
  }

  protected close(): void {
    void this.router.navigate(['/']);
  }
}
