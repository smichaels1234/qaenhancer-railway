using System.ComponentModel.DataAnnotations;

namespace backend.Models;

public class UserSession
{
    public Guid Id { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    public ApplicationUser User { get; set; } = null!;

    [Required]
    public string RefreshTokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? RevokedAt { get; set; }

    [MaxLength(100)]
    public string? IpAddress { get; set; }

    [MaxLength(1000)]
    public string? UserAgent { get; set; }
}