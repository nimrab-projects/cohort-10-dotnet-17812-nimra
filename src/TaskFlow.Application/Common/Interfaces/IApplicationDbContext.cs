using Microsoft.EntityFrameworkCore;
using TaskFlow.Core.Entities;

namespace TaskFlow.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TaskItem> Tasks { get; }
    DbSet<Category> Categories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
