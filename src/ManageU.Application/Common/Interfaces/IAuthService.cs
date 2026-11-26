using ManageU.Application.DTOs.Auth;

namespace ManageU.Application.Common.Interfaces;

public interface IAuthService
{
    Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default);
    Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default);
    Task<CurrentUserDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default);
}
