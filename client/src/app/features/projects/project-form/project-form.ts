import { DatePipe } from '@angular/common';
import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  computed,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed, toSignal } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  FormArray,
  FormBuilder,
  FormGroup,
  ReactiveFormsModule,
  Validators,
  type AsyncValidatorFn,
  type ValidationErrors,
} from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { catchError, map, of, switchMap, timer, type Observable } from 'rxjs';

import { LookupsApi } from '@core/api/lookups-api';
import { ProjectsApi } from '@core/api/projects-api';
import { toApiFailure, toControlPath } from '@core/api/problem-details';
import { APP_CONFIG } from '@core/config/app-config';
import {
  ActivityStatus,
  ProjectStatus,
  type ProjectDetail,
  type SaveActivityPayload,
  type SaveProjectPayload,
} from '@core/models/project';
import { MapBridge } from '@core/state/map-bridge';
import { ProjectsStore } from '@core/state/projects-store';
import { Notifications } from '@shared/notifications/notifications';

import { ActivitiesEditor } from './activities-editor';
import { LocationPicker } from './location-picker';
import {
  activitiesWithinProjectRange,
  activityErrorMessages,
  activityRow,
  endAfterStart,
  projectLocation,
} from './project-validators';
import { PROJECT_CODE_PATTERN } from '@core/rules/project-code';

interface DraftEnvelope {
  readonly projectId: number | null;
  readonly savedAt: string;
  readonly value: unknown;
}

// Add / Edit Project.
@Component({
  selector: 'app-project-form',
  standalone: true,
  imports: [ReactiveFormsModule, DatePipe, ActivitiesEditor, LocationPicker],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './project-form.html',
  styleUrl: './project-form.scss',
})
export class ProjectForm {
  private readonly fb = inject(FormBuilder);
  private readonly api = inject(ProjectsApi);
  private readonly lookups = inject(LookupsApi);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly notifications = inject(Notifications);
  private readonly store = inject(ProjectsStore);
  private readonly destroyRef = inject(DestroyRef);
  protected readonly bridge = inject(MapBridge);

  protected readonly descriptionMax = APP_CONFIG.form.descriptionMaxLength;

  protected readonly projectId = signal<number | null>(null);
  protected readonly isEdit = computed(() => this.projectId() !== null);
  protected readonly loading = signal(false);
  protected readonly saving = signal(false);
  protected readonly loadFailure = signal<string | null>(null);
  protected readonly draftRestoredAt = signal<string | null>(null);

  protected readonly projectTypes = toSignal(
    this.lookups.projectTypes().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );
  protected readonly projectStatuses = toSignal(
    this.lookups.projectStatuses().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );
  protected readonly activityStatuses = toSignal(
    this.lookups.activityStatuses().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );
  protected readonly users = toSignal(this.lookups.users().pipe(catchError(() => of([]))), {
    initialValue: [],
  });

  protected readonly form: FormGroup = this.fb.group(
    {
      projectCode: this.fb.control('', {
        validators: [Validators.required, Validators.pattern(PROJECT_CODE_PATTERN)],
        asyncValidators: [this.uniqueProjectCode()],
        // The brief asks for the uniqueness check when the field loses focus, not on
        // every keystroke — this is what stops a request per character.
        updateOn: 'blur',
      }),
      name: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', [Validators.maxLength(APP_CONFIG.form.descriptionMaxLength)]],
      projectTypeId: [null as number | null],
      status: [ProjectStatus.Draft, Validators.required],
      startDate: [null as string | null],
      endDate: [null as string | null],
      budget: [null as number | null, [Validators.min(0)]],
      ownerUserId: [null as number | null],
      latitude: [null as number | null],
      longitude: [null as number | null],
      activities: this.fb.array([]),
    },
    { validators: [endAfterStart, activitiesWithinProjectRange, projectLocation] },
  );

  protected get activities(): FormArray {
    return this.form.get('activities') as FormArray;
  }

