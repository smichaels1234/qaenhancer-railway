using backend.Data;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public sealed class PlanEntitlementService
{
    public const int FreeBugLimit = 10;

    private readonly QAEnhancerDbContext _context;

    public PlanEntitlementService(QAEnhancerDbContext context)
    {
        _context = context;
    }

    public async Task<PlanEntitlement> GetEntitlementAsync(string userId)
    {
        var subscription = await _context.Subscriptions
            .Where(item => item.UserId == userId && item.Status == "active")
            .OrderByDescending(item => item.UpdatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return PlanEntitlement.Inactive;
        }

        return subscription.PlanType.ToLowerInvariant() switch
        {
            "free" => new PlanEntitlement("free", FreeBugLimit),
            "pro" => new PlanEntitlement("pro", null),
            "custom" => new PlanEntitlement("custom", null),
            _ => PlanEntitlement.Inactive
        };
    }

    public async Task<bool> HasPaidPlanAsync(string userId)
    {
        var entitlement = await GetEntitlementAsync(userId);
        return entitlement.PlanType is "pro" or "custom";
    }
}

public sealed record PlanEntitlement(string PlanType, int? BugLimit)
{
    public static readonly PlanEntitlement Inactive = new("inactive", 0);

    public bool IsActive => PlanType != "inactive";
}