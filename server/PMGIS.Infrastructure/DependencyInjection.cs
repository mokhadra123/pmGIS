using Microsoft.Extensions.Hosting;

using PMGIS.Infrastructure.Data;

namespace PMGIS.Infrastructure;

public static class DependencyInjection
{
    public const string DatabaseResourceName = "pmgisdb";

    public static IHostApplicationBuilder AddInfrastructure(this IHostApplicationBuilder builder)
    {
        builder.AddNpgsqlDbContext<PmgisDbContext>(DatabaseResourceName);

        return builder;
    }
}
