using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PMGIS.Domain.Entities;

namespace PMGIS.Infrastructure.Data.Configurations;

public class ActivityConfiguration : IEntityTypeConfiguration<Activity>
{
    public void Configure(EntityTypeBuilder<Activity> builder)
    {
        builder.ToTable("Activities");
        builder.HasKey(a => a.Id);

        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();
        builder.Property(a => a.Status).HasConversion<string>().HasMaxLength(20);

        builder.HasOne(a => a.AssignedTo)
               .WithMany()
                .HasForeignKey(a => a.AssignedToUserId)
                .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(a => a.DeletedBy)
               .WithMany()
               .HasForeignKey(a => a.DeletedByUserId)
               .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(a => new { a.ProjectId, a.IsDeleted });

        builder.Ignore(a => a.DurationDays);
    }
}
