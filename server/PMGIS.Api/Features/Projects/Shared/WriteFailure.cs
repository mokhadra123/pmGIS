namespace PMGIS.Api.Features.Projects.Shared;

// A write that could not be completed, and why.
public sealed record WriteFailure(string Message, string? Field = null)
{
    public const string ProjectNotFound = "Project not found.";

    public static WriteFailure NotFound { get; } = new(ProjectNotFound);

    // Set when several fields fail a rule that can only be checked against stored state.
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; init; }

    public static WriteFailure Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new(errors.Values.First()[0]) { FieldErrors = errors };

    public bool IsNotFound => Message == ProjectNotFound;

    // The single place a failure becomes an HTTP response.
    public IResult ToResult() => this switch
    {
        { IsNotFound: true } => Results.NotFound(),
        { FieldErrors: not null } => Results.ValidationProblem(
            FieldErrors.ToDictionary(e => e.Key, e => e.Value)),
        { Field: null } => Results.Problem(Message, statusCode: StatusCodes.Status409Conflict),
        _ => Results.ValidationProblem(new Dictionary<string, string[]> { [Field] = [Message] }),
    };
}
