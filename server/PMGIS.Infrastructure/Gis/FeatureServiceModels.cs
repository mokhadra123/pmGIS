using System.Text.Json.Serialization;

namespace PMGIS.Infrastructure.Gis;

// A point in the feature layer's spatial reference (WGS84).
public sealed record FeaturePoint(double Longitude, double Latitude);

// Result of a single feature edit, as returned inside applyEdits.
public sealed record FeatureEditResult
{
    [JsonPropertyName("objectId")] public long ObjectId { get; init; }
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("error")] public FeatureEditError? Error { get; init; }
}

public sealed record FeatureEditError
{
    [JsonPropertyName("code")] public int Code { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
}

public sealed record ApplyEditsResponse
{
    [JsonPropertyName("addResults")] public List<FeatureEditResult> AddResults { get; init; } = [];
    [JsonPropertyName("updateResults")] public List<FeatureEditResult> UpdateResults { get; init; } = [];
    [JsonPropertyName("deleteResults")] public List<FeatureEditResult> DeleteResults { get; init; } = [];

    // Top-level error, e.g.
    [JsonPropertyName("error")] public ArcGisError? Error { get; init; }
}

public sealed record ArcGisError
{
    [JsonPropertyName("code")] public int Code { get; init; }
    [JsonPropertyName("message")] public string? Message { get; init; }
}

// Minimal attribute view of a feature; geometry is not requested.
public sealed record FeatureRecord
{
    public long ObjectId { get; init; }
    public string? ProjectCode { get; init; }
    public int? SourceId { get; init; }
}

// Thrown when the feature service reports failure.
public sealed class FeatureServiceException(string message, int? code = null)
    : Exception(message)
{
    public int? Code { get; } = code;
}

// One feature to create, as supplied to AddFeaturesAsync.
public sealed record FeatureAdd(string ProjectCode, int SourceId, FeaturePoint Point);
