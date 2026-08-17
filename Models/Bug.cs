using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

[Table("Bugs")]
public class Bug
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(500)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "Open"; // Open, In Progress, Resolved, Closed

    [MaxLength(20)]
    public string? Severity { get; set; } // Low, Medium, High, Critical

    [MaxLength(500)]
    public string? Location { get; set; }

    [MaxLength(1000)]
    public string? AnalyzedUrl { get; set; }

    [MaxLength(50)]
    public string? Source { get; set; } = "Manual"; // Manual, AI

    public DateTime? DueDate { get; set; }

    [Required]
    [MaxLength(100)]
    public string OrganizationId { get; set; } = string.Empty;

    public string? UserId { get; set; }

    [ForeignKey(nameof(UserId))]
    public ApplicationUser? User { get; set; }

    public string? AssignedUserId { get; set; }

    [ForeignKey(nameof(AssignedUserId))]
    public ApplicationUser? AssignedUser { get; set; }

    [MaxLength(200)]
    public string? AssignedUserName { get; set; }

    [MaxLength(256)]
    public string? AssignedUserEmail { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsActive { get; set; } = true;
}