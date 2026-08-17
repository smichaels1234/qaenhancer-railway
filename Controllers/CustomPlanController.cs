using backend.Data;
using backend.Models;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CustomPlanController : ControllerBase
{
    private readonly QAEnhancerDbContext _context;
    private readonly ILogger<CustomPlanController> _logger;
    private readonly HashSet<string> _adminEmails;

    public CustomPlanController(QAEnhancerDbContext context, ILogger<CustomPlanController> logger, IConfiguration configuration)
    {
        _context = context;
        _logger = logger;
        var configuredEmails = configuration.GetSection("AdminSettings:AllowedEmails").Get<string[]>() ?? [];
        _adminEmails = new HashSet<string>(configuredEmails, StringComparer.OrdinalIgnoreCase);
    }

    // GET: api/customplan/my-requests
    [HttpGet("my-requests")]
    public async Task<ActionResult<IEnumerable<CustomPlanRequest>>> GetMyRequests()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var requests = await _context.CustomPlanRequests
            .Where(r => r.UserId == userId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return Ok(requests);
    }

    // GET: api/customplan/all (Admin only)
    [HttpGet("all")]
    public async Task<ActionResult<IEnumerable<CustomPlanRequestWithUser>>> GetAllRequests(
        [FromQuery] string? status = null)
    {
        if (!IsAdminUser())
        {
            return Forbid();
        }

        var query = _context.CustomPlanRequests
            .Include(r => r.User)
            .AsQueryable();

        if (!string.IsNullOrEmpty(status))
        {
            query = query.Where(r => r.Status == status);
        }

        var requests = await query
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new CustomPlanRequestWithUser
            {
                Id = r.Id,
                UserId = r.UserId,
                UserEmail = r.User!.Email ?? "",
                UserFullName = r.User.FullName ?? "",
                CompanyName = r.CompanyName,
                PhoneNumber = r.PhoneNumber,
                TeamSize = r.TeamSize,
                Message = r.Message,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
                ContactedAt = r.ContactedAt,
                ConvertedAt = r.ConvertedAt,
                SalesNotes = r.SalesNotes,
                AssignedToSalesRep = r.AssignedToSalesRep,
                UpdatedAt = r.UpdatedAt
            })
            .ToListAsync();

        return Ok(requests);
    }

    // PUT: api/customplan/{id}/update-status (Admin/Sales only)
    [HttpPut("{id}/update-status")]
    public async Task<IActionResult> UpdateRequestStatus(int id, [FromBody] UpdateStatusRequest request)
    {
        if (!IsAdminUser())
        {
            return Forbid();
        }

        var allowedStatuses = new[] { "pending", "contacted", "converted", "declined" };
        if (!allowedStatuses.Contains(request.Status.ToLower()))
        {
            return BadRequest(new { message = "Invalid status. Use pending, contacted, converted, or declined." });
        }

        var customPlanRequest = await _context.CustomPlanRequests.FindAsync(id);

        if (customPlanRequest == null)
        {
            return NotFound(new { message = "Custom plan request not found" });
        }

        customPlanRequest.Status = request.Status;
        customPlanRequest.SalesNotes = request.SalesNotes;
        customPlanRequest.AssignedToSalesRep = request.AssignedToSalesRep;
        customPlanRequest.UpdatedAt = DateTime.UtcNow;

        if (request.Status == "contacted" && customPlanRequest.ContactedAt == null)
        {
            customPlanRequest.ContactedAt = DateTime.UtcNow;
        }

        if (request.Status == "converted" && customPlanRequest.ConvertedAt == null)
        {
            customPlanRequest.ConvertedAt = DateTime.UtcNow;
        }

        var subscription = await _context.Subscriptions
            .Where(s => s.UserId == customPlanRequest.UserId && s.PlanType == "custom")
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription != null)
        {
            if (request.Status == "converted")
            {
                subscription.Status = "active";
                subscription.StartDate ??= DateTime.UtcNow;
                subscription.EndDate ??= DateTime.UtcNow.AddMonths(1);
                subscription.CancelledAt = null;
                subscription.AutoRenew = true;
                subscription.UpdatedAt = DateTime.UtcNow;
            }
            else if (request.Status == "declined")
            {
                subscription.Status = "cancelled";
                subscription.CancelledAt = DateTime.UtcNow;
                subscription.AutoRenew = false;
                subscription.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation("Custom plan request {Id} updated to status {Status}", id, request.Status);

        return Ok(customPlanRequest);
    }

    // GET: api/customplan/stats (Admin/Sales only)
    [HttpGet("stats")]
    public async Task<ActionResult<CustomPlanStats>> GetStats()
    {
        if (!IsAdminUser())
        {
            return Forbid();
        }

        var stats = new CustomPlanStats
        {
            TotalRequests = await _context.CustomPlanRequests.CountAsync(),
            PendingRequests = await _context.CustomPlanRequests.CountAsync(r => r.Status == "pending"),
            ContactedRequests = await _context.CustomPlanRequests.CountAsync(r => r.Status == "contacted"),
            ConvertedRequests = await _context.CustomPlanRequests.CountAsync(r => r.Status == "converted"),
            DeclinedRequests = await _context.CustomPlanRequests.CountAsync(r => r.Status == "declined"),
            AverageResponseTime = await CalculateAverageResponseTime()
        };

        return Ok(stats);
    }

    private async Task<double> CalculateAverageResponseTime()
    {
        var contactedRequests = await _context.CustomPlanRequests
            .Where(r => r.ContactedAt != null)
            .ToListAsync();

        if (!contactedRequests.Any())
        {
            return 0;
        }

        var totalHours = contactedRequests
            .Sum(r => (r.ContactedAt!.Value - r.CreatedAt).TotalHours);

        return totalHours / contactedRequests.Count;
    }

    private bool IsAdminUser()
    {
        var email = User.FindFirst(ClaimTypes.Email)?.Value
            ?? User.FindFirst(JwtRegisteredClaimNames.Email)?.Value
            ?? User.FindFirst("email")?.Value;

        return !string.IsNullOrWhiteSpace(email) && _adminEmails.Contains(email);
    }
}

public class CustomPlanRequestWithUser
{
    public int Id { get; set; }
    public string UserId { get; set; } = "";
    public string UserEmail { get; set; } = "";
    public string UserFullName { get; set; } = "";
    public string CompanyName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
    public string TeamSize { get; set; } = "";
    public string? Message { get; set; }
    public string Status { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public DateTime? ContactedAt { get; set; }
    public DateTime? ConvertedAt { get; set; }
    public string? SalesNotes { get; set; }
    public string? AssignedToSalesRep { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class UpdateStatusRequest
{
    public required string Status { get; set; }
    public string? SalesNotes { get; set; }
    public string? AssignedToSalesRep { get; set; }
}

public class CustomPlanStats
{
    public int TotalRequests { get; set; }
    public int PendingRequests { get; set; }
    public int ContactedRequests { get; set; }
    public int ConvertedRequests { get; set; }
    public int DeclinedRequests { get; set; }
    public double AverageResponseTime { get; set; }
}
