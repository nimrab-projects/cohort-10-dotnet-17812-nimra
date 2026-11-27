using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ManageU.Application.Common.Interfaces;

namespace ManageU.Infrastructure.Authentication;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly JwtSettings _jwtSettings;

    public JwtTokenGenerator(IOptions<JwtSettings> jwtOptions)
    {
        ArgumentNullException.ThrowIfNull(jwtOptions);
        _jwtSettings = jwtOptions.Value;

        if (string.IsNullOrWhiteSpace(_jwtSettings.Secret))
        {
            throw new InvalidOperationException("JWT Secret is not configured.");
        }
    }

    public GeneratedTokenResult GenerateToken(string userId, string email, string fullName, IEnumerable<string> roles)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtSettings.Secret));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        // Standard RFC 7519 claims:
        // jti serves as a unique token identifier in this stateless implementation.
        // Stateless JWT authentication relies on cryptographic verification and lifetime expiration;
        // token blacklisting/revocation store is outside Module 2 scope.
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, userId),
            new(JwtRegisteredClaimNames.Email, email),
            new(ClaimTypes.Name, fullName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        if (roles != null)
        {
            foreach (var role in roles)
            {
                if (!string.IsNullOrWhiteSpace(role))
                {
                    claims.Add(new Claim(ClaimTypes.Role, role));
                }
            }
        }

        var expires = DateTime.UtcNow.AddMinutes(_jwtSettings.ExpiryMinutes);

        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = expires,
            Issuer = _jwtSettings.Issuer,
            Audience = _jwtSettings.Audience,
            SigningCredentials = credentials
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var token = tokenHandler.CreateToken(tokenDescriptor);
        var tokenString = tokenHandler.WriteToken(token);

        return new GeneratedTokenResult(tokenString, expires);
    }
}
