namespace backend.Services;

public sealed class TurnstileVerificationService
{
    private const string VerificationEndpoint = "https://challenges.cloudflare.com/turnstile/v0/siteverify";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _environment;
    private readonly ILogger<TurnstileVerificationService> _logger;

    public TurnstileVerificationService(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        IHostEnvironment environment,
        ILogger<TurnstileVerificationService> logger)
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _environment = environment;
        _logger = logger;
    }

    public async Task<bool> VerifyAsync(string token, string expectedAction, string? remoteIpAddress)
    {
        var secretKey = _configuration["Turnstile:SecretKey"];
        var allowedHostnames = _configuration
            .GetSection("Turnstile:AllowedHostnames")
            .Get<string[]>() ?? Array.Empty<string>();

        if (string.IsNullOrWhiteSpace(secretKey) ||
            string.IsNullOrWhiteSpace(token) ||
            token.Length > 2048 ||
            string.IsNullOrWhiteSpace(expectedAction) ||
            allowedHostnames.Length == 0)
        {
            _logger.LogWarning("Turnstile verification is not configured or the token is missing.");
            return false;
        }

        if (_environment.IsDevelopment())
        {
            return true;
        }

        try
        {
            var client = _httpClientFactory.CreateClient();
            using var response = await client.PostAsync(VerificationEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["secret"] = secretKey,
                ["response"] = token,
                ["remoteip"] = remoteIpAddress ?? string.Empty
            }));

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Turnstile verification returned HTTP status {StatusCode}.", response.StatusCode);
                return false;
            }

            var result = await response.Content.ReadFromJsonAsync<TurnstileVerificationResponse>();
                 return result?.Success == true &&
                     string.Equals(result.Action, expectedAction, StringComparison.Ordinal) &&
                     allowedHostnames.Any(hostname => string.Equals(hostname, result.Hostname, StringComparison.OrdinalIgnoreCase));
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
        public string? Action { get; set; }
        public string? Hostname { get; set; }
    }
}
