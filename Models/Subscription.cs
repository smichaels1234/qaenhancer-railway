using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public class Subscription
{
    [Key]
    public int Id { get; set; }

    [Required]
    public required string UserId { get; set; }

    [ForeignKey("UserId")]
    public ApplicationUser? User { get; set; }

    [Required]
    [MaxLength(20)]
    public required string PlanType { get; set; } // "free", "pro", "custom"

    [MaxLength(20)]
    public string? Status { get; set; } = "active"; // "active", "cancelled", "expired", "pending"

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? StartDate { get; set; }

    public DateTime? EndDate { get; set; }

    public DateTime? CancelledAt { get; set; }

    // Payment related (encrypted)
    [MaxLength(200)]
    public string? PaymentProcessorCustomerId { get; set; } // Stripe/PayPal customer ID

    [MaxLength(100)]
    public string? LastFourDigits { get; set; } // Last 4 digits of card for display

    public DateTime? NextBillingDate { get; set; }

    public decimal? MonthlyPrice { get; set; }

    public bool AutoRenew { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
