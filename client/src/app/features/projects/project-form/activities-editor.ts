import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { FormArray, FormGroup, ReactiveFormsModule } from '@angular/forms';
import { toSignal } from '@angular/core/rxjs-interop';
import { catchError, of } from 'rxjs';

import { LookupsApi } from '@core/api/lookups-api';
import { ActivityStatus } from '@core/models/project';
import {
  canTransition,
  isPercentCompleteEditable,
  normalizePercentComplete,
} from '@core/rules/activity-status';

// The Activities section: rows are added, edited and removed inline, without leaving the form.
@Component({
  selector: 'app-activities-editor',
  standalone: true,
  imports: [ReactiveFormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './activities-editor.html',
  styleUrl: './activities-editor.scss',
})
export class ActivitiesEditor {
  readonly activities = input.required<FormArray>();
  // Emitted so the parent can build a new row with its own validators attached.
  readonly addRow = input.required<() => void>();

  private readonly lookups = inject(LookupsApi);

  protected readonly statuses = toSignal(
    this.lookups.activityStatuses().pipe(catchError(() => of([]))),
    { initialValue: [] },
  );

  protected readonly users = toSignal(this.lookups.users().pipe(catchError(() => of([]))), {
    initialValue: [],
  });

  // Index awaiting delete confirmation.
  protected readonly confirmingIndex = signal<number | null>(null);

  protected rows(): FormGroup[] {
    return this.activities().controls as FormGroup[];
  }

  protected add(): void {
    this.addRow()();
  }

  protected askRemove(index: number): void {
    this.confirmingIndex.set(index);
  }

  protected cancelRemove(): void {
    this.confirmingIndex.set(null);
  }

  // Removing a row here only drops it from the payload.
  protected remove(index: number): void {
    this.activities().removeAt(index);
    this.confirmingIndex.set(null);
    this.activities().updateValueAndValidity();
  }

  // Status drags % Complete to what it demands, mirroring the rule on the server.
  protected onStatusChange(row: FormGroup, raw: string): void {
    const status = Number(raw) as ActivityStatus;
    const percent = Number(row.get('percentComplete')?.value ?? 0);

    row.patchValue({
      status,
      percentComplete: normalizePercentComplete(status, percent),
    });
  }

  protected percentEditable(row: FormGroup): boolean {
    return isPercentCompleteEditable(row.get('status')?.value as ActivityStatus);
  }

  // Statuses the row may legally move to from where it is stored.
  protected allowedStatuses(row: FormGroup): readonly { id: number; name: string }[] {
    const all = this.statuses();
    const original = row.get('originalStatus')?.value as ActivityStatus | null;

    if (original === null || original === undefined) {
      return all;
    }

    return all.filter((s) => canTransition(original, s.id as ActivityStatus));
  }

  protected rowHasError(row: FormGroup): boolean {
    return row.invalid && (row.touched || row.dirty);
  }
}
