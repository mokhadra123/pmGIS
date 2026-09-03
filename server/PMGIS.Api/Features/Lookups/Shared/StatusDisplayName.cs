namespace PMGIS.Api.Features.Lookups.Shared;

public static class StatusDisplayName
{
    // Turns "InProgress" into "In Progress" for display.
    public static string Humanise(string value) =>
        string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? " " + c : c.ToString()));
}
