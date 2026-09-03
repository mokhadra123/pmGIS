using Microsoft.Extensions.Hosting;

using Microsoft.Extensions.DependencyInjection;

using PMGIS.Infrastructure.Data;
using PMGIS.Infrastructure.Seeding;

namespace PMGIS.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseResourceName = "pmgisdb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<PmgisDbContext>(DatabaseResourceName);

        builder.Services.AddScoped<DataSeeder>();

        return builder;
    }
}
