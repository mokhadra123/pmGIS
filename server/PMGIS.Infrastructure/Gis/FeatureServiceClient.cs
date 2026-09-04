using System.Globalization;
using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PMGIS.Infrastructure.Gis;

public sealed class FeatureServiceClient(
    IHttpClientFactory httpClientFactory,
    ArcGisTokenProvider tokenProvider,
    IOptions<ArcGisOptions> options,
    ILogger<FeatureServiceClient> logger) : IFeatureServiceClient
{
    private readonly ArcGisOptions _options = options.Value;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    // Transient failures (5xx, timeouts) are retried with backoff by the resilience
    // handler on the named HttpClient. Token expiry is retried once here instead,
    // because it needs a new token rather than the same request again.
    private const int TokenRetryCodeInvalid = 498;
    private const int TokenRetryCodeRequired = 499;

    public async Task<long> AddFeatureAsync(
        string projectCode, int sourceId, FeaturePoint point, CancellationToken ct = default)
    {
        var adds = JsonSerializer.Serialize(new[]
        {
            new
            {
                geometry = new
                {
                    x = point.Longitude,
                    y = point.Latitude,
                    spatialReference = new { wkid = 4326 },
                },
                attributes = new Dictionary<string, object?>
                {
                    [_options.CodeField] = projectCode,
                    [_options.SourceIdField] = sourceId,
                },
            },
        }, Json);

        var response = await ApplyEditsAsync(("adds", adds), ct);
        var result = Single(response.AddResults, "add");

        logger.LogInformation("Created feature {ObjectId} for project {ProjectCode}", result.ObjectId, projectCode);

        return result.ObjectId;
    }

    public async Task UpdateFeatureAsync(
        long objectId, string projectCode, FeaturePoint point, CancellationToken ct = default)
    {
        var updates = JsonSerializer.Serialize(new[]
        {
            new
            {
                geometry = new
                {
                    x = point.Longitude,
                    y = point.Latitude,
                    spatialReference = new { wkid = 4326 },
                },
                attributes = new Dictionary<string, object?>
                {
                    ["OBJECTID"] = objectId,
                    [_options.CodeField] = projectCode,
                },
            },
        }, Json);

        var response = await ApplyEditsAsync(("updates", updates), ct);
        Single(response.UpdateResults, "update");

        logger.LogInformation("Updated feature {ObjectId} in place", objectId);
    }

    public async Task DeleteFeatureAsync(long objectId, CancellationToken ct = default)
    {
        var response = await ApplyEditsAsync(
            ("deletes", objectId.ToString(CultureInfo.InvariantCulture)), ct);

        Single(response.DeleteResults, "delete");

        logger.LogInformation("Deleted feature {ObjectId}", objectId);
    }

    // Bulk create.
    public async Task<IReadOnlyList<FeatureEditResult>> AddFeaturesAsync(
        IReadOnlyList<FeatureAdd> features, CancellationToken ct = default)
    {
        if (features.Count == 0)
        {
            return [];
        }

        var adds = JsonSerializer.Serialize(features.Select(f => new
        {
            geometry = new
            {
                x = f.Point.Longitude,
                y = f.Point.Latitude,
                spatialReference = new { wkid = 4326 },
            },
            attributes = new Dictionary<string, object?>
            {
                [_options.CodeField] = f.ProjectCode,
                [_options.SourceIdField] = f.SourceId,
            },
        }), Json);

        var response = await ApplyEditsAsync(
            ("adds", adds), ct, rollbackOnFailure: false, clientName: ArcGisHttpClient.BulkName);

        // Without a one-to-one, in-order mapping there is no safe way to say which
        // ObjectId belongs to which project, so refuse to guess.
        if (response.AddResults.Count != features.Count)
        {
            throw new FeatureServiceException(
                $"applyEdits returned {response.AddResults.Count} add results for " +
                $"{features.Count} features; results cannot be mapped back to projects.");
        }

        var succeeded = response.AddResults.Count(r => r.Success);

        logger.LogInformation(
            "Batch add: {Succeeded}/{Total} features created", succeeded, features.Count);

        return response.AddResults;
    }

    public async Task<IReadOnlyList<FeatureRecord>> GetOwnedFeaturesAsync(CancellationToken ct = default)
    {
        var all = new List<FeatureRecord>();
        var offset = 0;

        while (true)
        {
            var form = new Dictionary<string, string>
            {
                ["f"] = "json",
                ["where"] = $"{_options.SourceIdField} >= {_options.SourceIdBase}",
                // Only the attributes actually needed, and no geometry: the brief asks
                // for minimal payloads.
                ["outFields"] = $"OBJECTID,{_options.CodeField},{_options.SourceIdField}",
                ["returnGeometry"] = "false",
                ["resultOffset"] = offset.ToString(CultureInfo.InvariantCulture),
                ["resultRecordCount"] = _options.QueryPageSize.ToString(CultureInfo.InvariantCulture),
            };

            using var doc = await SendAsync("query", form, ct);

            if (!doc.RootElement.TryGetProperty("features", out var features))
            {
                break;
            }

            var batch = 0;

            foreach (var feature in features.EnumerateArray())
            {
                var attrs = feature.GetProperty("attributes");

                all.Add(new FeatureRecord
                {
                    ObjectId = attrs.GetProperty("OBJECTID").GetInt64(),
                    ProjectCode = attrs.TryGetProperty(_options.CodeField, out var c) && c.ValueKind is JsonValueKind.String
                        ? c.GetString()
                        : null,
                    SourceId = attrs.TryGetProperty(_options.SourceIdField, out var s) && s.ValueKind is JsonValueKind.Number
                        ? s.GetInt32()
                        : null,
                });

                batch++;
            }

            var exceeded = doc.RootElement.TryGetProperty("exceededTransferLimit", out var e) && e.GetBoolean();

            if (batch == 0 || !exceeded)
            {
                break;
            }

            offset += batch;
        }

        return all;
    }

    // ---------------------------------------------------------------------------

    private async Task<ApplyEditsResponse> ApplyEditsAsync(
        (string Key, string Value) edit,
        CancellationToken ct,
        bool rollbackOnFailure = true,
        string clientName = ArcGisHttpClient.Name)
    {
        var form = new Dictionary<string, string>
        {
            ["f"] = "json",
            // All edits in one operation per save, as the brief requires.
            [edit.Key] = edit.Value,
            ["rollbackOnFailure"] = rollbackOnFailure ? "true" : "false",
        };

        using var doc = await SendAsync("applyEdits", form, ct, clientName);

        var response = doc.RootElement.Deserialize<ApplyEditsResponse>(Json)
                       ?? throw new FeatureServiceException("Empty response from applyEdits.");

        if (response.Error is { } topLevel)
        {
            throw new FeatureServiceException(
                $"applyEdits failed: {topLevel.Message} (code {topLevel.Code})", topLevel.Code);
        }

        return response;
    }

    // Posts a form to the layer and returns the parsed body.
    private async Task<JsonDocument> SendAsync(
        string operation,
        Dictionary<string, string> form,
        CancellationToken ct,
        string clientName = ArcGisHttpClient.Name)
    {
        for (var attempt = 0; ; attempt++)
        {
            var token = await tokenProvider.GetTokenAsync(ct);

            var payload = new Dictionary<string, string>(form);

            if (token is not null)
            {
                payload["token"] = token;
            }

            var client = httpClientFactory.CreateClient(clientName);
            var url = $"{_options.FeatureLayerUrl.TrimEnd('/')}/{operation}";

            using var content = new FormUrlEncodedContent(payload);
            using var response = await client.PostAsync(url, content, ct);

            response.EnsureSuccessStatusCode();

            var body = await response.Content.ReadAsStringAsync(ct);
            var doc = JsonDocument.Parse(body);

            // A 200 carrying an error object is the case the brief warns about.
            if (doc.RootElement.TryGetProperty("error", out var error) &&
                error.TryGetProperty("code", out var codeElement))
            {
                var code = codeElement.GetInt32();

                if (code is TokenRetryCodeInvalid or TokenRetryCodeRequired && attempt == 0)
                {
                    logger.LogWarning("ArcGIS token rejected ({Code}); refreshing and retrying once.", code);
                    tokenProvider.Invalidate();
                    doc.Dispose();
                    continue;
                }

                var message = error.TryGetProperty("message", out var m) ? m.GetString() : "unknown error";
                doc.Dispose();

                throw new FeatureServiceException($"{operation} failed: {message} (code {code})", code);
            }

            return doc;
        }
    }

    // HTTP 200 is not success: exactly one result must come back and report success.
    private static FeatureEditResult Single(List<FeatureEditResult> results, string operation)
    {
        if (results.Count != 1)
        {
            throw new FeatureServiceException(
                $"Expected exactly one {operation} result, received {results.Count}.");
        }

        var result = results[0];

        if (!result.Success)
        {
            throw new FeatureServiceException(
                $"Feature {operation} failed: {result.Error?.Description ?? "no description"} " +
                $"(code {result.Error?.Code})", result.Error?.Code);
        }

        return result;
    }

}
