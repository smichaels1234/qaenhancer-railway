using System.Security.Claims;
using System.ComponentModel.DataAnnotations;
using backend.Data;
using backend.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace backend.Controllers;

[ApiController]
[Route("api/application-roi")]
[Authorize]
public class ApplicationRoiController : ControllerBase
{
    private readonly QAEnhancerDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public ApplicationRoiController(
        QAEnhancerDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    [HttpGet]
    public async Task<ActionResult<IEnumerable<ApplicationRoi>>> GetApplicationRoi()
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        var records = await _context.ApplicationRoi
            .Where(record => record.OrganizationId == currentUser.OrganizationId)
            .OrderBy(record => record.ApplicationName)
            .ToListAsync();

        return Ok(records);
    }

    [HttpPut]
    public async Task<ActionResult<ApplicationRoi>> SaveApplicationRoi(SaveApplicationRoiRequest request)
    {
        var currentUser = await GetCurrentUserAsync();
        if (currentUser == null)
        {
            return Unauthorized();
        }

        if (string.IsNullOrWhiteSpace(request.ApplicationName) ||
            request.ApplicationName.Length > 500 ||
            request.ActualCost < 0 ||
            request.RealizedRevenue < 0 ||
            request.HoursAvoided < 0 ||
            request.LaborRate < 0 ||
            request.DowntimeAvoided < 0 ||
            request.IncidentCost < 0)
        {
            return BadRequest(new { message = "Application name and financial values must be valid non-negative values." });
        }

        var applicationName = request.ApplicationName.Trim();
        var record = await _context.ApplicationRoi
            .FirstOrDefaultAsync(item =>
                item.OrganizationId == currentUser.OrganizationId &&
                item.ApplicationName == applicationName);

        if (record == null)
        {
            record = new ApplicationRoi
            {
                OrganizationId = currentUser.OrganizationId,
                ApplicationName = applicationName
            };
            _context.ApplicationRoi.Add(record);
        }

        record.ActualCost = request.ActualCost;
        record.RealizedRevenue = request.RealizedRevenue;
        record.HoursAvoided = request.HoursAvoided;
        record.LaborRate = request.LaborRate;
        record.DowntimeAvoided = request.DowntimeAvoided;
        record.IncidentCost = request.IncidentCost;
        record.UpdatedByUserId = currentUser.Id;
        record.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return Ok(record);
    }

    private async Task<ApplicationUser?> GetCurrentUserAsync()
    {
        var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return string.IsNullOrWhiteSpace(userId)
            ? null
            : await _userManager.FindByIdAsync(userId);
    }
}

public class SaveApplicationRoiRequest
{
    [Required]
    [MaxLength(500)]
    public string ApplicationName { get; set; } = string.Empty;

    public decimal ActualCost { get; set; }

    public decimal RealizedRevenue { get; set; }

    public decimal HoursAvoided { get; set; }

    public decimal LaborRate { get; set; }

    public decimal DowntimeAvoided { get; set; }

    public decimal IncidentCost { get; set; }
}