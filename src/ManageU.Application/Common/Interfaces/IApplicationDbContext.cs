using Microsoft.EntityFrameworkCore;
using ManageU.Core.Entities;

namespace ManageU.Application.Common.Interfaces;

public interface IApplicationDbContext
{
    DbSet<TaskItem> Tasks { get; }
    DbSet<Category> Categories { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
