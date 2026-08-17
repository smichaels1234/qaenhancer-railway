using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using backend.Data;
using backend.Models;
using backend.Services;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BugsController : ControllerBase
{
    private readonly QAEnhancerDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<BugsController> _logger;
    private readonly PlanEntitlementService _planEntitlementService;

    public BugsController(
        QAEnhancerDbContext context,
        UserManager<ApplicationUser> userManager,
        ILogger<BugsController> logger,
        PlanEntitlementService planEntitlementService)
    {
        _context = context;
        _userManager = userManager;
        _logger = logger;
        _planEntitlementService = planEntitlementService;
    }

    // GET: api/bugs
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Bug>>> GetBugs([FromQuery] string? url = null)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var query = _context.Bugs.Where(b => b.IsActive && b.UserId == currentUser.Id && b.OrganizationId == currentUser.OrganizationId);

            if (!string.IsNullOrEmpty(url))
            {
                query = query.Where(b => b.AnalyzedUrl == url);
            }

            var bugs = await query
                .OrderByDescending(b => b.CreatedAt)
                .ToListAsync();

            return Ok(bugs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bugs");
            return StatusCode(500, "Internal server error");
        }
    }

    // GET: api/bugs/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Bug>> GetBug(Guid id)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var bug = await _context.Bugs
                .FirstOrDefaultAsync(b => b.Id == id && b.IsActive && b.UserId == currentUser.Id && b.OrganizationId == currentUser.OrganizationId);

            if (bug == null)
            {
                return NotFound();
            }

            return Ok(bug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving bug {BugId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // POST: api/bugs
    [HttpPost]
    public async Task<ActionResult<Bug>> CreateBug(CreateBugRequest request)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var planCheck = await CanCreateBugsAsync(currentUser, 1);
            if (planCheck != null)
            {
                return planCheck;
            }

            var bug = new Bug
            {
                UserId = currentUser.Id,
                OrganizationId = currentUser.OrganizationId,
                Title = request.Title,
                Description = request.Description,
                Status = request.Status ?? "Open",
                Severity = request.Severity,
                Location = request.Location,
                AnalyzedUrl = request.AnalyzedUrl,
                Source = request.Source ?? "Manual",
                DueDate = request.DueDate
            };

            if (!string.IsNullOrWhiteSpace(request.AssignedUserId))
            {
                var assignee = await _userManager.FindByIdAsync(request.AssignedUserId);
                if (assignee == null)
                {
                    return BadRequest(new { message = "Assigned user not found" });
                }

                if (!string.Equals(assignee.OrganizationId, currentUser.OrganizationId, StringComparison.Ordinal))
                {
                    return BadRequest(new { message = "Assigned user must belong to your organization" });
                }

                bug.AssignedUserId = assignee.Id;
                bug.AssignedUserName = assignee.FullName;
                bug.AssignedUserEmail = assignee.Email;
            }

            _context.Bugs.Add(bug);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created bug {BugId} for URL {Url}", bug.Id, bug.AnalyzedUrl);

            return CreatedAtAction(nameof(GetBug), new { id = bug.Id }, bug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bug");
            return StatusCode(500, "Internal server error");
        }
    }

    // PUT: api/bugs/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateBug(Guid id, UpdateBugRequest request)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var bug = await _context.Bugs
                .FirstOrDefaultAsync(b => b.Id == id && b.IsActive && b.UserId == currentUser.Id && b.OrganizationId == currentUser.OrganizationId);

            if (bug == null)
            {
                return NotFound();
            }

            // Update only provided fields
            if (!string.IsNullOrEmpty(request.Title))
                bug.Title = request.Title;
            
            if (!string.IsNullOrEmpty(request.Description))
                bug.Description = request.Description;
            
            if (!string.IsNullOrEmpty(request.Status))
                bug.Status = request.Status;
            
            if (!string.IsNullOrEmpty(request.Severity))
                bug.Severity = request.Severity;
            
            if (!string.IsNullOrEmpty(request.Location))
                bug.Location = request.Location;

            if (request.ClearDueDate)
                bug.DueDate = null;
            else if (request.DueDate.HasValue)
                bug.DueDate = request.DueDate;

            if (request.AssignedUserId != null)
            {
                if (string.IsNullOrWhiteSpace(request.AssignedUserId))
                {
                    bug.AssignedUserId = null;
                    bug.AssignedUserName = null;
                    bug.AssignedUserEmail = null;
                }
                else
                {
                    var assignee = await _userManager.FindByIdAsync(request.AssignedUserId);
                    if (assignee == null)
                    {
                        return BadRequest(new { message = "Assigned user not found" });
                    }

                    if (!string.Equals(assignee.OrganizationId, currentUser.OrganizationId, StringComparison.Ordinal))
                    {
                        return BadRequest(new { message = "Assigned user must belong to your organization" });
                    }

                    bug.AssignedUserId = assignee.Id;
                    bug.AssignedUserName = assignee.FullName;
                    bug.AssignedUserEmail = assignee.Email;
                }
            }

            bug.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Updated bug {BugId}", bug.Id);

            return Ok(bug);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating bug {BugId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // DELETE: api/bugs/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteBug(Guid id)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var bug = await _context.Bugs
                .FirstOrDefaultAsync(b => b.Id == id && b.IsActive && b.UserId == currentUser.Id && b.OrganizationId == currentUser.OrganizationId);

            if (bug == null)
            {
                return NotFound();
            }

            _context.Bugs.Remove(bug);

            await _context.SaveChangesAsync();

            _logger.LogInformation("Deleted bug {BugId}", bug.Id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting bug {BugId}", id);
            return StatusCode(500, "Internal server error");
        }
    }

    // POST: api/bugs/batch
    [HttpPost("batch")]
    public async Task<ActionResult<IEnumerable<Bug>>> CreateBugsBatch(CreateBugsBatchRequest request)
    {
        try
        {
            var currentUser = await GetCurrentUserAsync();
            if (currentUser == null)
            {
                return Unauthorized();
            }

            var planCheck = await CanCreateBugsAsync(currentUser, request.Bugs.Count);
            if (planCheck != null)
            {
                return planCheck;
            }

            var bugs = request.Bugs.Select(bugData => new Bug
            {
                UserId = currentUser.Id,
                OrganizationId = currentUser.OrganizationId,
                Title = bugData.Title,
                Description = bugData.Description,
                Status = bugData.Status ?? "Open",
                Severity = bugData.Severity,
                Location = bugData.Location,
                AnalyzedUrl = request.AnalyzedUrl,
                Source = request.Source ?? "AI",
                DueDate = bugData.DueDate
            }).ToList();

            _context.Bugs.AddRange(bugs);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Created {Count} bugs for URL {Url}", bugs.Count, request.AnalyzedUrl);

            return Ok(bugs);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating bug batch");
            return StatusCode(500, "Internal server error");
        }
    }

    private string? GetCurrentUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = GetCurrentUserId();
        if (string.IsNullOrWhiteSpace(userId))
        {
            return null;
        }

        return await _userManager.FindByIdAsync(userId);
    }

    private async Task<ActionResult?> CanCreateBugsAsync(ApplicationUser currentUser, int requestedBugCount)
    {
        var entitlement = await _planEntitlementService.GetEntitlementAsync(currentUser.Id);
        if (!entitlement.IsActive)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Your selected plan is awaiting activation. Complete Pro checkout or wait for Custom plan approval."
            });
        }

        if (entitlement.BugLimit is not int bugLimit)
        {
            return null;
        }

        var activeBugCount = await _context.Bugs.CountAsync(bug =>
            bug.IsActive &&
            bug.UserId == currentUser.Id &&
            bug.OrganizationId == currentUser.OrganizationId);

        if (activeBugCount + requestedBugCount <= bugLimit)
        {
            return null;
        }

        return StatusCode(StatusCodes.Status403Forbidden, new
        {
            message = $"The Free plan allows up to {bugLimit} tracked bugs. Upgrade to Pro or Custom to add more.",
            limit = bugLimit,
            currentUsage = activeBugCount
        });
    }
}

// Request/Response DTOs
public class CreateBugRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? Location { get; set; }
    public string? AnalyzedUrl { get; set; }
    public string? Source { get; set; }
    public DateTime? DueDate { get; set; }
    public string? AssignedUserId { get; set; }
}

public class UpdateBugRequest
{
    public string? Title { get; set; }
    public string? Description { get; set; }
    public string? Status { get; set; }
    public string? Severity { get; set; }
    public string? Location { get; set; }
    public DateTime? DueDate { get; set; }
    public bool ClearDueDate { get; set; }
    public string? AssignedUserId { get; set; }
}

public class CreateBugsBatchRequest
{
    public string? AnalyzedUrl { get; set; }
    public string? Source { get; set; }
    public List<CreateBugRequest> Bugs { get; set; } = new();
}