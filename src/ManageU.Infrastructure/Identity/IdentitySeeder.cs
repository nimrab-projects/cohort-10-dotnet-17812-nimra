using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ManageU.Application.Common.Interfaces;
using ManageU.Core.Constants;

namespace ManageU.Infrastructure.Identity;

public class IdentitySeeder : IIdentitySeeder
{
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ILogger<IdentitySeeder> _logger;

    public IdentitySeeder(
        RoleManager<IdentityRole> roleManager,
        ILogger<IdentitySeeder> logger)
    {
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        string[] roles = [Roles.Admin, Roles.User];

        foreach (var role in roles)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                _logger.LogInformation("Seeding initial role: {Role}", role);
                var result = await _roleManager.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded)
                {
                    _logger.LogError("Failed to seed role {Role}: {Errors}",
                        role, string.Join(", ", result.Errors.Select(e => e.Description)));
                }
            }
        }
    }
}
