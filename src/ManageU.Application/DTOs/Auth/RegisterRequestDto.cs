using System.ComponentModel.DataAnnotations;

namespace ManageU.Application.DTOs.Auth;

public class RegisterRequestDto
{
    [Required(ErrorMessage = "Full name is required.")]
    [StringLength(100, ErrorMessage = "Full name cannot exceed 100 characters.")]
    public string FullName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Invalid email address format.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Password is required.")]
    [MinLength(12, ErrorMessage = "Password must be at least 12 characters long.")]
    public string Password { get; set; } = string.Empty;

    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    public string? ConfirmPassword { get; set; }
}
