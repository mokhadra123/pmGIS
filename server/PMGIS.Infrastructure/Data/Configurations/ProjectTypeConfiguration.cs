using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PMGIS.Domain.Entities;

namespace PMGIS.Infrastructure.Data.Configurations;

public class ProjectTypeConfiguration : IEntityTypeConfiguration<ProjectType>
{
    public void Configure(EntityTypeBuilder<ProjectType> builder)
    {
        builder.ToTable("ProjectTypes");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Code).HasMaxLength(30).IsRequired();
        builder.Property(t => t.Name).HasMaxLength(100).IsRequired();

        builder.HasIndex(t => t.Code).IsUnique();
    }
}