  // Bound into the child so a new row always gets this component's validators.
  protected readonly addActivityRow = (): void => {
    this.activities.push(this.buildActivityRow());
    this.form.updateValueAndValidity();
  };

  protected readonly descriptionLength = computed(() => this.descriptionValue().length);
  private readonly descriptionValue = toSignal(
    this.form.get('description')!.valueChanges.pipe(map((v) => (v as string) ?? '')),
    { initialValue: '' },
  );

  // Every offending activity row, listed — not just the first.
  protected readonly activitySummary = computed(() => {
    this.formRevision();
    return activityErrorMessages(this.activities, (status) => this.activityStatusName(status));
  });

  // Bumped on every value change so the computed summary above re-runs.
  private readonly formRevision = toSignal(this.form.valueChanges.pipe(map((_, index) => index)), {
    initialValue: 0,
  });

  protected readonly canSubmit = computed(() => {
    this.formRevision();
    return !this.saving() && !this.loading();
  });

  constructor() {
    // Route drives the mode. `new` has no id; `:id/edit` loads the project.
    this.route.paramMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe((params) => {
      const raw = params.get('id');
      const id = raw ? Number(raw) : null;
      this.projectId.set(id);
      id === null ? this.startBlank() : this.load(id);
    });

    // Auto-save a draft every 30 seconds so an accidental refresh does not lose the entry.
    timer(APP_CONFIG.form.autosaveIntervalMS, APP_CONFIG.form.autosaveIntervalMS)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe(() => this.saveDraft());

    // The form owns the coordinate; the picker and map mirror it through the bridge.
    this.destroyRef.onDestroy(() => this.bridge.clearDraftLocation());
  }

  // ----- loading -------------------------------------------------------------

  private startBlank(): void {
    this.form.reset({
      projectCode: '',
      name: '',
      description: '',
      projectTypeId: null,
      status: ProjectStatus.Draft,
      startDate: null,
      endDate: null,
      budget: null,
      ownerUserId: null,
      latitude: null,
      longitude: null,
    });
    this.activities.clear();
    this.bridge.clearDraftLocation();
    this.restoreDraft(null);
  }

