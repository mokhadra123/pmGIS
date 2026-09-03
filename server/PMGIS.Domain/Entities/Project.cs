using PMGIS.Domain.Enums;

namespace PMGIS.Domain.Entities;

public class Project
{
    public int Id { get; set; }
    public required string Name { get; set; }
    public required string ProjectCode { get; set; }
    public string? Description { get; set; }

    public int? ProjectTypeId { get; set; }
    public ProjectType? ProjectType { get; set; }

    public ProjectStatus Status { get; set; } = ProjectStatus.Draft;

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public decimal? Budget { get; set; }

    public int? OwnerUserId { get; set; }
    public User? Owner { get; set; }

    public long? ObjectId { get; set; }

    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public ICollection<Activity> Activities { get; set; } = [];

    public int CreatedByUserId { get; set; }
    public User? CreatedBy { get; set; }
    public DateTimeOffset CreatedOn { get; set; }

    public int LastModifiedByUserId { get; set; }
    public User? LastModifiedBy { get; set; }
    public DateTimeOffset LastModifiedOn { get; set; }

    public int? DurationDays { get; private set; }

    public bool HasLocation => ObjectId.HasValue;
}
