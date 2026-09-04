import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NavigationEnd, Router, RouterOutlet } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { filter, map, startWith } from 'rxjs';

import { ProjectMap } from '../map/map-view/project-map';
import { ProjectList } from '../projects/project-list/project-list';

// Projects List, map, and a detail pane that appears only when a child route fills it.
@Component({
  selector: 'app-workspace',
  standalone: true,
  imports: [RouterOutlet, ProjectList, ProjectMap],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './workspace.html',
  styleUrl: './workspace.scss',
})
export class Workspace {
  private readonly router = inject(Router);

  // Drives the third column: absent on the bare list-and-map view.
  protected readonly panelOpen = signal(false);

  constructor() {
    this.router.events
      .pipe(
        filter((event): event is NavigationEnd => event instanceof NavigationEnd),
        map((event) => event.urlAfterRedirects.includes('/projects')),
        startWith(this.router.url.includes('/projects')),
        takeUntilDestroyed(),
      )
      .subscribe((open) => this.panelOpen.set(open));
  }
}
