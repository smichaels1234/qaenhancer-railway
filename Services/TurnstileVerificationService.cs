using System.Net.Http.Json;

namespace backend.Services;

public sealed class TurnstileVerificationService
{
    private const string VerificationEndpoint = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<TurnstileVerificationService> _logger;

    public TurnstileVerificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<TurnstileVerificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string token, string? remoteIpAddress)
    {
        var secretKey = _configuration["Turnstile:SecretKey"];
        if (string.IsNullOrWhiteSpace(secretKey) || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Turnstile verification is not configured or the token is missing.");
            return false;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.PostAsJsonAsync(VerificationEndpoint, new
            {
                secret = secretKey,
                response = token,
                remoteip = remoteIpAddress
            });

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verification returned HTTP status {StatusCode}.", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileVerificationResponse>();
            return result?.Success == true;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Turnstile verification request failed.");
            return false;
        }
    }

    private sealed class TurnstileVerificationResponse
    {
        public bool Success { get; set; }
    }
}
