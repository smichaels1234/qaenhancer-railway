using backend.Models;
using backend.Models.Auth;
using backend.Services;
using backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;
using System.Text;

namespace backend.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly ILogger<AuthController> _logger;
    private readonly QAEnhancerDbContext _context;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        ITokenService tokenService,
        ILogger<AuthController> logger,
        QAEnhancerDbContext context)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _logger = logger;
        _context = context;
    }

    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register([FromBody] RegisterRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var planType = request.PlanType.Trim().ToLowerInvariant();
            if (planType is not ("free" or "pro" or "custom"))
            {
                return BadRequest(new { message = "Invalid plan type" });
            }

            var existingUser = await _userManager.FindByEmailAsync(request.Email);
            if (existingUser != null)
            {
                return BadRequest(new { message = "User with this email already exists" });
            }

            var user = new ApplicationUser
            {
                UserName = request.Email,
                Email = request.Email,
                FullName = request.FullName,
                OrganizationId = Guid.NewGuid().ToString("N"),
                CreatedAt = DateTime.UtcNow
            };

            var result = await _userManager.CreateAsync(user, request.Password);

            if (!result.Succeeded)
            {
                var errors = result.Errors.Select(e => e.Description);
                _logger.LogError("User creation failed for {Email}: {Errors}", request.Email, string.Join(", ", errors));
                return BadRequest(new { message = "Failed to create user", errors });
            }

            // Create subscription record
            var subscription = new Subscription
            {
                UserId = user.Id,
                PlanType = planType,
                Status = planType == "free" ? "active" : "pending",
                StartDate = planType == "free" ? DateTime.UtcNow : null,
                EndDate = null,
                MonthlyPrice = planType == "pro" ? 99m : 0m,
                AutoRenew = planType == "pro"
            };

            _context.Subscriptions.Add(subscription);

            // If custom plan, create a custom plan request
            if (planType == "custom" && !string.IsNullOrEmpty(request.CompanyName))
            {
                var customPlanRequest = new CustomPlanRequest
                {
                    UserId = user.Id,
                    CompanyName = request.CompanyName,
                    PhoneNumber = request.PhoneNumber ?? "",
                    TeamSize = request.TeamSize ?? "",
                    Message = request.Message,
                    Status = "pending"
                };

                _context.CustomPlanRequests.Add(customPlanRequest);
                
                _logger.LogInformation("Custom plan request created for user: {Email}, Company: {Company}", 
                    user.Email, request.CompanyName);
                
                // TODO: Send notification to sales team
                // await _emailService.SendCustomPlanRequestNotification(customPlanRequest);
            }

            await _context.SaveChangesAsync();

            var accessToken = _tokenService.GenerateAccessToken(user);
            var refreshToken = _tokenService.GenerateRefreshToken();
            var session = CreateSession(user, refreshToken, 7);

            user.RefreshToken = refreshToken;
            user.RefreshTokenExpiryTime = session.ExpiresAt;
            _context.UserSessions.Add(session);
            await _userManager.UpdateAsync(user);

            _logger.LogInformation("User registered successfully: {Email} with plan: {Plan}", 
                user.Email, request.PlanType);

            return Ok(new AuthResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                TokenType = "Bearer",
                ExpiresIn = 3600,
                User = new UserInfo
                {
                    Id = user.Id,
                    Email = user.Email ?? "",
                    FullName = user.FullName ?? "",
                    OrganizationId = user.OrganizationId
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception during user registration for {Email}", request.Email);
            return StatusCode(500, new { message = "An error occurred during registration", detail = ex.Message });
        }
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login([FromBody] LoginRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user == null)
        {
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);

        if (!result.Succeeded)
        {
            if (result.IsLockedOut)
            {
                return Unauthorized(new { message = "Account is locked. Please try again later." });
            }
            return Unauthorized(new { message = "Invalid email or password" });
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();
        var session = CreateSession(user, refreshToken, request.RememberMe ? 30 : 7);

        user.RefreshToken = refreshToken;
        user.RefreshTokenExpiryTime = session.ExpiresAt;
        user.LastLoginAt = DateTime.UtcNow;
        _context.UserSessions.Add(session);
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("User logged in successfully: {Email}", user.Email);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            TokenType = "Bearer",
            ExpiresIn = 3600,
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                OrganizationId = user.OrganizationId
            }
        });
    }

    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh([FromBody] RefreshTokenRequest request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tokenHash = HashToken(request.RefreshToken);
        var session = await _context.UserSessions
            .SingleOrDefaultAsync(s => s.RefreshTokenHash == tokenHash);
        if (session == null || session.RevokedAt != null || session.ExpiresAt <= DateTime.UtcNow)
        {
            return Unauthorized(new { message = "Invalid, revoked, or expired refresh token" });
        }

        var user = await _userManager.FindByIdAsync(session.UserId);
        if (user == null)
        {
            return Unauthorized(new { message = "User not found" });
        }

        var accessToken = _tokenService.GenerateAccessToken(user);
        var newRefreshToken = _tokenService.GenerateRefreshToken();

        session.RefreshTokenHash = HashToken(newRefreshToken);
        session.LastSeenAt = DateTime.UtcNow;
        session.ExpiresAt = DateTime.UtcNow.AddDays(7);
        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = session.ExpiresAt;
        await _context.SaveChangesAsync();
        await _userManager.UpdateAsync(user);

        return Ok(new AuthResponse
        {
            AccessToken = accessToken,
            RefreshToken = newRefreshToken,
            TokenType = "Bearer",
            ExpiresIn = 3600,
            User = new UserInfo
            {
                Id = user.Id,
                Email = user.Email ?? "",
                FullName = user.FullName ?? "",
                OrganizationId = user.OrganizationId
            }
        });
    }

    [HttpPost("revoke")]
    [Authorize]
    public async Task<IActionResult> Revoke([FromBody] RevokeTokenRequest request)
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        var session = await _context.UserSessions
            .SingleOrDefaultAsync(s => s.UserId == userId && s.RefreshTokenHash == HashToken(request.RefreshToken));
        if (session != null)
        {
            session.RevokedAt = DateTime.UtcNow;
            session.LastSeenAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
        }

        if (user.RefreshToken == request.RefreshToken)
        {
            user.RefreshToken = null;
            user.RefreshTokenExpiryTime = null;
        }
        await _userManager.UpdateAsync(user);

        _logger.LogInformation("Refresh token revoked for user: {Email}", user.Email);

        return Ok(new { message = "Token revoked successfully" });
    }

    [HttpGet("sessions")]
    [Authorize]
    public async Task<ActionResult<IEnumerable<SessionInfo>>> GetSessions()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var sessions = await _context.UserSessions
            .Where(s => s.UserId == userId && s.RevokedAt == null && s.ExpiresAt > DateTime.UtcNow)
            .OrderByDescending(s => s.LastSeenAt)
            .Select(s => new SessionInfo(s.Id, s.CreatedAt, s.LastSeenAt, s.ExpiresAt, s.IpAddress, s.UserAgent))
            .ToListAsync();

        return Ok(sessions);
    }

    private UserSession CreateSession(ApplicationUser user, string refreshToken, int expiryDays)
    {
        var now = DateTime.UtcNow;
        return new UserSession
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            RefreshTokenHash = HashToken(refreshToken),
            CreatedAt = now,
            LastSeenAt = now,
            ExpiresAt = now.AddDays(expiryDays),
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            UserAgent = Request.Headers.UserAgent.ToString()
        };
    }

    private static string HashToken(string token)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }


    public record SessionInfo(
        Guid Id,
        DateTime CreatedAt,
        DateTime LastSeenAt,
        DateTime ExpiresAt,
        string? IpAddress,
        string? UserAgent);

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserInfo>> GetCurrentUser()
    {
        var userId = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        if (string.IsNullOrEmpty(userId))
        {
            return Unauthorized();
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return NotFound(new { message = "User not found" });
        }

        return Ok(new UserInfo
        {
            Id = user.Id,
            Email = user.Email ?? "",
            FullName = user.FullName ?? "",
            OrganizationId = user.OrganizationId
        });
    }
}
