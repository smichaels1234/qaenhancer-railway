using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace backend.Models;

public class ApplicationRoi
{
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [MaxLength(100)]
    public string OrganizationId { get; set; } = string.Empty;

    [Required]
    [MaxLength(500)]
    public string ApplicationName { get; set; } = string.Empty;

    public decimal ActualCost { get; set; }

    public decimal RealizedRevenue { get; set; }

    public decimal HoursAvoided { get; set; }

    public decimal LaborRate { get; set; }

    public decimal DowntimeAvoided { get; set; }

    public decimal IncidentCost { get; set; }

    [NotMapped]
    public decimal CostSavings => (HoursAvoided * LaborRate) + DowntimeAvoided + IncidentCost;

    public string? UpdatedByUserId { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}