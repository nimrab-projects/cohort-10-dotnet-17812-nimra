using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Moq;
using ManageU.Application.Common.Exceptions;
using ManageU.Application.Common.Interfaces;
using ManageU.Application.DTOs.Auth;
using ManageU.Core.Constants;
using ManageU.Infrastructure.Identity;
using Xunit;

namespace ManageU.Tests;

public class AuthServiceTests
{
    private readonly Mock<UserManager<ApplicationUser>> _mockUserManager;
    private readonly Mock<SignInManager<ApplicationUser>> _mockSignInManager;
    private readonly Mock<RoleManager<IdentityRole>> _mockRoleManager;
    private readonly Mock<IJwtTokenGenerator> _mockJwtGenerator;
    private readonly Mock<ILogger<AuthService>> _mockLogger;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var userStore = new Mock<IUserStore<ApplicationUser>>();
        _mockUserManager = new Mock<UserManager<ApplicationUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var contextAccessor = new Mock<IHttpContextAccessor>();
        var userPrincipalFactory = new Mock<IUserClaimsPrincipalFactory<ApplicationUser>>();
        _mockSignInManager = new Mock<SignInManager<ApplicationUser>>(
            _mockUserManager.Object,
            contextAccessor.Object,
            userPrincipalFactory.Object,
            null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<IdentityRole>>();
        _mockRoleManager = new Mock<RoleManager<IdentityRole>>(
            roleStore.Object, null!, null!, null!, null!);

        _mockJwtGenerator = new Mock<IJwtTokenGenerator>();
        _mockLogger = new Mock<ILogger<AuthService>>();

        _sut = new AuthService(
            _mockUserManager.Object,
            _mockSignInManager.Object,
            _mockRoleManager.Object,
            _mockJwtGenerator.Object,
            _mockLogger.Object);
    }

