using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PMGIS.Infrastructure.Data;


public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<PmgisDbContext>
{
    public PmgisDbContext CreateDbContext(string[] args)
    {
        var connection =
            Environment.GetEnvironmentVariable("PMGIS_MIGRATIONS_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=pmgisdb;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<PmgisDbContext>()
            .UseNpgsql(connection)
            .Options;

        return new PmgisDbContext(options);
    }
}
