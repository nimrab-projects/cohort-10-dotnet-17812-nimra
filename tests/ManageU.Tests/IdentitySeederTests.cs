using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using ManageU.Core.Constants;
using ManageU.Infrastructure.Identity;
using Xunit;

namespace ManageU.Tests;

public class IdentitySeederTests
{
    private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
    private readonly Mock<ILogger<IdentitySeeder>> _mockLogger;
    private readonly IdentitySeeder _sut;

    public IdentitySeederTests()
    {
        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
            roleStore.Object, null!, null!, null!, null!);
        _mockLogger = new Mock<ILogger<IdentitySeeder>>();

        _sut = new IdentitySeeder(_mockRoleManager.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task SeedAsync_WhenRolesDoNotExist_CreatesAdminAndUserRoles()
    {
        // Arrange
        _mockRoleManager.Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(false);

        _mockRoleManager.Setup(m => m.CreateAsync(It.IsAny<IdentityRole>()))
            .ReturnsAsync(IdentityResult.Success);

        // Act
        await _sut.SeedAsync();

        // Assert
        _mockRoleManager.Verify(m => m.CreateAsync(It.Is<IdentityRole>(r => r.Name == Roles.Admin)), Times.Once);
        _mockRoleManager.Verify(m => m.CreateAsync(It.Is<IdentityRole>(r => r.Name == Roles.User)), Times.Once);
    }

    [Fact]
    public async Task SeedAsync_WhenRolesAlreadyExist_DoesNotCreateRolesAgain()
    {
        // Arrange
        _mockRoleManager.Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        // Act
        await _sut.SeedAsync();

        // Assert
        _mockRoleManager.Verify(m => m.CreateAsync(It.IsAny<IdentityRole>()), Times.Never);
    }
}
