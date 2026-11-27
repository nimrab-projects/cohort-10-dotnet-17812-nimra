namespace ManageU.Application.Common.Interfaces;

public record GeneratedTokenResult(string Token, DateTime ExpiresAt);

public interface IJwtTokenGenerator
{
    GeneratedTokenResult GenerateToken(string userId, string email, string fullName, IEnumerable<string> roles);
}
