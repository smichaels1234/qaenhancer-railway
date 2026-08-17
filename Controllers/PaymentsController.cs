using System.Security.Claims;
using backend.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;

namespace backend.Controllers;

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly QAEnhancerDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(
        QAEnhancerDbContext context,
        IConfiguration configuration,
        ILogger<PaymentsController> logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
    }

    [Authorize]
    [HttpPost("checkout-session")]
    public async Task<IActionResult> CreateCheckoutSession()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        if (string.IsNullOrWhiteSpace(userId))
        {
            return Unauthorized();
        }

        var subscription = await _context.Subscriptions
            .Where(item => item.UserId == userId && item.PlanType == "pro" && item.Status == "pending")
            .OrderByDescending(item => item.CreatedAt)
            .FirstOrDefaultAsync();

        if (subscription == null)
        {
            return BadRequest(new { message = "No pending Pro subscription was found." });
        }

        var requiredSettings = new[] { "Stripe:SecretKey", "Stripe:ProPriceId", "Stripe:FrontendUrl" };
        var missingSettings = requiredSettings
            .Where(key => string.IsNullOrWhiteSpace(_configuration[key]))
            .ToArray();

        if (missingSettings.Length > 0)
        {
            _logger.LogError("Stripe checkout is missing configuration: {MissingSettings}", string.Join(", ", missingSettings));
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new
            {
                message = $"Stripe checkout is not configured. Missing: {string.Join(", ", missingSettings)}."
            });
        }

        var secretKey = GetRequiredSetting("Stripe:SecretKey");
        var priceId = GetRequiredSetting("Stripe:ProPriceId");
        var frontendUrl = GetRequiredSetting("Stripe:FrontendUrl").TrimEnd('/');
        var userEmail = await _context.Users
            .Where(user => user.Id == userId)
            .Select(user => user.Email)
            .SingleAsync();

        var options = new SessionCreateOptions
        {
            Mode = "subscription",
            SuccessUrl = $"{frontendUrl}/qa-enhancer?payment=success",
            CancelUrl = $"{frontendUrl}/qa-enhancer?payment=cancelled",
            CustomerEmail = userEmail,
            ClientReferenceId = userId,
            LineItems =
            [
                new SessionLineItemOptions
                {
                    Price = priceId,
                    Quantity = 1
                }
            ],
            Metadata = new Dictionary<string, string>
            {
                ["userId"] = userId,
                ["subscriptionId"] = subscription.Id.ToString()
            }
        };

        Session session;
        try
        {
            var service = new SessionService(new StripeClient(secretKey));
            session = await service.CreateAsync(options);
        }
        catch (StripeException exception)
        {
            _logger.LogError(exception, "Stripe rejected Checkout session creation for user {UserId}", userId);
            return StatusCode(StatusCodes.Status502BadGateway, new
            {
                message = exception.StripeError?.Message ?? "Stripe rejected the Checkout session request."
            });
        }

        return Ok(new { url = session.Url });
    }

    [AllowAnonymous]
    [HttpPost("webhook")]
    public async Task<IActionResult> Webhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        var webhookSecret = GetRequiredSetting("Stripe:WebhookSecret");

        Event stripeEvent;
        try
        {
            stripeEvent = EventUtility.ConstructEvent(
                json,
                Request.Headers["Stripe-Signature"],
                webhookSecret);
        }
        catch (StripeException exception)
        {
            _logger.LogWarning(exception, "Rejected invalid Stripe webhook signature");
            return BadRequest();
        }

        if (stripeEvent.Type == EventTypes.CheckoutSessionCompleted &&
            stripeEvent.Data.Object is Session session &&
            session.PaymentStatus == "paid")
        {
            await ActivateSubscriptionAsync(session);
        }

        return Ok();
    }

    private async Task ActivateSubscriptionAsync(Session session)
    {
        if (!session.Metadata.TryGetValue("subscriptionId", out var subscriptionIdValue) ||
            !int.TryParse(subscriptionIdValue, out var subscriptionId))
        {
            _logger.LogWarning("Stripe Checkout session {SessionId} has no valid subscription metadata", session.Id);
            return;
        }

        var subscription = await _context.Subscriptions.FindAsync(subscriptionId);
        if (subscription == null || subscription.Status == "active")
        {
            return;
        }

        subscription.Status = "active";
        subscription.StartDate = DateTime.UtcNow;
        subscription.NextBillingDate = DateTime.UtcNow.AddMonths(1);
        subscription.PaymentProcessorCustomerId = session.CustomerId;
        subscription.AutoRenew = true;
        subscription.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        _logger.LogInformation("Activated Pro subscription {SubscriptionId} after Stripe payment", subscription.Id);
    }

    private string GetRequiredSetting(string key)
    {
        var value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"Required payment setting '{key}' is not configured.");
        }

        return value;
    }
}