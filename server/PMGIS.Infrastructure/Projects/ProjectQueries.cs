using Microsoft.EntityFrameworkCore;

using PMGIS.Domain.Entities;
using PMGIS.Infrastructure.Data;

namespace PMGIS.Infrastructure.Projects;

// The single place project list logic lives.
public static class ProjectQueries
{
    // Applies every filter in query and returns a composable IQueryable.
    public static IQueryable<Project> Filter(PmgisDbContext db, ProjectQuery query)
    {
        // PostGIS evaluates the drawn polygon. Parameterised, so the WKT cannot be
        // injected. Composition below still happens in LINQ, so this stays one query.
        IQueryable<Project> source = string.IsNullOrWhiteSpace(query.PolygonWkt)
            ? db.Projects
            : db.Projects.FromSql(
                $"""
                 SELECT * FROM "Projects"
                 WHERE "Longitude" IS NOT NULL
                   AND "Latitude" IS NOT NULL
                   AND ST_Contains(
                         ST_GeomFromText({query.PolygonWkt}, 4326),
                         ST_SetSRID(ST_MakePoint("Longitude", "Latitude"), 4326))
                 """);

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = $"%{query.Search.Trim()}%";
            source = source.Where(p =>
                EF.Functions.ILike(p.Name, term) ||
                EF.Functions.ILike(p.ProjectCode, term));
        }

        if (query.ProjectTypeIds.Count > 0)
        {
            var ids = query.ProjectTypeIds.ToArray();
            source = source.Where(p => p.ProjectTypeId != null && ids.Contains(p.ProjectTypeId.Value));
        }

        if (query.Statuses.Count > 0)
        {
            var statuses = query.Statuses.ToArray();
            source = source.Where(p => statuses.Contains(p.Status));
        }

        // Overlap, not containment: a project running Jan-Dec matches a March filter.
        // A project is excluded only if it ends before the range or starts after it.
        if (query.DateFrom is { } from)
        {
            source = source.Where(p => p.EndDate == null || p.EndDate >= from);
        }

        if (query.DateTo is { } to)
        {
            source = source.Where(p => p.StartDate == null || p.StartDate <= to);
        }

        if (query.HasExtent)
        {
            source = source.Where(p =>
                p.Longitude != null && p.Latitude != null &&
                p.Longitude >= query.MinLongitude! && p.Longitude <= query.MaxLongitude! &&
                p.Latitude >= query.MinLatitude! && p.Latitude <= query.MaxLatitude!);
        }

        return source;
    }

    // Projects to the list shape.
    public static IQueryable<ProjectListItem> Project(IQueryable<Project> source) =>
        source.Select(p => new ProjectListItem
        {
            Id = p.Id,
            ProjectCode = p.ProjectCode,
            Name = p.Name,
            ProjectTypeName = p.ProjectType != null ? p.ProjectType.Name : null,
            Status = p.Status,
            StartDate = p.StartDate,
            EndDate = p.EndDate,
            ActivityCount = p.Activities.Count(a => !a.IsDeleted),
            DurationDays = p.DurationDays,
            LastModifiedByName = p.LastModifiedBy != null ? p.LastModifiedBy.Name : null,
            LastModifiedOn = p.LastModifiedOn,
            ObjectId = p.ObjectId,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
        });

    // Sorting happens in the database.
    public static IQueryable<ProjectListItem> Sort(IQueryable<ProjectListItem> source, ProjectQuery query)
    {
        var desc = query.Descending;

        var ordered = ProjectSort.Normalise(query.Sort) switch
        {
            ProjectSort.Name => Order(source, x => x.Name, desc),
            ProjectSort.ProjectCode => Order(source, x => x.ProjectCode, desc),
            ProjectSort.ProjectType => Order(source, x => x.ProjectTypeName, desc),
            ProjectSort.Status => Order(source, x => x.Status, desc),
            ProjectSort.StartDate => Order(source, x => x.StartDate, desc),
            ProjectSort.EndDate => Order(source, x => x.EndDate, desc),
            ProjectSort.ActivityCount => Order(source, x => x.ActivityCount, desc),
            ProjectSort.DurationDays => Order(source, x => x.DurationDays, desc),
            ProjectSort.LastModifiedBy => Order(source, x => x.LastModifiedByName, desc),
            _ => Order(source, x => x.LastModifiedOn, desc),
        };

        // Stable tie-break, otherwise paging can repeat or skip rows across pages.
        return ordered.ThenBy(x => x.Id);

        static IOrderedQueryable<ProjectListItem> Order<TKey>(
            IQueryable<ProjectListItem> src,
            System.Linq.Expressions.Expression<Func<ProjectListItem, TKey>> key,
            bool descending) =>
            descending ? src.OrderByDescending(key) : src.OrderBy(key);
    }

    // One page of results plus the total count, both computed server-side.
    public static async Task<PagedResult<ProjectListItem>> PageAsync(
        PmgisDbContext db, ProjectQuery query, CancellationToken ct = default)
    {
        var filtered = Filter(db, query);
        var total = await filtered.CountAsync(ct);

        var page = Math.Max(1, query.Page);
        var size = Math.Clamp(query.PageSize, 1, 200);

        var items = await Sort(Project(filtered), query)
            .Skip((page - 1) * size)
            .Take(size)
            .ToListAsync(ct);

        return new PagedResult<ProjectListItem>
        {
            Items = items,
            TotalCount = total,
            Page = page,
            PageSize = size,
        };
    }

    // The whole filtered, sorted set as a stream, for CSV export.
    public static IAsyncEnumerable<ProjectListItem> StreamAsync(
        PmgisDbContext db, ProjectQuery query, CancellationToken ct = default) =>
        Sort(Project(Filter(db, query)), query).AsAsyncEnumerable();

    // Projects within radiusKm of a point, nearest first.
    public static async Task<IReadOnlyList<NearbyProject>> NearbyAsync(
        PmgisDbContext db, double latitude, double longitude, double radiusKm,
        int limit = 100, CancellationToken ct = default)
    {
        var metres = radiusKm * 1000d;

        return await db.Database
            .SqlQuery<NearbyProject>(
                $"""
                 SELECT p."Id"          AS "Id",
                        p."ProjectCode" AS "ProjectCode",
                        p."Name"        AS "Name",
                        p."Latitude"    AS "Latitude",
                        p."Longitude"   AS "Longitude",
                        ST_Distance(
                          ST_SetSRID(ST_MakePoint(p."Longitude", p."Latitude"), 4326)::geography,
                          ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography
                        ) / 1000.0 AS "DistanceKm"
                 FROM "Projects" p
                 WHERE p."Longitude" IS NOT NULL
                   AND p."Latitude" IS NOT NULL
                   AND ST_DWithin(
                         ST_SetSRID(ST_MakePoint(p."Longitude", p."Latitude"), 4326)::geography,
                         ST_SetSRID(ST_MakePoint({longitude}, {latitude}), 4326)::geography,
                         {metres})
                 ORDER BY "DistanceKm"
                 LIMIT {limit}
                 """)
            .ToListAsync(ct);
    }
}

public sealed record NearbyProject
{
    public int Id { get; init; }
    public string ProjectCode { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public double? Latitude { get; init; }
    public double? Longitude { get; init; }
    public double DistanceKm { get; init; }
}
