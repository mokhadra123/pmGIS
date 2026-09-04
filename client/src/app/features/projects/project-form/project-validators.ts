import type {
  AbstractControl,
  FormArray,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';

import { APP_CONFIG } from '@core/config/app-config';
import { ActivityStatus } from '@core/models/project';
import { canTransition, isPercentCompleteEditable } from '@core/rules/activity-status';

// Client-side twins of the server's FluentValidation rules.

// End Date must be later than Start Date.
export const endAfterStart: ValidatorFn = (group: AbstractControl): ValidationErrors | null => {
  const start = group.get('startDate')?.value as string | null;
  const end = group.get('endDate')?.value as string | null;

  if (!start || !end) {
    return null;
  }

  return end > start ? null : { endAfterStart: true };
};

// One activity row.
export const activityRow: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const group = control as FormGroup;
  const errors: ValidationErrors = {};

  const start = group.get('startDate')?.value as string | null;
  const end = group.get('endDate')?.value as string | null;
  const status = group.get('status')?.value as ActivityStatus;
  const percent = Number(group.get('percentComplete')?.value ?? 0);
  const original = group.get('originalStatus')?.value as ActivityStatus | null;

  if (start && end && end < start) {
    errors['dateOrder'] = true;
  }

  // Planned -> In Progress -> Completed, On Hold reachable from either and resumable.
  // Only existing rows have an original status; a new row may start anywhere.
  if (original !== null && original !== undefined && !canTransition(original, status)) {
    errors['transition'] = { from: original, to: status };
  }

  if (status === ActivityStatus.Planned && percent !== 0) {
    errors['percentPlanned'] = true;
  }

  if (status === ActivityStatus.Completed && percent !== 100) {
    errors['percentCompleted'] = true;
  }

  if (percent < 0 || percent > 100) {
    errors['percentRange'] = true;
  }

  return Object.keys(errors).length > 0 ? errors : null;
};

// Activity dates must fall inside the parent project's range.
export const activitiesWithinProjectRange: ValidatorFn = (
  control: AbstractControl,
): ValidationErrors | null => {
  const group = control as FormGroup;
  const projectStart = group.get('startDate')?.value as string | null;
  const projectEnd = group.get('endDate')?.value as string | null;
  const activities = group.get('activities') as FormArray | null;

  if (!activities) {
    return null;
  }

  let offending = 0;

  for (const row of activities.controls) {
    const existing = { ...(row.errors ?? {}) };
    delete existing['outsideProject'];

    const start = row.get('startDate')?.value as string | null;
    const end = row.get('endDate')?.value as string | null;

    const outside =
      projectStart !== null &&
      projectEnd !== null &&
      start !== null &&
      end !== null &&
      (start < projectStart || end > projectEnd);

    if (outside) {
      offending++;
      row.setErrors({ ...existing, outsideProject: true });
    } else {
      row.setErrors(Object.keys(existing).length > 0 ? existing : null);
    }
  }

  return offending > 0 ? { activitiesOutsideProject: offending } : null;
};

// [minLon, minLat, maxLon, maxLat] — the configured allowed project boundary.
export function isInsideBoundary(latitude: number, longitude: number): boolean {
  const [minLon, minLat, maxLon, maxLat] = APP_CONFIG.boundary.extent;
  return longitude >= minLon && longitude <= maxLon && latitude >= minLat && latitude <= maxLat;
}

// The location pair.
export const projectLocation: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const group = control as FormGroup;
  const lat = group.get('latitude')?.value as number | null;
  const lon = group.get('longitude')?.value as number | null;

  const hasLat = lat !== null && lat !== undefined && !Number.isNaN(lat);
  const hasLon = lon !== null && lon !== undefined && !Number.isNaN(lon);

  if (!hasLat && !hasLon) {
    return null;
  }

  if (hasLat !== hasLon) {
    return { locationIncomplete: true };
  }

  if (lat! < -90 || lat! > 90 || lon! < -180 || lon! > 180) {
    return { locationRange: true };
  }

  if (!isInsideBoundary(lat!, lon!)) {
    return { locationOutsideBoundary: true };
  }

  return null;
};

// Human-readable summary lines, one per offending activity row.
export function activityErrorMessages(
  activities: FormArray,
  statusName: (status: ActivityStatus) => string,
): readonly string[] {
  const messages: string[] = [];

  activities.controls.forEach((row, index) => {
    const label = (row.get('name')?.value as string) || `Row ${index + 1}`;
    const errors = row.errors ?? {};

    if (row.get('name')?.hasError('required')) {
      messages.push(`Row ${index + 1}: Activity Name is required.`);
    }

    if (row.get('assignedToUserId')?.invalid) {
      messages.push(`"${label}": Assigned To is required.`);
    }

    if (errors['dateOrder']) {
      messages.push(`"${label}": End Date cannot be before its Start Date.`);
    }

    if (errors['outsideProject']) {
      messages.push(`"${label}": dates fall outside the project's Start / End range.`);
    }

    if (errors['transition']) {
      const { from, to } = errors['transition'] as { from: ActivityStatus; to: ActivityStatus };
      messages.push(`"${label}": cannot move from ${statusName(from)} to ${statusName(to)}.`);
    }

    if (errors['percentPlanned']) {
      messages.push(`"${label}": % Complete must be 0 while Planned.`);
    }

    if (errors['percentCompleted']) {
      messages.push(`"${label}": % Complete must be 100 when Completed.`);
    }

    if (errors['percentRange']) {
      messages.push(`"${label}": % Complete must be between 0 and 100.`);
    }
  });

  return messages;
}

export { isPercentCompleteEditable };
