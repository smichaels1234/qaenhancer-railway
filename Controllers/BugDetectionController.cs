using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using backend.Services;
using backend.Models;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class BugDetectionController : ControllerBase
{
    private readonly ILogger<BugDetectionController> _logger;
    private readonly AIBugDetectionService _aiService;
    private readonly PlanEntitlementService _planEntitlementService;

    public BugDetectionController(
        ILogger<BugDetectionController> logger,
        AIBugDetectionService aiService,
        PlanEntitlementService planEntitlementService)
    {
        _logger = logger;
        _aiService = aiService;
        _planEntitlementService = planEntitlementService;
    }

    /// <summary>
    /// Analyzes code or URL for potential bugs using AI
    /// POST /api/bugdetection/analyze
    /// </summary>
    [HttpPost("analyze")]
    public async Task<ActionResult<BugDetectionResponse>> AnalyzeForBugs([FromBody] BugDetectionRequest request)
    {
        try
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId) || !await _planEntitlementService.HasPaidPlanAsync(userId))
            {
                return StatusCode(StatusCodes.Status403Forbidden, new BugDetectionResponse
                {
                    Success = false,
                    Error = "AI analysis is available with an active Pro or Custom plan."
                });
            }

            _logger.LogInformation("Received bug detection request for {AnalysisType}", request.AnalysisType);

            if (string.IsNullOrEmpty(request.AnalysisType))
            {
                return BadRequest(new BugDetectionResponse
                {
                    Success = false,
                    Error = "AnalysisType is required (either 'code' or 'url')"
                });
            }

            if (request.AnalysisType == "code" && string.IsNullOrEmpty(request.Code))
            {
                return BadRequest(new BugDetectionResponse
                {
                    Success = false,
                    Error = "Code is required when AnalysisType is 'code'"
                });
            }

            if (request.AnalysisType == "url" && string.IsNullOrEmpty(request.Url))
            {
                return BadRequest(new BugDetectionResponse
                {
                    Success = false,
                    Error = "URL is required when AnalysisType is 'url'"
                });
            }

            var response = await _aiService.AnalyzeAsync(request);

            // AI failures are expected in some environments, so return the structured payload
            // and let the frontend decide whether to show the message or fall back.
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in bug detection endpoint");
            return StatusCode(500, new BugDetectionResponse
            {
                Success = false,
                Error = $"Server error: {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Health check endpoint
    /// GET /api/bugdetection/health
    /// </summary>
    [HttpGet("health")]
    public ActionResult<object> Health()
    {
        return Ok(new
        {
            Status = "Healthy",
            Service = "AI Bug Detection",
            Timestamp = DateTime.UtcNow
        });
    }
}
