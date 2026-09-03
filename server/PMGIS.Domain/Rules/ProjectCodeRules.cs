using System.Text.RegularExpressions;

namespace PMGIS.Domain.Rules;

public static partial class ProjectCodeRules
{
    // The pattern expected is Three uppercase letters, a hyphen, four digits. For example ABC-0000.
    public const string Pattern = "^[A-Z]{3}-[0-9]{4}$";

    [GeneratedRegex(Pattern)]
    private static partial Regex Matcher();

    public static bool IsCodeValid(string? code) =>
      !string.IsNullOrWhiteSpace(code) && Matcher().IsMatch(code);
}
