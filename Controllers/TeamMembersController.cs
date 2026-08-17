using System.ComponentModel.DataAnnotations;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class TeamMembersController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<TeamMembersController> _logger;
    private readonly PlanEntitlementService _planEntitlementService;

    public TeamMembersController(
        UserManager<ApplicationUser> userManager,
        ILogger<TeamMembersController> logger,
        PlanEntitlementService planEntitlementService)
    {
        _userManager = userManager;
        _logger = logger;
        _planEntitlementService = planEntitlementService;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<TeamMemberResponse>>> GetTeamMembers()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (!await _planEntitlementService.HasPaidPlanAsync(currentUser.Id))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Team collaboration is available with an active Pro or Custom plan."
            });
        }

        var users = await _userManager.Users
            .Where(u => u.OrganizationId == currentUser.OrganizationId)
            .OrderBy(u => u.Email)
            .Select(u => new TeamMemberResponse
            {
                Id = u.Id,
                Email = u.Email ?? string.Empty,
                FullName = u.FullName,
                CreatedAt = u.CreatedAt
            })
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<ActionResult<CreateTeamMemberResponse>> CreateTeamMember([FromBody] CreateTeamMemberRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (!await _planEntitlementService.HasPaidPlanAsync(currentUser.Id))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new
            {
                message = "Team collaboration is available with an active Pro or Custom plan."
            });
        }

        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var existingUser = await _userManager.FindByEmailAsync(normalizedEmail);
        if (existingUser != null)
        {
            return BadRequest(new { message = "User with this email already exists" });
        }

        var password = string.IsNullOrWhiteSpace(request.Password)
            ? GenerateTemporaryPassword()
            : request.Password;

        var user = new ApplicationUser
        {
            UserName = normalizedEmail,
            Email = normalizedEmail,
            FullName = request.FullName?.Trim(),
            EmailConfirmed = true,
            OrganizationId = currentUser.OrganizationId,
            CreatedAt = DateTime.UtcNow
        };

        var result = await _userManager.CreateAsync(user, password);
        if (!result.Succeeded)
        {
            var errors = result.Errors.Select(e => e.Description).ToList();
            _logger.LogWarning("Failed to create team member {Email}. Errors: {Errors}", normalizedEmail, string.Join(", ", errors));
            return BadRequest(new { message = "Failed to create team member", errors });
        }

        _logger.LogInformation("Team member created: {Email}", normalizedEmail);

        return Ok(new CreateTeamMemberResponse
        {
            Id = user.Id,
            Email = user.Email ?? string.Empty,
            FullName = user.FullName,
            TemporaryPassword = password
        });
    }

    private static string GenerateTemporaryPassword()
    {
        // Meets Identity password policy: upper, lower, digit, non-alphanumeric, length >= 8
        return $"Qa!{Guid.NewGuid():N}"[..12] + "9#";
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrWhiteSpace(currentUserId))
        {
            return null;
        }

        return await _userManager.FindByIdAsync(currentUserId);
    }
}

public class TeamMemberResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CreateTeamMemberRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [MaxLength(200)]
    public string? FullName { get; set; }

    public string? Password { get; set; }
}

public class CreateTeamMemberResponse
{
    public string Id { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string TemporaryPassword { get; set; } = string.Empty;
}
