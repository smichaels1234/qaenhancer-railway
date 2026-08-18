using System.ComponentModel.DataAnnotations;

namespace backend.Models.Auth;

public class RegisterRequest
{
    [Required(ErrorMessage = "Full name is required")]
    public required string FullName { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
    public required string Password { get; set; }

    // Plan information
    [MaxLength(20)]
    public string PlanType { get; set; } = "free";

    // Custom plan contact information
    [MaxLength(200)]
    public string? CompanyName { get; set; }

    [MaxLength(20)]
    public string? PhoneNumber { get; set; }

    [MaxLength(50)]
    public string? TeamSize { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }

    [Required]
    public required string CaptchaToken { get; set; }
}

public class LoginRequest
{
    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email address")]
    public required string Email { get; set; }

    [Required(ErrorMessage = "Password is required")]
    public required string Password { get; set; }

    public bool RememberMe { get; set; } = false;
}

public class AuthResponse
{
    public required string AccessToken { get; set; }
    public required string RefreshToken { get; set; }
    public required string TokenType { get; set; } = "Bearer";
    public int ExpiresIn { get; set; }
    public required UserInfo User { get; set; }
}

public class UserInfo
{
    public required string Id { get; set; }
    public required string Email { get; set; }
    public required string FullName { get; set; }
    public required string OrganizationId { get; set; }
}

public class RefreshTokenRequest
{
    [Required]
    public required string RefreshToken { get; set; }
}

public class RevokeTokenRequest
{
    [Required]
    public required string RefreshToken { get; set; }
}
