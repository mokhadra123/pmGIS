using PMGIS.Domain.Enums;

namespace PMGIS.Domain.Entities;

public class Activity
{
  public int Id { get; set; }

  public int ProjectId { get; set; }
  public Project? Project { get; set; }

  public required string Name { get; set; }

  public DateOnly StartDate { get; set; }
  public DateOnly EndDate { get; set; }

  public ActivityStatus Status { get; set; } = ActivityStatus.Planned;

  public int AssignedToUserId { get; set; }
  public User? AssignedTo { get; set; }

  public int PercentComplete { get; set; }

  public bool IsDeleted { get; set; }
  public int? DeletedByUserId { get; set; }
  public User? DeletedBy { get; set; }
  public DateTimeOffset? DeletedOn { get; set; }

  public DateTimeOffset CreatedOn { get; set; }
  public DateTimeOffset LastModifiedOn { get; set; }

  public int DurationDays => EndDate < StartDate ? 1 : EndDate.DayNumber - StartDate.DayNumber + 1;
}