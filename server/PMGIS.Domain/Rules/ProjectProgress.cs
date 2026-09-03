using PMGIS.Domain.Entities;

namespace PMGIS.Domain.Rules;

public static class ProjectProgress
{
  public static double Calculate(IEnumerable<Activity> activities)
  {
    ArgumentNullException.ThrowIfNull(activities);

    var nonDeletedActivities = activities.Where(a => !a.IsDeleted).ToList();
    if(nonDeletedActivities.Count == 0) return 0;

    var totalDurationDays = nonDeletedActivities.Sum(a => (long)a.DurationDays);

    if (totalDurationDays <= 0) return nonDeletedActivities.Average(a => a.PercentComplete);

    var percentComplete = nonDeletedActivities.Sum(a => (double)a.PercentComplete * a.DurationDays);

    return Math.Round(percentComplete / totalDurationDays, 2);
  }
}