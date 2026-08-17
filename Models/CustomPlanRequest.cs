using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public class CustomPlanRequest
{
    [Key]
    public int Id { get; set; }

    [Required]
    public required string UserId { get; set; }

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(200)]
    public required string CompanyName { get; set; }

    [Required]
    [MaxLength(20)]
    public required string PhoneNumber { get; set; }

    [Required]
    [MaxLength(50)]
    public required string TeamSize { get; set; }

    [MaxLength(2000)]
    public string? Message { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // "pending", "contacted", "converted", "declined"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? ContactedAt { get; set; }

    public DateTime? ConvertedAt { get; set; }

    [MaxLength(500)]
    public string? SalesNotes { get; set; }

    [MaxLength(200)]
    public string? AssignedToSalesRep { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
