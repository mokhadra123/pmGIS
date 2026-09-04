namespace PMGIS.Infrastructure.Gis;

public sealed class ArcGisOptions
{
    public const string SectionName = "ArcGis";

    // Full URL of the Project Feature Layer, ending in the layer index.
    public string FeatureLayerUrl { get; set; } =
        "https://services3.arcgis.com/GVgbJbqm8hXASVYi/ArcGIS/rest/services/my_points/FeatureServer/0";

    // Token endpoint.
    public string TokenUrl { get; set; } = "https://www.arcgis.com/sharing/rest/generateToken";

    // Optional.
    public string? Username { get; set; }
    public string? Password { get; set; }

    // Features this application owns carry a SOURCEID at or above this value.
    public int SourceIdBase { get; set; } = 5_000_000;

    // Attribute holding the Project Code.
    public string CodeField { get; set; } = "name";

    public string SourceIdField { get; set; } = "SOURCEID";

    // Page size for layer queries, kept under the layer's maxRecordCount.
    public int QueryPageSize { get; set; } = 1000;
}
