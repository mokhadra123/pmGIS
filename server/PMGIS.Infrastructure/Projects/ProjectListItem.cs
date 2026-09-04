using PMGIS.Domain.Enums;

namespace PMGIS.Infrastructure.Projects;

// One row of the Projects List.
public sealed record ProjectListItem
{
    public required int Id { get; init; }
    public required string ProjectCode { get; init; }
    public required string Name { get; init; }
    public string? ProjectTypeName { get; init; }
    public required ProjectStatus Status { get; init; }
    public DateOnly? StartDate { get; init; }
    public DateOnly? EndDate { get; init; }
    public int ActivityCount { get; init; }
    public int? DurationDays { get; init; }
    public string? LastModifiedByName { get; init; }
    public DateTimeOffset LastModifiedOn { get; init; }
    public long? ObjectId { get; init; }
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }

    // Drives the per-record enablement of "Zoom to Project".
    public bool HasLocation => ObjectId.HasValue;
}

public sealed record PagedResult<T>
{
    public required IReadOnlyList<T> Items { get; init; }
    public required int TotalCount { get; init; }
    public required int Page { get; init; }
    public required int PageSize { get; init; }
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);
}
