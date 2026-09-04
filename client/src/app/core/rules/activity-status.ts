import { ActivityStatus } from '@core/models/project';

export function canTransition(from: ActivityStatus, to: ActivityStatus): boolean {
  if (from === to) {
    return true;
  }

  const allowed: Record<ActivityStatus, readonly ActivityStatus[]> = {
    [ActivityStatus.Planned]: [ActivityStatus.InProgress, ActivityStatus.OnHold],
    [ActivityStatus.InProgress]: [ActivityStatus.Completed, ActivityStatus.OnHold],
    [ActivityStatus.OnHold]: [ActivityStatus.Planned, ActivityStatus.InProgress],
    [ActivityStatus.Completed]: [],
  };

  return allowed[from].includes(to);
}

export function isPercentCompleteEditable(status: ActivityStatus): boolean {
  return status === ActivityStatus.InProgress;
}

export function normalizePercentComplete(status: ActivityStatus, requested: number): number {
  if (status === ActivityStatus.Planned) return 0;
  if (status === ActivityStatus.Completed) return 100;
  return Math.min(100, Math.max(0, Math.round(requested || 0)));
}
