using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

using PMGIS.Domain.Entities;

namespace PMGIS.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Name).HasMaxLength(150).IsRequired();
        builder.Property(u => u.Email).HasMaxLength(200).IsRequired();

        builder.HasIndex(u => u.Email).IsUnique();
    }
}
