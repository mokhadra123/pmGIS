using PMGIS.Domain.Enums;

namespace PMGIS.Domain.Rules;

public static class ActivityStatusTransitions
{
  // Determines whether transitioning from one activity status to another is allowed.
  public static bool CanTransition(ActivityStatus from, ActivityStatus to)
  {
    if (from == to) return true;

    return (from, to) switch
    {
      (ActivityStatus.Planned, ActivityStatus.InProgress) => true,
      (ActivityStatus.Planned, ActivityStatus.OnHold) => true,

      (ActivityStatus.InProgress, ActivityStatus.Completed) => true,
      (ActivityStatus.InProgress, ActivityStatus.OnHold) => true,

      (ActivityStatus.OnHold, ActivityStatus.Planned) => true,
      (ActivityStatus.OnHold, ActivityStatus.InProgress) => true,

      _ => false,
    };
  }

// Normalizes the requested completion percentage based on the activity status.
  public static int NormalizePercentComplete(ActivityStatus status, int requested)
  {
    return status switch
    {
      ActivityStatus.Planned => 0,
      ActivityStatus.Completed => 100,
      _ => Math.Clamp(requested, 0, 100),
    };
  }
}