using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ManageU.Core.Constants;
using ManageU.Infrastructure.Authentication;
using Xunit;

namespace ManageU.Tests;

public class JwtTokenGeneratorTests
{
    private const string TestSecret = "SuperSecureTestSecretKeyForTestingPurposesOnly1234567890!";
    private const string TestIssuer = "ManageUTestApi";
    private const string TestAudience = "ManageUTestClient";

    private readonly JwtSettings _jwtSettings;
    private readonly JwtTokenGenerator _sut;

    public JwtTokenGeneratorTests()
    {
        _jwtSettings = new JwtSettings
        {
            Secret = TestSecret,
            Issuer = TestIssuer,
            Audience = TestAudience,
            ExpiryMinutes = 60
        };

        var options = Options.Create(_jwtSettings);
        _sut = new JwtTokenGenerator(options);
    }

    [Fact]
    public void GenerateToken_WithValidParameters_ReturnsValidSignedJwtAndExactExpiry()
    {
        // Arrange
        var userId = "user-test-id-123";
        var email = "user@manageu.com";
        var fullName = "Jane Doe";
        var roles = new[] { Roles.User };

        // Act
        var tokenResult = _sut.GenerateToken(userId, email, fullName, roles);

        // Assert
        Assert.NotNull(tokenResult);
        Assert.NotEmpty(tokenResult.Token);
        Assert.True(tokenResult.ExpiresAt > DateTime.UtcNow);

        // Validate token signature and claims via token handler
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret)),
            ValidateIssuer = true,
            ValidIssuer = TestIssuer,
            ValidateAudience = true,
            ValidAudience = TestAudience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        var principal = tokenHandler.ValidateToken(tokenResult.Token, validationParameters, out var validatedToken);

        Assert.NotNull(validatedToken);
        var jwtSecurityToken = Assert.IsType<JwtSecurityToken>(validatedToken);

        // Verify validated ClaimsPrincipal mappings
        Assert.Equal(userId, principal.FindFirst(ClaimTypes.NameIdentifier)?.Value);
        Assert.Equal(email, principal.FindFirst(ClaimTypes.Email)?.Value);
        Assert.Equal(fullName, principal.FindFirst(ClaimTypes.Name)?.Value);
        Assert.Equal(Roles.User, principal.FindFirst(ClaimTypes.Role)?.Value);

        // Verify token exp matches tokenResult.ExpiresAt within 1 second precision
        var expectedEpoch = new DateTimeOffset(tokenResult.ExpiresAt).ToUnixTimeSeconds();
        var actualEpoch = new DateTimeOffset(jwtSecurityToken.ValidTo).ToUnixTimeSeconds();
        Assert.Equal(expectedEpoch, actualEpoch);

        // Verify no duplicate claim keys in raw payload (sub, email, unique_name, jti only once)
        var rawClaims = jwtSecurityToken.Claims.ToList();
        Assert.Single(rawClaims, c => c.Type == JwtRegisteredClaimNames.Sub);
        Assert.Single(rawClaims, c => c.Type == JwtRegisteredClaimNames.Email);
        Assert.Single(rawClaims, c => c.Type == "unique_name");
        Assert.Single(rawClaims, c => c.Type == JwtRegisteredClaimNames.Jti);
    }

    [Fact]
    public void GenerateToken_WithNonDefaultExpiry_AccuratelySetsTokenAndResultExpiry()
    {
        // Arrange: custom 45-minute expiry
        var customSettings = new JwtSettings
        {
            Secret = TestSecret,
            Issuer = TestIssuer,
            Audience = TestAudience,
            ExpiryMinutes = 45
        };
        var generator = new JwtTokenGenerator(Options.Create(customSettings));

        var beforeCall = DateTime.UtcNow;

        // Act
        var tokenResult = generator.GenerateToken("user-45", "user45@manageu.com", "User 45", new[] { Roles.User });

        var afterCall = DateTime.UtcNow;

        // Assert: Expiration must be precisely 45 minutes from generation
        var expectedMin = beforeCall.AddMinutes(45);
        var expectedMax = afterCall.AddMinutes(45);
        Assert.InRange(tokenResult.ExpiresAt, expectedMin, expectedMax);

        // Verify token ValidTo matches result.ExpiresAt
        var tokenHandler = new JwtSecurityTokenHandler();
        var jwtToken = tokenHandler.ReadJwtToken(tokenResult.Token);
        var expectedEpoch = new DateTimeOffset(tokenResult.ExpiresAt).ToUnixTimeSeconds();
        var actualEpoch = new DateTimeOffset(jwtToken.ValidTo).ToUnixTimeSeconds();
        Assert.Equal(expectedEpoch, actualEpoch);
    }

    [Fact]
    public void GenerateToken_WithMultipleRoles_IncludesAllRoleClaimsInValidatedPrincipal()
    {
        // Arrange
        var userId = "admin-test-id-456";
        var email = "admin@manageu.com";
        var fullName = "Admin User";
        var roles = new[] { Roles.Admin, Roles.User };

        // Act
        var tokenResult = _sut.GenerateToken(userId, email, fullName, roles);

        // Assert
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(TestSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false
        };

        var principal = tokenHandler.ValidateToken(tokenResult.Token, validationParameters, out _);
        var roleClaims = principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ToList();

        Assert.Contains(Roles.Admin, roleClaims);
        Assert.Contains(Roles.User, roleClaims);
        Assert.Equal(2, roleClaims.Count);
        Assert.True(principal.IsInRole(Roles.Admin));
        Assert.True(principal.IsInRole(Roles.User));
    }

    [Fact]
    public void Constructor_WithMissingSecret_ThrowsInvalidOperationException()
    {
        // Arrange
        var invalidSettings = new JwtSettings
        {
            Secret = "",
            Issuer = TestIssuer,
            Audience = TestAudience,
            ExpiryMinutes = 60
        };

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => new JwtTokenGenerator(Options.Create(invalidSettings)));
    }

    [Theory]
    [InlineData("", "test@manageu.com")]
    [InlineData("   ", "test@manageu.com")]
    [InlineData("user-1", "")]
    [InlineData("user-1", "   ")]
    public void GenerateToken_WithInvalidUserIdOrEmail_ThrowsArgumentException(string userId, string email)
    {
        // Act & Assert
        Assert.Throws<ArgumentException>(() => _sut.GenerateToken(userId, email, "Name", new[] { Roles.User }));
    }
}
