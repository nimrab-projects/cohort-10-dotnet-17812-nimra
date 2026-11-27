namespace ManageU.Application.Common.Interfaces;

public interface IIdentitySeeder
{
    Task SeedAsync(CancellationToken cancellationToken = default);
}
