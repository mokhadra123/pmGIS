using PMGIS.Domain.Enums;

namespace PMGIS.Infrastructure.Projects;

// Every input the Projects List can vary, in one object.
public sealed record ProjectQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 25;

    // One of ProjectSort.
    public string Sort { get; init; } = ProjectSort.LastModifiedOn;
    public bool Descending { get; init; } = true;

    // Free-text, matched against Project Name and Project Code.
    public string? Search { get; init; }

    public IReadOnlyList<int> ProjectTypeIds { get; init; } = [];
    public IReadOnlyList<ProjectStatus> Statuses { get; init; } = [];

    // Inclusive range.
    public DateOnly? DateFrom { get; init; }
    public DateOnly? DateTo { get; init; }

    // Map extent for the "only projects in current map extent" toggle.
    public double? MinLongitude { get; init; }
    public double? MinLatitude { get; init; }
    public double? MaxLongitude { get; init; }
    public double? MaxLatitude { get; init; }

    // Polygon drawn with the Sketch widget, as WKT in WGS84.
    public string? PolygonWkt { get; init; }

    public bool HasExtent =>
        MinLongitude.HasValue && MinLatitude.HasValue &&
        MaxLongitude.HasValue && MaxLatitude.HasValue;
}

// Allow-list of sortable columns.
public static class ProjectSort
{
    public const string Name = "name";
    public const string ProjectCode = "projectCode";
    public const string ProjectType = "projectTypeName";
    public const string Status = "status";
    public const string StartDate = "startDate";
    public const string EndDate = "endDate";
    public const string ActivityCount = "activityCount";
    public const string DurationDays = "durationDays";
    public const string LastModifiedOn = "lastModifiedOn";
    public const string LastModifiedBy = "lastModifiedByName";

    public static readonly string[] All =
    [
        Name, ProjectCode, ProjectType, Status, StartDate, EndDate,
        ActivityCount, DurationDays, LastModifiedOn, LastModifiedBy,
    ];

    public static bool IsValid(string? sort) =>
        sort is not null && All.Contains(sort, StringComparer.OrdinalIgnoreCase);

    // Maps user input onto the exact constant, case-insensitively.
    public static string Normalise(string? sort) =>
        All.FirstOrDefault(s => string.Equals(s, sort, StringComparison.OrdinalIgnoreCase))
        ?? LastModifiedOn;
}