  private load(id: number): void {
    this.loading.set(true);
    this.loadFailure.set(null);

    this.api
      .get(id)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (project) => {
          this.apply(project);
          this.loading.set(false);
          this.restoreDraft(id);
        },
        error: (error: unknown) => {
          this.loadFailure.set(toApiFailure(error).message);
          this.loading.set(false);
        },
      });
  }

  private apply(project: ProjectDetail): void {
    this.form.patchValue({
      projectCode: project.projectCode,
      name: project.name,
      description: project.description ?? '',
      projectTypeId: project.projectTypeId,
      status: project.status,
      startDate: project.startDate,
      endDate: project.endDate,
      budget: project.budget,
      ownerUserId: project.ownerUserId,
      latitude: project.latitude,
      longitude: project.longitude,
    });

    this.activities.clear();
    for (const activity of project.activities) {
      this.activities.push(
        this.buildActivityRow({
          id: activity.id,
          originalStatus: activity.status,
          name: activity.name,
          startDate: activity.startDate,
          endDate: activity.endDate,
          status: activity.status,
          assignedToUserId: activity.assignedToUserId,
          percentComplete: activity.percentComplete,
        }),
      );
    }

    this.bridge.draftLocation.set(
      project.latitude !== null && project.longitude !== null
        ? { latitude: project.latitude, longitude: project.longitude }
        : null,
    );

    this.form.markAsPristine();
    this.form.updateValueAndValidity();
  }

  private buildActivityRow(value?: {
    id: number | null;
    originalStatus: ActivityStatus | null;
    name: string;
    startDate: string;
    endDate: string;
    status: ActivityStatus;
    assignedToUserId: number | null;
    percentComplete: number;
  }): FormGroup {
    return this.fb.group(
      {
        id: [value?.id ?? null],
        // Not sent to the server. Held so the status state machine can be enforced
        // against where the row actually is, not against where it started this edit.
        originalStatus: [value?.originalStatus ?? null],
        name: [value?.name ?? '', [Validators.required, Validators.maxLength(200)]],
        startDate: [value?.startDate ?? '', Validators.required],
        endDate: [value?.endDate ?? '', Validators.required],
        status: [value?.status ?? ActivityStatus.Planned, Validators.required],
        assignedToUserId: [
          value?.assignedToUserId ?? null,
          [Validators.required, Validators.min(1)],
        ],
        percentComplete: [value?.percentComplete ?? 0],
      },
      { validators: [activityRow] },
    );
  }

  // ----- async uniqueness ----------------------------------------------------

  // Asked of the server when the field loses focus.
  private uniqueProjectCode(): AsyncValidatorFn {
    return (control: AbstractControl): Observable<ValidationErrors | null> => {
      const code = (control.value as string | null)?.trim();

      if (!code || !PROJECT_CODE_PATTERN.test(code)) {
        return of(null);
      }

      return this.api.checkCode(code, this.projectId()).pipe(
        map((result) => (result.isAvailable ? null : { codeTaken: true })),
        // A failed availability check must not block the form: the server rejects a
        // duplicate on submit regardless.
        catchError(() => of(null)),
      );
    };
  }

  // ----- draft persistence ---------------------------------------------------

  private draftKey(projectId: number | null): string {
    return `${APP_CONFIG.form.draftStorageKey}.${projectId ?? 'new'}`;
  }

  private saveDraft(): void {
    if (!this.form.dirty || this.saving()) {
      return;
    }

    const envelope: DraftEnvelope = {
      projectId: this.projectId(),
      savedAt: new Date().toISOString(),
      value: this.form.getRawValue(),
    };

    try {
      localStorage.setItem(this.draftKey(this.projectId()), JSON.stringify(envelope));
    } catch {
      // Storage full or blocked (private browsing). A missing draft is not worth
      // interrupting the user over.
    }
  }

  private restoreDraft(projectId: number | null): void {
    let envelope: DraftEnvelope | null = null;

    try {
      const raw = localStorage.getItem(this.draftKey(projectId));
      envelope = raw ? (JSON.parse(raw) as DraftEnvelope) : null;
    } catch {
      envelope = null;
    }

    if (!envelope) {
      this.draftRestoredAt.set(null);
      return;
    }

    const value = envelope.value as Record<string, unknown>;
    const rows = (value['activities'] as unknown[] | undefined) ?? [];

    this.activities.clear();
    for (const row of rows) {
      const r = row as Record<string, unknown>;
      this.activities.push(
        this.buildActivityRow({
          id: (r['id'] as number | null) ?? null,
          originalStatus: (r['originalStatus'] as ActivityStatus | null) ?? null,
          name: (r['name'] as string) ?? '',
          startDate: (r['startDate'] as string) ?? '',
          endDate: (r['endDate'] as string) ?? '',
          status: (r['status'] as ActivityStatus) ?? ActivityStatus.Planned,
          assignedToUserId: (r['assignedToUserId'] as number | null) ?? null,
          percentComplete: (r['percentComplete'] as number) ?? 0,
        }),
      );
    }

    this.form.patchValue(value);
    this.form.markAsDirty();
    this.draftRestoredAt.set(envelope.savedAt);

    const lat = value['latitude'] as number | null;
    const lon = value['longitude'] as number | null;
    if (lat !== null && lat !== undefined && lon !== null && lon !== undefined) {
      this.bridge.draftLocation.set({ latitude: lat, longitude: lon });
    }
  }

  protected discardDraft(): void {
    try {
      localStorage.removeItem(this.draftKey(this.projectId()));
    } catch {
      /* nothing to do */
    }
    this.draftRestoredAt.set(null);
    const id = this.projectId();
    id === null ? this.startBlank() : this.load(id);
  }

  private clearDraft(): void {
    try {
      localStorage.removeItem(this.draftKey(this.projectId()));
    } catch {
      /* nothing to do */
    }
    this.draftRestoredAt.set(null);
  }

  // Consulted by the route guard before navigating away.
  hasUnsavedChanges(): boolean {
    return this.form.dirty && !this.saving();
  }

  // ----- save ----------------------------------------------------------------

  protected submit(): void {
    if (this.saving()) {
      return; // Double submission is blocked here as well as by the disabled button.
    }

    this.form.markAllAsTouched();
    this.activities.controls.forEach((row) => row.markAllAsTouched());
    this.form.updateValueAndValidity();

    if (this.form.invalid) {
      this.notifications.error('Fix the highlighted fields before saving.');
      return;
    }

    const payload = this.toPayload();
    this.saving.set(true);

    const id = this.projectId();
    const request = id === null ? this.api.create(payload) : this.api.update(id, payload);

    request.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (result) => {
        this.saving.set(false);
        this.form.markAsPristine();
        this.clearDraft();
        this.bridge.clearDraftLocation();
        this.store.reload();
        this.notifications.success(
          id === null ? `Project ${result.projectCode} created.` : 'Project saved.',
        );
        void this.router.navigate(['/projects', result.id]);
      },
      error: (error: unknown) => {
        this.saving.set(false);
        this.applyServerErrors(error);
      },
    });
  }

  private toPayload(): SaveProjectPayload {
    const raw = this.form.getRawValue() as Record<string, unknown>;

    const activities: SaveActivityPayload[] = (raw['activities'] as Record<string, unknown>[]).map(
      (row) => ({
        id: (row['id'] as number | null) ?? null,
        name: row['name'] as string,
        startDate: row['startDate'] as string,
        endDate: row['endDate'] as string,
        status: row['status'] as ActivityStatus,
        assignedToUserId: row['assignedToUserId'] as number,
        percentComplete: Number(row['percentComplete'] ?? 0),
        // originalStatus is deliberately not sent: it is a client-side concern.
      }),
    );

    const description = ((raw['description'] as string) ?? '').trim();

    return {
      projectCode: (raw['projectCode'] as string).trim(),
      name: (raw['name'] as string).trim(),
      description: description === '' ? null : description,
      projectTypeId: (raw['projectTypeId'] as number | null) ?? null,
      status: raw['status'] as ProjectStatus,
      startDate: (raw['startDate'] as string | null) || null,
      endDate: (raw['endDate'] as string | null) || null,
      budget: raw['budget'] === null || raw['budget'] === '' ? null : Number(raw['budget']),
      ownerUserId: (raw['ownerUserId'] as number | null) ?? null,
      latitude: (raw['latitude'] as number | null) ?? null,
      longitude: (raw['longitude'] as number | null) ?? null,
      activities,
    };
  }

  // Puts server rejections back on the fields that caused them, not only in a toast.
  private applyServerErrors(error: unknown): void {
    const failure = toApiFailure(error);
    let placed = 0;

    for (const [field, messages] of Object.entries(failure.fieldErrors)) {
      const control = this.form.get(toControlPath(field));
      if (control) {
        control.setErrors({ ...(control.errors ?? {}), server: messages.join(' ') });
        control.markAsTouched();
        placed++;
      }
    }

    this.notifications.error(
      placed > 0 && failure.status === 400
        ? 'The server rejected some values. See the highlighted fields.'
        : failure.message,
    );
  }

  // ----- helpers used by the template ----------------------------------------

  protected activityStatusName(status: ActivityStatus): string {
    return this.activityStatuses().find((s) => s.id === status)?.name ?? String(status);
  }

  protected serverError(path: string): string | null {
    return (this.form.get(path)?.errors?.['server'] as string | undefined) ?? null;
  }

  protected showError(path: string, code: string): boolean {
    const control = this.form.get(path);
    return !!control && control.hasError(code) && (control.touched || control.dirty);
  }

  protected codePending(): boolean {
    return this.form.get('projectCode')?.pending ?? false;
  }

  protected cancel(): void {
    void this.router.navigate(['/']);
  }
}
