using Azure;
using Azure.AI.Inference;
using Azure.Core;
using backend.Models;
using System.Net;
using System.Text.Json;

namespace backend.Services;

public class AIBugDetectionService
{
    private readonly ILogger<AIBugDetectionService> _logger;
    private readonly IConfiguration _configuration;
    private readonly HttpClient _httpClient;

    public AIBugDetectionService(
        ILogger<AIBugDetectionService> logger,
        IConfiguration configuration,
        HttpClient httpClient)
    {
        _logger = logger;
        _configuration = configuration;
        _httpClient = httpClient;
    }

    /// <summary>
    /// Analyzes code or URL for bugs using AI
    /// </summary>
    public async Task<BugDetectionResponse> AnalyzeAsync(BugDetectionRequest request)
    {
        try
        {
            _logger.LogInformation("Starting AI bug detection analysis");

            string contentToAnalyze;
            if (request.AnalysisType == "code" && !string.IsNullOrEmpty(request.Code))
            {
                contentToAnalyze = request.Code;
            }
            else if (request.AnalysisType == "url" && !string.IsNullOrEmpty(request.Url))
            {
                var normalizedUrl = NormalizeUrl(request.Url);
                if (string.IsNullOrEmpty(normalizedUrl))
                {
                    return new BugDetectionResponse
                    {
                        Success = false,
                        Error = "Invalid URL: please provide a valid http or https address"
                    };
                }

                request.Url = normalizedUrl;
                contentToAnalyze = await FetchUrlContentAsync(normalizedUrl);
            }
            else
            {
                return new BugDetectionResponse
                {
                    Success = false,
                    Error = "Invalid request: Provide either code or a valid URL"
                };
            }

            var analysisResult = await DetectBugsWithAI(contentToAnalyze, request);

            if (!string.IsNullOrWhiteSpace(analysisResult.Error))
            {
                return new BugDetectionResponse
                {
                    Success = false,
                    Error = analysisResult.Error,
                    AnalysisTime = DateTime.UtcNow
                };
            }

            return new BugDetectionResponse
            {
                Success = true,
                Bugs = analysisResult.Bugs,
                Summary = $"Found {analysisResult.Bugs.Count} potential issues",
                AnalysisTime = DateTime.UtcNow
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI bug detection");
            return new BugDetectionResponse
            {
                Success = false,
                Error = $"Analysis failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Uses AI to detect bugs in the provided content
    /// </summary>
    private async Task<(List<DetectedBug> Bugs, string? Error)> DetectBugsWithAI(string content, BugDetectionRequest request)
    {
        // Option 1: Using GitHub Models (Free to start with GitHub PAT)
        var endpoint = _configuration["AI:Endpoint"] ?? "https://models.inference.ai.azure.com";
        var configuredApiKey = _configuration["AI:ApiKey"];
        var envApiKey = Environment.GetEnvironmentVariable("AI__ApiKey")
            ?? Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        var apiKey = !string.IsNullOrWhiteSpace(envApiKey)
            ? envApiKey
            : configuredApiKey;
        apiKey = apiKey?.Trim();
        var model = _configuration["AI:Model"] ?? "gpt-4.1-mini";

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return (new List<DetectedBug>(),
                "AI API Key not configured. Set 'AI:ApiKey' in appsettings.Development.json (or appsettings.json) " +
                "or set AI__ApiKey/GITHUB_TOKEN environment variable.");
        }

        try
        {
            var client = new ChatCompletionsClient(
                new Uri(endpoint),
                new AzureKeyCredential(apiKey));

            var systemPrompt = BuildSystemPrompt(request.AnalysisType ?? "code", request.Language);
            var userPrompt = BuildUserPrompt(content, request);

            var chatRequest = new ChatCompletionsOptions
            {
                Messages =
                {
                    new ChatRequestSystemMessage(systemPrompt),
                    new ChatRequestUserMessage(userPrompt)
                },
                Model = model,
                Temperature = 0.3f,
                MaxTokens = 2000
            };

            var response = await client.CompleteAsync(chatRequest);
            var aiResponse = response.Value.Content;

            // Parse AI response to extract bugs
            return (ParseAIResponse(aiResponse), null);
        }
        catch (RequestFailedException ex) when (ex.Status == 401)
        {
            _logger.LogError(ex, "GitHub API authentication failed. Token may be invalid or expired.");
            return (new List<DetectedBug>(),
                "GitHub API authentication failed. Please update your GitHub Personal Access Token in appsettings.json. " +
                "To create a new token: Go to GitHub Settings → Developer settings → Personal access tokens → Generate new token. " +
                "The token needs 'public_repo' scope for GitHub Models API.");
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "GitHub API request failed with status {Status}", ex.Status);
            return (new List<DetectedBug>(),
                $"GitHub API request failed: {ex.Message} (Status: {ex.Status})");
        }
    }

    /// <summary>
    /// Builds the system prompt for the AI
    /// </summary>
    private string BuildSystemPrompt(string analysisType, string? language)
    {
        var languageContext = !string.IsNullOrEmpty(language) ? $" in {language}" : "";
        
        return $@"You are an expert code reviewer and security analyst{languageContext}. 
Your task is to analyze {analysisType} and identify potential bugs, security vulnerabilities, 
performance issues, and code quality problems.

For each issue found, provide:
1. Severity (Critical/High/Medium/Low)
2. Type (Security/Performance/Logic/Syntax/BestPractice/Accessibility)
3. Description of the issue
4. Location (line number or section)
5. Suggestion for fixing it
6. A brief code snippet showing the problem (if applicable)

Return your response as a JSON array with the following structure:
[
  {{
    ""severity"": ""High"",
    ""type"": ""Security"",
    ""description"": ""SQL injection vulnerability detected"",
    ""location"": ""Line 42"",
    ""suggestion"": ""Use parameterized queries instead of string concatenation"",
    ""codeSnippet"": ""SELECT * FROM users WHERE id = ' + userId + ';""
  }}
]

If no issues are found, return an empty array: []
Focus on real, actionable issues. Be thorough but concise.";
    }

    /// <summary>
    /// Builds the user prompt with the content to analyze
    /// </summary>
    private string BuildUserPrompt(string content, BugDetectionRequest request)
    {
        var analysisType = request.AnalysisType == "url" ? "the following webpage content" : "the following code";
        var language = !string.IsNullOrEmpty(request.Language) ? $" ({request.Language})" : "";

        return $@"Please analyze {analysisType}{language} and identify all potential bugs, security issues, 
and code quality problems:

```
{content}
```

Return only the JSON array of detected issues.";
    }

    /// <summary>
    /// Parses the AI response to extract detected bugs
    /// </summary>
    private List<DetectedBug> ParseAIResponse(string aiResponse)
    {
        try
        {
            // Try to extract JSON array from response
            var jsonStart = aiResponse.IndexOf('[');
            var jsonEnd = aiResponse.LastIndexOf(']');

            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = aiResponse.Substring(jsonStart, jsonEnd - jsonStart + 1);
                var bugs = JsonSerializer.Deserialize<List<DetectedBug>>(jsonContent, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return bugs ?? new List<DetectedBug>();
            }

            // If no JSON array found, create a single bug with the AI's response
            return new List<DetectedBug>
            {
                new DetectedBug
                {
                    Severity = "Medium",
                    Type = "Analysis",
                    Description = aiResponse,
                    Location = "General",
                    Suggestion = "Review the analysis provided"
                }
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing AI response");
            return new List<DetectedBug>
            {
                new DetectedBug
                {
                    Severity = "Low",
                    Type = "Info",
                    Description = "AI analysis completed but response format was unexpected",
                    Location = "General",
                    Suggestion = aiResponse.Length > 200 ? aiResponse.Substring(0, 200) + "..." : aiResponse
                }
            };
        }
    }

    /// <summary>
    /// Fetches content from a URL for analysis
    /// </summary>
    private async Task<string> FetchUrlContentAsync(string url)
    {
        try
        {
            // Many sites block requests that don't resemble a browser.
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
            request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");
            request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.9");

            using var response = await _httpClient.SendAsync(request);
            var content = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return TruncateContent(content);
            }

            _logger.LogWarning("URL fetch returned {StatusCode} for {Url}", (int)response.StatusCode, url);
            return BuildBlockedContentPlaceholder(url, response.StatusCode, content);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch URL content for {Url}", url);

            // Return a placeholder instead of throwing so analysis can continue and report actionable guidance.
            return BuildBlockedContentPlaceholder(url, null, ex.Message);
        }
    }

    private static string TruncateContent(string content)
    {
        const int maxLength = 8000;
        if (content.Length <= maxLength)
        {
            return content;
        }

        return content.Substring(0, maxLength) + "\n\n[Content truncated...]";
    }

    private static string BuildBlockedContentPlaceholder(string url, HttpStatusCode? statusCode, string details)
    {
        var statusText = statusCode.HasValue
            ? $"{(int)statusCode.Value} ({statusCode.Value})"
            : "Unknown";

        var detailSnippet = string.IsNullOrWhiteSpace(details)
            ? "No response body available."
            : TruncateContent(details);

        return $@"[URL_FETCH_UNAVAILABLE]
Target URL: {url}
HTTP status: {statusText}

The target page could not be fetched by the backend service. This usually happens due to bot protection,
login requirements, geo restrictions, or anti-automation rules.

Response details:
{detailSnippet}

Please report this as an accessibility/observability finding and suggest testing the URL manually in a browser
or providing authenticated HTML content for analysis.";
    }

    private static string? NormalizeUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        var trimmed = url.Trim();
        if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri) &&
            (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
        {
            return absoluteUri.ToString();
        }

        if (Uri.TryCreate($"https://{trimmed}", UriKind.Absolute, out var httpsUri))
        {
            return httpsUri.ToString();
        }

        return null;
    }
}