    [Fact]
    public async Task RegisterAsync_WithValidData_CreatesUserWithUserRoleAndReturnsTokenWithMatchingExpiry()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Alice Johnson",
            Email = "alice@manageu.com",
            Password = "SecureP@ssword123",
            ConfirmPassword = "SecureP@ssword123"
        };

        var expectedExpiresAt = DateTime.UtcNow.AddMinutes(120);
        var tokenResult = new GeneratedTokenResult("valid-jwt-token", expectedExpiresAt);

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) => u.Id = "new-user-123");

        _mockRoleManager.Setup(m => m.RoleExistsAsync(Roles.User))
            .ReturnsAsync(true);

        _mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.User))
            .ReturnsAsync(IdentityResult.Success);

        _mockJwtGenerator.Setup(g => g.GenerateToken("new-user-123", request.Email, "Alice Johnson", It.IsAny<IEnumerable<string>>()))
            .Returns(tokenResult);

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("valid-jwt-token", result.Token);
        Assert.Equal("new-user-123", result.UserId);
        Assert.Equal("Alice Johnson", result.FullName);
        Assert.Equal(request.Email, result.Email);
        Assert.Contains(Roles.User, result.Roles);
        Assert.Equal(expectedExpiresAt, result.ExpiresAt);

        _mockUserManager.Verify(m => m.CreateAsync(It.Is<ApplicationUser>(u =>
            u.Email == request.Email &&
            u.FullName == "Alice Johnson" &&
            u.IsActive), request.Password), Times.Once);

        _mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.User), Times.Once);
        _mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.Admin), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WithDuplicateEmail_ThrowsConflictException()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Duplicate User",
            Email = "existing@manageu.com",
            Password = "SecureP@ssword123"
        };

        var existingUser = new ApplicationUser { Email = request.Email, Id = "existing-1" };
        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(existingUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ConflictException>(() => _sut.RegisterAsync(request));
        Assert.Contains("already exists", ex.Message);
        _mockUserManager.Verify(m => m.CreateAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RegisterAsync_WhenPasswordShorterThan12Chars_ThrowsValidationException()
    {
        // Arrange: Password shorter than 12 characters rejected by Identity
        var request = new RegisterRequestDto
        {
            FullName = "Short Password User",
            Email = "short@manageu.com",
            Password = "ShortPass1!"
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        var errors = new[]
        {
            new IdentityError { Code = "PasswordTooShort", Description = "Passwords must be at least 12 characters." }
        };

        _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Failed(errors));

        // Act & Assert
        var ex = await Assert.ThrowsAsync<ValidationException>(() => _sut.RegisterAsync(request));
        Assert.True(ex.Errors.ContainsKey("General"));
        Assert.Contains("Passwords must be at least 12 characters.", ex.Errors["General"]);
    }

    [Fact]
    public async Task RegisterAsync_PreventsAdminRoleSelfAssignment()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            FullName = "Attacker Trying Admin",
            Email = "attacker@manageu.com",
            Password = "SecureP@ssword123"
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        _mockUserManager.Setup(m => m.CreateAsync(It.IsAny<ApplicationUser>(), request.Password))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<ApplicationUser, string>((u, _) => u.Id = "attacker-1");

        _mockRoleManager.Setup(m => m.RoleExistsAsync(It.IsAny<string>()))
            .ReturnsAsync(true);

        _mockUserManager.Setup(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);

        _mockJwtGenerator.Setup(g => g.GenerateToken(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
            .Returns(new GeneratedTokenResult("token", DateTime.UtcNow.AddMinutes(60)));

        // Act
        var result = await _sut.RegisterAsync(request);

        // Assert: Registration must only assign Roles.User, NEVER Roles.Admin
        _mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.User), Times.Once);
        _mockUserManager.Verify(m => m.AddToRoleAsync(It.IsAny<ApplicationUser>(), Roles.Admin), Times.Never);
        Assert.DoesNotContain(Roles.Admin, result.Roles);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_ReturnsTokenAndRolesWithMatchingExpiry()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "bob@manageu.com",
            Password = "SecureP@ssword123"
        };

        var user = new ApplicationUser
        {
            Id = "user-bob-1",
            Email = request.Email,
            FullName = "Bob Smith",
            IsActive = true
        };

        var expectedExpiresAt = DateTime.UtcNow.AddMinutes(60);

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(s => s.CheckPasswordSignInAsync(user, request.Password, true))
            .ReturnsAsync(SignInResult.Success);

        _mockUserManager.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.User });

        _mockJwtGenerator.Setup(g => g.GenerateToken(user.Id, user.Email, user.FullName, It.IsAny<IEnumerable<string>>()))
            .Returns(new GeneratedTokenResult("bob-jwt-token", expectedExpiresAt));

        // Act
        var result = await _sut.LoginAsync(request);

        // Assert
        Assert.NotNull(result);
        Assert.Equal("bob-jwt-token", result.Token);
        Assert.Equal(user.Id, result.UserId);
        Assert.Equal("Bob Smith", result.FullName);
        Assert.Contains(Roles.User, result.Roles);
        Assert.Equal(expectedExpiresAt, result.ExpiresAt);
    }

    [Fact]
    public async Task LoginAsync_WithUnknownEmail_ThrowsUnauthorizedException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "nonexistent@manageu.com",
            Password = "SomePassword123!"
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync((ApplicationUser?)null);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request));
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ThrowsUnauthorizedException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "inactive@manageu.com",
            Password = "SecureP@ssword123"
        };

        var inactiveUser = new ApplicationUser
        {
            Id = "inactive-1",
            Email = request.Email,
            FullName = "Inactive User",
            IsActive = false
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(inactiveUser);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request));
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_WithIncorrectPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "bob@manageu.com",
            Password = "WrongPassword123!"
        };

        var user = new ApplicationUser
        {
            Id = "user-bob-1",
            Email = request.Email,
            FullName = "Bob Smith",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(s => s.CheckPasswordSignInAsync(user, request.Password, true))
            .ReturnsAsync(SignInResult.Failed);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request));
        Assert.Equal("Invalid email or password.", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_WhenAccountIsLockedOut_ThrowsUnauthorizedException()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "locked@manageu.com",
            Password = "SecureP@ssword123"
        };

        var user = new ApplicationUser
        {
            Id = "user-locked-1",
            Email = request.Email,
            FullName = "Locked User",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByEmailAsync(request.Email))
            .ReturnsAsync(user);

        _mockSignInManager.Setup(s => s.CheckPasswordSignInAsync(user, request.Password, true))
            .ReturnsAsync(SignInResult.LockedOut);

        // Act & Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() => _sut.LoginAsync(request));
        Assert.Equal("User account is locked out.", ex.Message);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithExistingActiveUser_ReturnsCurrentUserDto()
    {
        // Arrange
        var userId = "user-123";
        var user = new ApplicationUser
        {
            Id = userId,
            Email = "user@manageu.com",
            FullName = "Active User",
            IsActive = true
        };

        _mockUserManager.Setup(m => m.FindByIdAsync(userId))
            .ReturnsAsync(user);

        _mockUserManager.Setup(m => m.GetRolesAsync(user))
            .ReturnsAsync(new List<string> { Roles.User, Roles.Admin });

        // Act
        var result = await _sut.GetCurrentUserAsync(userId);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(userId, result.UserId);
        Assert.Equal("user@manageu.com", result.Email);
        Assert.Equal("Active User", result.FullName);
        Assert.Contains(Roles.User, result.Roles);
        Assert.Contains(Roles.Admin, result.Roles);
    }

    [Fact]
    public async Task GetCurrentUserAsync_WithNonExistentOrInactiveUser_ReturnsNull()
    {
        // Arrange
        _mockUserManager.Setup(m => m.FindByIdAsync("missing"))
            .ReturnsAsync((ApplicationUser?)null);

        var inactiveUser = new ApplicationUser { Id = "inactive", IsActive = false };
        _mockUserManager.Setup(m => m.FindByIdAsync("inactive"))
            .ReturnsAsync(inactiveUser);

        // Act
        var missingResult = await _sut.GetCurrentUserAsync("missing");
        var inactiveResult = await _sut.GetCurrentUserAsync("inactive");

        // Assert
        Assert.Null(missingResult);
        Assert.Null(inactiveResult);
    }
}
