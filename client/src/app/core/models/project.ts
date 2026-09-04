export enum ProjectStatus {
  Draft = 0,
  Active = 1,
  InProgress = 2,
  OnHold = 3,
  Completed = 4,
}

export enum ActivityStatus {
  Planned = 0,
  InProgress = 1,
  Completed = 2,
  OnHold = 3,
}

export type IsoDate = string;

export interface ProjectListItem {
  readonly id: number;
  readonly projectCode: string;
  readonly name: string;
  readonly projectTypeName: string | null;
  readonly status: ProjectStatus;
  readonly startDate: IsoDate | null;
  readonly endDate: IsoDate | null;
  readonly activityCount: number;
  readonly durationDays: number | null;
  readonly lastModifiedByName: string | null;
  readonly lastModifiedOn: string;
  readonly objectId: number | null;
  readonly latitude: number | null;
  readonly longitude: number | null;
  // Drives per-record enablement of "Zoom to Project".
  readonly hasLocation: boolean;
}

export interface ActivityDetail {
  readonly id: number;
  readonly name: string;
  readonly startDate: IsoDate;
  readonly endDate: IsoDate;
  readonly status: ActivityStatus;
  readonly assignedToUserId: number;
  readonly assignedToName: string | null;
  readonly percentComplete: number;
  readonly durationDays: number;
}

export interface ProjectDetail {
  readonly id: number;
  readonly projectCode: string;
  readonly name: string;
  readonly description: string | null;
  readonly projectTypeId: number | null;
  readonly projectTypeName: string | null;
  readonly status: ProjectStatus;
  readonly startDate: IsoDate | null;
  readonly endDate: IsoDate | null;
  readonly budget: number | null;
  readonly ownerUserId: number | null;
  readonly ownerName: string | null;
  readonly objectId: number | null;
  readonly latitude: number | null;
  readonly longitude: number | null;
  readonly durationDays: number | null;
  // Average % complete weighted by activity duration. Server-calculated, read-only.
  readonly progress: number;
  readonly lastModifiedByName: string | null;
  readonly lastModifiedOn: string;
  readonly activities: readonly ActivityDetail[];
}

export interface SaveActivityPayload {
  id: number | null;
  name: string;
  startDate: IsoDate;
  endDate: IsoDate;
  status: ActivityStatus;
  assignedToUserId: number;
  percentComplete: number;
}

export interface SaveProjectPayload {
  projectCode: string;
  name: string;
  description: string | null;
  projectTypeId: number | null;
  status: ProjectStatus;
  startDate: IsoDate | null;
  endDate: IsoDate | null;
  budget: number | null;
  ownerUserId: number | null;
  latitude: number | null;
  longitude: number | null;
  activities: readonly SaveActivityPayload[];
}

export interface SaveProjectResult {
  readonly id: number;
  readonly projectCode: string;
  readonly objectId: number | null;
}

export interface CodeAvailability {
  readonly projectCode: string;
  readonly isAvailable: boolean;
  readonly isWellFormed: boolean;
}

export interface BulkDeleteFailure {
  readonly projectId: number;
  readonly reason: string;
}

export interface BulkDeleteResult {
  readonly deleted: readonly number[];
  readonly failed: readonly BulkDeleteFailure[];
}

export interface NearbyProject {
  readonly id: number;
  readonly projectCode: string;
  readonly name: string;
  readonly latitude: number | null;
  readonly longitude: number | null;
  readonly distanceKm: number;
}
