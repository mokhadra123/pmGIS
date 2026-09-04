namespace PMGIS.Api.Features.Projects.Shared;

// Activities must sit inside the project's own date range.
public static class ActivityWindowRule
{
    public static void Check(
        IReadOnlyList<ProjectActivityInput> activities,
        DateOnly? projectStart,
        DateOnly? projectEnd,
        Action<string, string> addFailure)
    {
        if (projectStart is not { } start || projectEnd is not { } end)
        {
            return;
        }

        for (var i = 0; i < activities.Count; i++)
        {
            var a = activities[i];

            if (a.StartDate < start || a.EndDate > end)
            {
                addFailure(
                    $"Activities[{i}]",
                    $"\"{a.Name}\" runs {a.StartDate:yyyy-MM-dd} to {a.EndDate:yyyy-MM-dd}, " +
                    $"outside the project range {start:yyyy-MM-dd} to {end:yyyy-MM-dd}.");
            }
        }
    }
}
