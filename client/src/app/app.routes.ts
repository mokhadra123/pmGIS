import { Routes } from '@angular/router';
import { unsavedChangesGuard } from '@core/guards/unsaved-changes';

export const routes: Routes = [
  {
    path: '',
    loadComponent: () =>
      import('./features/workspace/workspace').then((m) => m.Workspace),
    children: [
      {
        path: 'projects/new',
        loadComponent: () =>
          import('./features/projects/project-form/project-form').then((m) => m.ProjectForm),
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'projects/:id/edit',
        loadComponent: () =>
          import('./features/projects/project-form/project-form').then((m) => m.ProjectForm),
        canDeactivate: [unsavedChangesGuard],
      },
      {
        path: 'projects/:id',
        loadComponent: () =>
          import('./features/projects/project-details/project-details').then(
            (m) => m.ProjectDetails,
          ),
      },
    ],
  },
  { path: '**', redirectTo: '' },
];
