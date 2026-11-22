using TaskFlow.Core.Common;
using TaskFlow.Core.Enums;

namespace TaskFlow.Core.Entities;

public class TaskItem : BaseEntity, ISoftDeletable
{
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskItemStatus Status { get; set; } = TaskItemStatus.Pending;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateTime? DueDate { get; set; }

    // Category relationship
    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;

    // Identity user decoupling (pure string IDs)
    public string? AssignedToUserId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;

    // Soft delete
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public string? DeletedByUserId { get; set; }
}
