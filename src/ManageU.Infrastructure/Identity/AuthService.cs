using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using ManageU.Application.Common.Exceptions;
using ManageU.Application.Common.Interfaces;
using ManageU.Application.DTOs.Auth;
using ManageU.Core.Constants;
using ManageU.Infrastructure.Identity;

namespace ManageU.Infrastructure.Identity;

public class AuthService : IAuthService
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        IJwtTokenGenerator jwtTokenGenerator,
        ILogger<AuthService> logger)
    {
        _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
        _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
        _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
        _jwtTokenGenerator = jwtTokenGenerator ?? throw new ArgumentNullException(nameof(jwtTokenGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AuthResponseDto> RegisterAsync(RegisterRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trimmedEmail = request.Email.Trim().ToLowerInvariant();
        var trimmedFullName = request.FullName.Trim();

        // Check if user already exists
        var existingUser = await _userManager.FindByEmailAsync(trimmedEmail);
        if (existingUser != null)
        {
            _logger.LogWarning("Registration failed: User with email {Email} already exists", trimmedEmail);
            throw new ConflictException($"A user with email '{trimmedEmail}' already exists.");
        }

        var user = new ApplicationUser
        {
            UserName = trimmedEmail,
            Email = trimmedEmail,
            FullName = trimmedFullName,
            CreatedAt = DateTime.UtcNow,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description);
            _logger.LogWarning("Registration failed for {Email}: {Errors}", trimmedEmail, string.Join(", ", errors));
            throw new ValidationException(errors);
        }

        // Ensure User role exists and assign default User role
        if (!await _roleManager.RoleExistsAsync(Roles.User))
        {
            await _roleManager.CreateAsync(new IdentityRole(Roles.User));
        }

        await _userManager.AddToRoleAsync(user, Roles.User);
        _logger.LogInformation("User {Email} registered successfully with role {Role}", trimmedEmail, Roles.User);

        var roles = new List<string> { Roles.User };
        var tokenResult = _jwtTokenGenerator.GenerateToken(user.Id, user.Email!, user.FullName, roles);

        return new AuthResponseDto
        {
            Token = tokenResult.Token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Roles = roles,
            ExpiresAt = tokenResult.ExpiresAt
        };
    }

    public async Task<AuthResponseDto> LoginAsync(LoginRequestDto request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trimmedEmail = request.Email.Trim().ToLowerInvariant();

        var user = await _userManager.FindByEmailAsync(trimmedEmail);
        if (user == null || !user.IsActive)
        {
            _logger.LogWarning("Login failed for {Email}: User not found or inactive", trimmedEmail);
            throw new UnauthorizedException("Invalid email or password.");
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (result.IsLockedOut)
        {
            _logger.LogWarning("Login failed for {Email}: Account locked out", trimmedEmail);
            throw new UnauthorizedException("User account is locked out.");
        }

        if (!result.Succeeded)
        {
            _logger.LogWarning("Login failed for {Email}: Invalid credentials", trimmedEmail);
            throw new UnauthorizedException("Invalid email or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var tokenResult = _jwtTokenGenerator.GenerateToken(user.Id, user.Email!, user.FullName, roles);

        _logger.LogInformation("User {Email} logged in successfully", trimmedEmail);

        return new AuthResponseDto
        {
            Token = tokenResult.Token,
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email!,
            Roles = roles.ToList(),
            ExpiresAt = tokenResult.ExpiresAt
        };
    }

    public async Task<CurrentUserDto?> GetCurrentUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(userId);

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !user.IsActive)
        {
            return null;
        }

        var roles = await _userManager.GetRolesAsync(user);

        return new CurrentUserDto
        {
            UserId = user.Id,
            FullName = user.FullName,
            Email = user.Email ?? string.Empty,
            Roles = roles.ToList()
        };
    }
}
