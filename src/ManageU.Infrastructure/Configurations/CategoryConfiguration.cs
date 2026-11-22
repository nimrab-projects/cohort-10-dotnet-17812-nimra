using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ManageU.Core.Entities;

namespace ManageU.Infrastructure.Configurations;

public class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        // Explicit table name mapping
        builder.ToTable("Categories");

        builder.HasKey(c => c.Id);

        builder.Property(c => c.Name)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(c => c.Description)
            .HasMaxLength(250);

        builder.Property(c => c.ColorHex)
            .IsRequired()
            .HasMaxLength(7)
            .HasDefaultValue("#6c757d");

        // Soft delete global query filter
        builder.HasQueryFilter(c => !c.IsDeleted);

        // Filtered unique index: unique among active categories
        builder.HasIndex(c => c.Name)
            .IsUnique()
            .HasFilter("[IsDeleted] = 0")
            .HasDatabaseName("IX_Categories_Name_ActiveUnique");
    }
}
