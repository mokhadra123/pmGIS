using System.Text.Json;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PMGIS.Infrastructure.Gis;

// Caches an ArcGIS token and re-issues it on demand.
public sealed class ArcGisTokenProvider(
    IHttpClientFactory httpClientFactory,
    IOptions<ArcGisOptions> options,
    ILogger<ArcGisTokenProvider> logger)
{
    private readonly ArcGisOptions _options = options.Value;
    private readonly SemaphoreSlim _gate = new(1, 1);

    private string? _token;
    private DateTimeOffset _expiresOn = DateTimeOffset.MinValue;

    public bool IsAnonymous =>
        string.IsNullOrWhiteSpace(_options.Username) || string.IsNullOrWhiteSpace(_options.Password);

    // Returns a valid token, or null when the layer is used anonymously.
    public async Task<string?> GetTokenAsync(CancellationToken ct = default)
    {
        if (IsAnonymous)
        {
            return null;
        }

        // Refresh a minute early so a token cannot expire mid-flight.
        if (_token is not null && DateTimeOffset.UtcNow < _expiresOn.AddMinutes(-1))
        {
            return _token;
        }

        await _gate.WaitAsync(ct);
        try
        {
            if (_token is not null && DateTimeOffset.UtcNow < _expiresOn.AddMinutes(-1))
            {
                return _token;
            }

            var client = httpClientFactory.CreateClient(ArcGisHttpClient.Name);

            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["f"] = "json",
                ["username"] = _options.Username!,
                ["password"] = _options.Password!,
                ["referer"] = "https://www.arcgis.com",
                ["expiration"] = "60",
            });

            using var response = await client.PostAsync(_options.TokenUrl, form, ct);
            response.EnsureSuccessStatusCode();

            using var stream = await response.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                throw new FeatureServiceException(
                    $"ArcGIS token request failed: {error}",
                    error.TryGetProperty("code", out var c) ? c.GetInt32() : null);
            }

            _token = doc.RootElement.GetProperty("token").GetString();
            var expiresMs = doc.RootElement.GetProperty("expires").GetInt64();
            _expiresOn = DateTimeOffset.FromUnixTimeMilliseconds(expiresMs);

            logger.LogInformation("Acquired ArcGIS token, expires {ExpiresOn:o}", _expiresOn);

            return _token;
        }
        finally
        {
            _gate.Release();
        }
    }

    // Drops the cached token so the next call fetches a fresh one.
    public void Invalidate()
    {
        _token = null;
        _expiresOn = DateTimeOffset.MinValue;
    }
}

public static class ArcGisHttpClient
{
    public const string Name = "arcgis";

    // Client used for bulk applyEdits payloads.
    public const string BulkName = "arcgis-bulk";
}
