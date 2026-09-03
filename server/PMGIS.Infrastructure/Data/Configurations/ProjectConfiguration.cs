using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PMGIS.Domain.Entities;

namespace PMGIS.Infrastructure.Data.Configurations;

public class ProjectConfiguration : IEntityTypeConfiguration<Project>
{
    public void Configure(EntityTypeBuilder<Project> builder)
    {
        builder.ToTable("Projects");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProjectCode).HasMaxLength(8).IsRequired();
        builder.Property(p => p.Name).HasMaxLength(200).IsRequired();
        builder.Property(p => p.Description).HasMaxLength(500);
        builder.Property(p => p.Budget).HasPrecision(18, 2);
        builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasIndex(p => p.ProjectCode).IsUnique();

        builder.HasIndex(p => p.ObjectId)
               .IsUnique()
               .HasFilter("\"ObjectId\" IS NOT NULL");

        // Foreign keys are indexed by EF convention, so ProjectTypeId and the three
        // user keys are not declared here.
        builder.HasIndex(p => p.Name);
        builder.HasIndex(p => p.Status);
        builder.HasIndex(p => p.StartDate);
        builder.HasIndex(p => p.EndDate);
        builder.HasIndex(p => new { p.Latitude, p.Longitude });
        builder.HasIndex(p => new { p.LastModifiedOn, p.Id });

        builder.HasOne(p => p.ProjectType)
               .WithMany(t => t.Projects)
               .HasForeignKey(p => p.ProjectTypeId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.Owner)
               .WithMany()
               .HasForeignKey(p => p.OwnerUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.CreatedBy)
               .WithMany()
               .HasForeignKey(p => p.CreatedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(p => p.LastModifiedBy)
               .WithMany()
               .HasForeignKey(p => p.LastModifiedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(p => p.Activities)
               .WithOne(a => a.Project)
               .HasForeignKey(a => a.ProjectId)
               .OnDelete(DeleteBehavior.Cascade);

        builder.Property(p => p.DurationDays)
               .HasComputedColumnSql(
                   "CASE WHEN \"StartDate\" IS NULL OR \"EndDate\" IS NULL " +
                   "THEN NULL ELSE (\"EndDate\" - \"StartDate\") + 1 END",
                   stored: true);
        builder.HasIndex(p => p.DurationDays);

        builder.Ignore(p => p.HasLocation);
    }
}
