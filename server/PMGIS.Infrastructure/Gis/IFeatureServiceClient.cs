namespace PMGIS.Infrastructure.Gis;

// The only place the application talks to the ArcGIS feature layer.
public interface IFeatureServiceClient
{
    // Creates the point and returns the ObjectId the service assigned.
    Task<long> AddFeatureAsync(string projectCode, int sourceId, FeaturePoint point, CancellationToken ct = default);

    // Moves an existing point.
    Task UpdateFeatureAsync(long objectId, string projectCode, FeaturePoint point, CancellationToken ct = default);

    Task DeleteFeatureAsync(long objectId, CancellationToken ct = default);

    // One applyEdits call; results come back in the same order as the input.
    Task<IReadOnlyList<FeatureEditResult>> AddFeaturesAsync(
        IReadOnlyList<FeatureAdd> features, CancellationToken ct = default);

    // Every feature this application owns, paged with resultOffset/resultRecordCount.
    Task<IReadOnlyList<FeatureRecord>> GetOwnedFeaturesAsync(CancellationToken ct = default);
}
