using Bogus;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

using PMGIS.Domain.Entities;
using PMGIS.Domain.Enums;
using PMGIS.Domain.Rules;
using PMGIS.Infrastructure.Data;

namespace PMGIS.Infrastructure.Seeding;

// Creates the lookup data and the 5,000 projects the acceptance criteria call for.
public sealed class DataSeeder(PmgisDbContext db, ILogger<DataSeeder> logger)
{
    public const int ProjectCount = 5_000;

    private const int UserCount = 25;

    // Rows held in the change tracker at once.
    private const int BatchSize = 500;

    // Fixed so a reviewer running the seed twice gets identical data.
    private const int RandomSeed = 20260904;

    private static readonly (string Code, string Name)[] Types =
    [
        ("INFRA", "Infrastructure"),
        ("WATER", "Water & Sanitation"),
        ("ROAD", "Roads & Transport"),
        ("ENERGY", "Energy"),
        ("BUILD", "Buildings"),
        ("ENV", "Environmental"),
        ("TELECOM", "Telecommunications"),
    ];

    // Cairo, Giza, Alexandria, Aswan, Luxor, Port Said, Suez, Hurghada.
    private static readonly (double Lon, double Lat)[] Anchors =
    [
        (31.2357, 30.0444), (31.1342, 29.9792), (29.9187, 31.2001), (32.8998, 24.0889),
        (32.6396, 25.6872), (32.3019, 31.2653), (32.5498, 29.9668), (33.8116, 27.2579),
    ];

    public async Task SeedAsync(CancellationToken ct = default)
    {
        await db.Database.MigrateAsync(ct);

        if (await db.Projects.AnyAsync(ct))
        {
            logger.LogInformation("Database already seeded; skipping.");
            return;
        }

        Randomizer.Seed = new Random(RandomSeed);

        // Order is dictated by the foreign keys: a project cannot exist before the user
        // recorded as its creator, and an activity cannot exist before its assignee.
        var users = SeedUsers();
        var types = SeedTypes();

        await db.Users.AddRangeAsync(users, ct);
        await db.ProjectTypes.AddRangeAsync(types, ct);
        await db.SaveChangesAsync(ct);

        logger.LogInformation("Seeding {Count} projects…", ProjectCount);

        var userIds = users.Select(u => u.Id).ToArray();
        var faker = new Faker();
        var batch = new List<Project>(BatchSize);

        for (var i = 1; i <= ProjectCount; i++)
        {
            batch.Add(BuildProject(i, faker, types, userIds));

            if (batch.Count < BatchSize && i != ProjectCount)
            {
                continue;
            }

            await db.Projects.AddRangeAsync(batch, ct);
            await db.SaveChangesAsync(ct);

            // The graphs have been written; drop them so a long run does not accumulate
            // thousands of tracked entities.
            db.ChangeTracker.Clear();
            batch.Clear();

            logger.LogInformation("Seeded {Done} of {Total} projects.", i, ProjectCount);
        }

        logger.LogInformation("Seed complete: {Projects} projects.", ProjectCount);
    }

    private static List<User> SeedUsers()
    {
        var faker = new Faker<User>()
            .RuleFor(u => u.Name, f => f.Name.FullName())
            .RuleFor(u => u.Email, (f, u) => f.Internet.Email(
                u.Name.Split(' ')[0], u.Name.Split(' ')[^1], uniqueSuffix: f.UniqueIndex.ToString()))
            .RuleFor(u => u.IsActive, true);

        return faker.Generate(UserCount);
    }

    private static List<ProjectType> SeedTypes() =>
        [.. Types.Select((t, index) => new ProjectType
        {
            Code = t.Code,
            Name = t.Name,
            SortOrder = index + 1,
            IsActive = true,
        })];

    private static Project BuildProject(
        int sequence, Faker faker, List<ProjectType> types, int[] userIds)
    {
        var type = faker.PickRandom(types);

        var start = DateOnly.FromDateTime(faker.Date.Between(
            new DateTime(2024, 1, 1), new DateTime(2027, 6, 30)));
        var end = start.AddDays(faker.Random.Int(30, 720));

        // Sequential number, so uniqueness holds regardless of the letter prefix.
        var code = $"{type.Code[..3]}-{sequence:0000}";

        // One project in eight has no location, so the "Zoom to Project is disabled
        // without a stored location" rule has data to exercise.
        var hasLocation = faker.Random.Int(1, 8) != 1;
        double? latitude = null;
        double? longitude = null;

        if (hasLocation)
        {
            var anchor = faker.PickRandom(Anchors);
            longitude = Math.Round(anchor.Lon + faker.Random.Double(-0.35, 0.35), 6);
            latitude = Math.Round(anchor.Lat + faker.Random.Double(-0.35, 0.35), 6);
        }

        var createdOn = new DateTimeOffset(
            faker.Date.Between(new DateTime(2024, 1, 1), new DateTime(2026, 9, 1)),
            TimeSpan.Zero);

        var project = new Project
        {
            ProjectCode = code,
            Name = $"{faker.Address.City()} {type.Name} {faker.Commerce.ProductAdjective()}",
            Description = faker.Random.Bool(0.7f) ? faker.Lorem.Sentence(12) : null,
            ProjectTypeId = type.Id,
            Status = faker.PickRandom<ProjectStatus>(),
            StartDate = start,
            EndDate = end,
            Budget = Math.Round(faker.Random.Decimal(50_000, 25_000_000), 2),
            OwnerUserId = faker.PickRandom(userIds),
            // Left null deliberately: no feature exists in the layer yet. The backfill
            // slice populates these once the ArcGIS client is in place.
            ObjectId = null,
            Latitude = latitude,
            Longitude = longitude,
            CreatedByUserId = faker.PickRandom(userIds),
            CreatedOn = createdOn,
            LastModifiedByUserId = faker.PickRandom(userIds),
            LastModifiedOn = createdOn.AddDays(faker.Random.Int(0, 200)),
        };

        foreach (var activity in BuildActivities(faker, project, userIds))
        {
            project.Activities.Add(activity);
        }

        return project;
    }

    private static IEnumerable<Activity> BuildActivities(
        Faker faker, Project project, int[] userIds)
    {
        var count = faker.Random.Int(0, 6);

        for (var i = 1; i <= count; i++)
        {
            // Activity dates must fall inside the project's own range, which is the rule
            // the create and update validators enforce. Seeded data must not violate it.
            var start = faker.Date.BetweenDateOnly(project.StartDate!.Value, project.EndDate!.Value);
            var maxLength = project.EndDate.Value.DayNumber - start.DayNumber;
            var end = start.AddDays(faker.Random.Int(0, Math.Max(0, Math.Min(maxLength, 120))));

            var status = faker.PickRandom<ActivityStatus>();

            yield return new Activity
            {
                Name = $"{faker.Hacker.Verb()} {faker.Hacker.Noun()} {i}",
                StartDate = start,
                EndDate = end,
                Status = status,
                AssignedToUserId = faker.PickRandom(userIds),
                // The status dictates the value; asking the domain rule keeps seeded rows
                // consistent with what the validators would accept.
                PercentComplete = ActivityStatusTransitions.NormalizePercentComplete(
                    status, faker.Random.Int(0, 100)),
                IsDeleted = false,
                CreatedOn = project.CreatedOn,
                LastModifiedOn = project.LastModifiedOn,
            };
        }
    }
}
