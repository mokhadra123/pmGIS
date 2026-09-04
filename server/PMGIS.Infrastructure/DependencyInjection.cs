using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Gis;
using PMGIS.Infrastructure.Seeding;

namespace PMGIS.Infrastructure;

public static class DependencyInjection
{
    // Registers the data-access layer and the ArcGIS client.
    public const string DatabaseResourceName = "pmgisdb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<PmgisDbContext>(DatabaseResourceName);

        builder.Services.AddScoped<DataSeeder>();

        builder.Services
            .AddOptions<ArcGisOptions>()
            .Bind(builder.Configuration.GetSection(ArcGisOptions.SectionName))
            .ValidateOnStart();

        // Transient failures are retried with exponential backoff, capped at three
        // attempts, before the failure is surfaced. Token expiry is handled separately
        // inside FeatureServiceClient, because it needs a new token, not a repeat.
        builder.Services
            .AddHttpClient(ArcGisHttpClient.Name, client => client.Timeout = TimeSpan.FromSeconds(30))
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.UseJitter = true;
                options.Retry.Delay = TimeSpan.FromSeconds(1);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(10);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromSeconds(60);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromSeconds(30);
            });

        // Same three-attempt backoff, sized for bulk applyEdits payloads. The interactive
        // client's 10-second attempt timeout would classify a slow batch as transient and
        // retry it, which would duplicate features in the layer.
        builder.Services
            .AddHttpClient(ArcGisHttpClient.BulkName, client => client.Timeout = TimeSpan.FromMinutes(5))
            .AddStandardResilienceHandler(options =>
            {
                options.Retry.MaxRetryAttempts = 3;
                options.Retry.UseJitter = true;
                options.Retry.Delay = TimeSpan.FromSeconds(2);
                options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
                options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
                options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(3);
            });

        // Singleton: the token is cached across requests and refreshed under a lock.
        builder.Services.AddSingleton<ArcGisTokenProvider>();

        builder.Services.AddScoped<IFeatureServiceClient, FeatureServiceClient>();
        builder.Services.AddScoped<ReconciliationService>();

        return builder;
    }
}
