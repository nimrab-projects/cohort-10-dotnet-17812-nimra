using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Core.Entities;
using TaskFlow.Infrastructure.Identity;

namespace TaskFlow.Infrastructure.Configurations;

public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
{
    public void Configure(EntityTypeBuilder<TaskItem> builder)
    {
        // Explicit table name mapping
        builder.ToTable("Tasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Title)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(t => t.Description)
            .HasMaxLength(2000);

        builder.Property(t => t.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(t => t.Priority)
            .IsRequired()
            .HasConversion<int>();

        // Category Relationship: delete behavior restrict
        builder.HasOne(t => t.Category)
            .WithMany(c => c.Tasks)
            .HasForeignKey(t => t.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // User Relationships: delete behavior restrict
        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.AssignedTasks)
            .HasForeignKey(t => t.AssignedToUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ApplicationUser>()
            .WithMany(u => u.CreatedTasks)
            .HasForeignKey(t => t.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        // Soft delete global query filter
        builder.HasQueryFilter(t => !t.IsDeleted);

        // Indexes (zero redundancy)
        builder.HasIndex(t => new { t.AssignedToUserId, t.Status })
            .HasDatabaseName("IX_Tasks_AssignedToUserId_Status");

        builder.HasIndex(t => t.CreatedByUserId)
            .HasDatabaseName("IX_Tasks_CreatedByUserId");

        builder.HasIndex(t => t.CategoryId)
            .HasDatabaseName("IX_Tasks_CategoryId");

        builder.HasIndex(t => new { t.DueDate, t.Status })
            .HasDatabaseName("IX_Tasks_DueDate_Status");
    }
}
