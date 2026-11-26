using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;
using ManageU.Api.Controllers;
using ManageU.Application.Common.Interfaces;
using ManageU.Application.DTOs.Auth;
using ManageU.Core.Constants;
using Xunit;

namespace ManageU.Tests;

public class AuthControllerTests
{
    private readonly Mock<IAuthService> _mockAuthService;
    private readonly Mock<ILogger<AuthController>> _mockLogger;
    private readonly AuthController _sut;

    public AuthControllerTests()
    {
        _mockAuthService = new Mock<IAuthService>();
        _mockLogger = new Mock<ILogger<AuthController>>();
        _sut = new AuthController(_mockAuthService.Object, _mockLogger.Object);
    }

    [Fact]
    public async Task Register_WithValidData_ReturnsOkWithAuthResponse()
    {
        // Arrange
        var request = new RegisterRequestDto
        {
            FullName = "New User",
            Email = "new@manageu.com",
            Password = "Password123!"
        };

        var expectedResponse = new AuthResponseDto
        {
            Token = "token-123",
            UserId = "user-1",
            FullName = request.FullName,
            Email = request.Email,
            Roles = new List<string> { Roles.User },
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        };

        _mockAuthService.Setup(s => s.RegisterAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.Register(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.Equal("token-123", response.Token);
        Assert.Equal(request.Email, response.Email);
    }

    [Fact]
    public async Task Register_WithInvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new RegisterRequestDto();
        _sut.ModelState.AddModelError("Email", "Email is required");

        // Act
        var result = await _sut.Register(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockAuthService.Verify(s => s.RegisterAsync(It.IsAny<RegisterRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Login_WithValidData_ReturnsOkWithAuthResponse()
    {
        // Arrange
        var request = new LoginRequestDto
        {
            Email = "login@manageu.com",
            Password = "Password123!"
        };

        var expectedResponse = new AuthResponseDto
        {
            Token = "token-login",
            UserId = "user-login",
            FullName = "Login User",
            Email = request.Email,
            Roles = new List<string> { Roles.User },
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        };

        _mockAuthService.Setup(s => s.LoginAsync(request, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedResponse);

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var response = Assert.IsType<AuthResponseDto>(okResult.Value);
        Assert.Equal("token-login", response.Token);
    }

    [Fact]
    public async Task Login_WithInvalidModelState_ReturnsBadRequest()
    {
        // Arrange
        var request = new LoginRequestDto();
        _sut.ModelState.AddModelError("Password", "Password is required");

        // Act
        var result = await _sut.Login(request, CancellationToken.None);

        // Assert
        Assert.IsType<BadRequestObjectResult>(result);
        _mockAuthService.Verify(s => s.LoginAsync(It.IsAny<LoginRequestDto>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Me_WhenAuthenticated_ReturnsCurrentUserDto()
    {
        // Arrange
        var userId = "user-456";
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Email, "me@manageu.com")
        }, "TestAuth"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        var expectedUser = new CurrentUserDto
        {
            UserId = userId,
            FullName = "Current User",
            Email = "me@manageu.com",
            Roles = new List<string> { Roles.User }
        };

        _mockAuthService.Setup(s => s.GetCurrentUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expectedUser);

        // Act
        var result = await _sut.Me(CancellationToken.None);

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        var user = Assert.IsType<CurrentUserDto>(okResult.Value);
        Assert.Equal(userId, user.UserId);
        Assert.Equal("me@manageu.com", user.Email);
    }

    [Fact]
    public async Task Me_WhenUserClaimMissing_ReturnsUnauthorized()
    {
        // Arrange
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
        };

        // Act
        var result = await _sut.Me(CancellationToken.None);

        // Assert
        Assert.IsType<UnauthorizedResult>(result);
    }

    [Fact]
    public async Task Me_WhenUserNotFoundInDatabase_ReturnsNotFound()
    {
        // Arrange
        var userId = "deleted-user";
        var userClaims = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, userId)
        }, "TestAuth"));

        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = userClaims }
        };

        _mockAuthService.Setup(s => s.GetCurrentUserAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((CurrentUserDto?)null);

        // Act
        var result = await _sut.Me(CancellationToken.None);

        // Assert
        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public void Logout_ReturnsOkWithMessage()
    {
        // Act
        var result = _sut.Logout();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }

    [Fact]
    public void AdminOnly_ReturnsOkMessage()
    {
        // Act
        var result = _sut.AdminOnly();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result);
        Assert.NotNull(okResult.Value);
    }
}
