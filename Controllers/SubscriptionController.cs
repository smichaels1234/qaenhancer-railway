using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SubscriptionController : ControllerBase
{
    private readonly QAEnhancerDbContext _context;
    private readonly ILogger<SubscriptionController> _logger;

    public SubscriptionController(QAEnhancerDbContext context, ILogger<SubscriptionController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/subscription/current
    [HttpGet("current")]
    public async Task<ActionResult<Subscription>> GetCurrentSubscription()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var subscription = await _context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return NotFound(new { message = "No active subscription found" });
        }

        return Ok(subscription);
    }

    // GET: api/subscription/history
    [HttpGet("history")]
    public async Task<ActionResult<IEnumerable<Subscription>>> GetSubscriptionHistory()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var subscriptions = await _context.Subscriptions
            .Where(s => s.UserId == userId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return Ok(subscriptions);
    }

    // POST: api/subscription/upgrade
    [HttpPost("upgrade")]
    public async Task<ActionResult<Subscription>> UpgradeSubscription([FromBody] UpgradeRequest request)
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var requestedPlan = request.PlanType.Trim().ToLowerInvariant();
        if (requestedPlan is not ("free" or "pro" or "custom"))
        {
            return BadRequest(new { message = "Invalid plan type." });
        }

        if (requestedPlan != "free")
        {
            return BadRequest(new
            {
                message = "Pro activation requires Stripe checkout. Custom activation requires an approved sales request."
            });
        }

        var currentSubscription = await _context.Subscriptions
            .Where(s => s.UserId == userId && s.Status == "active")
            .FirstOrDefaultAsync();

        if (currentSubscription != null)
        {
            currentSubscription.Status = "cancelled";
            currentSubscription.CancelledAt = DateTime.UtcNow;
            currentSubscription.UpdatedAt = DateTime.UtcNow;
        }

        var newSubscription = new Subscription
        {
            UserId = userId,
            PlanType = requestedPlan,
            Status = "active",
            StartDate = DateTime.UtcNow,
            EndDate = null,
            MonthlyPrice = 0m
        };

        _context.Subscriptions.Add(newSubscription);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Subscription upgraded for user {UserId} to {PlanType}", userId, request.PlanType);

        return Ok(newSubscription);
    }

    // POST: api/subscription/cancel
    [HttpPost("cancel")]
    public async Task<IActionResult> CancelSubscription()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var subscription = await _context.Subscriptions
            .Where(s => s.UserId == userId && s.Status == "active")
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return NotFound(new { message = "No active subscription found" });
        }

        subscription.Status = "cancelled";
        subscription.CancelledAt = DateTime.UtcNow;
        subscription.AutoRenew = false;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        _logger.LogInformation("Subscription cancelled for user {UserId}", userId);

        return Ok(new { message = "Subscription cancelled successfully" });
    }
}

public class UpgradeRequest
{
    public required string PlanType { get; set; }
}
