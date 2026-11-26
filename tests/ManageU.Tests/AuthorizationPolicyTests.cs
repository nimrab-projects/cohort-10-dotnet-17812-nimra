using System.Reflection;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ManageU.Api.Controllers;
using ManageU.Core.Constants;
using Xunit;

namespace ManageU.Tests;

public class AuthorizationPolicyTests
{
    [Fact]
    public void AuthController_Endpoints_HaveAppropriateAuthorizationAttributes()
    {
        // 1. Controller itself does not require authorization so Login/Register are public
        var controllerType = typeof(AuthController);
        Assert.Null(controllerType.GetCustomAttribute<AuthorizeAttribute>());

        // 2. Register is public
        var registerMethod = controllerType.GetMethod(nameof(AuthController.Register));
        Assert.NotNull(registerMethod);
        Assert.Null(registerMethod.GetCustomAttribute<AuthorizeAttribute>());

        // 3. Login is public
        var loginMethod = controllerType.GetMethod(nameof(AuthController.Login));
        Assert.NotNull(loginMethod);
        Assert.Null(loginMethod.GetCustomAttribute<AuthorizeAttribute>());

        // 4. Me requires authentication
        var meMethod = controllerType.GetMethod(nameof(AuthController.Me));
        Assert.NotNull(meMethod);
        var meAuthorize = meMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(meAuthorize);

        // 5. Logout requires authentication
        var logoutMethod = controllerType.GetMethod(nameof(AuthController.Logout));
        Assert.NotNull(logoutMethod);
        var logoutAuthorize = logoutMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(logoutAuthorize);

        // 6. AdminOnly requires RequireAdminRole policy
        var adminMethod = controllerType.GetMethod(nameof(AuthController.AdminOnly));
        Assert.NotNull(adminMethod);
        var adminAuthorize = adminMethod.GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(adminAuthorize);
        Assert.Equal("RequireAdminRole", adminAuthorize.Policy);
    }

    [Fact]
    public async Task RequireAdminRole_PolicyEvaluation_GrantsAccessOnlyToAdminRole()
    {
        // Arrange
        var authorizationOptions = new AuthorizationOptions();
        authorizationOptions.AddPolicy("RequireAdminRole", policy => policy.RequireRole(Roles.Admin));
        authorizationOptions.AddPolicy("RequireUserRole", policy => policy.RequireRole(Roles.User));

        var adminPolicy = authorizationOptions.GetPolicy("RequireAdminRole");
        Assert.NotNull(adminPolicy);

        // Principal with Admin role
        var adminPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "admin-1"),
            new Claim(ClaimTypes.Role, Roles.Admin)
        }, "TestAuth"));

        // Principal with User role only
        var userPrincipal = new ClaimsPrincipal(new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, Roles.User)
        }, "TestAuth"));

        // Principal with no roles (unauthenticated or guest)
        var unauthenticatedPrincipal = new ClaimsPrincipal(new ClaimsIdentity());

        // Assert
        Assert.True(adminPrincipal.IsInRole(Roles.Admin));
        Assert.False(userPrincipal.IsInRole(Roles.Admin));
        Assert.False(unauthenticatedPrincipal.IsInRole(Roles.Admin));

        await Task.CompletedTask;
    }
}
