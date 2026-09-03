using Microsoft.EntityFrameworkCore;

using PMGIS.Domain.Entities;

namespace PMGIS.Infrastructure.Data;

public class PmgisDbContext(DbContextOptions<PmgisDbContext> options) : DbContext(options)
{
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Activity> Activities => Set<Activity>();
    public DbSet<ProjectType> ProjectTypes => Set<ProjectType>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PmgisDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}
