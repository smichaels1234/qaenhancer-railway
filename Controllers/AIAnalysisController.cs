using Microsoft.AspNetCore.Mvc;
using System.Net.Http;
using System.Text.Json;
using System.Text;
using backend.Data;
using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace backend.Controllers;

[ApiController]
[Route("api/aianalysis")]
[Authorize]
public class AIAnalysisController : ControllerBase
{
    private readonly ILogger<AIAnalysisController> _logger;
    private readonly HttpClient _httpClient;
    private readonly QAEnhancerDbContext _context;
    private readonly PlanEntitlementService _planEntitlementService;

    public AIAnalysisController(
        ILogger<AIAnalysisController> logger,
        HttpClient httpClient,
        QAEnhancerDbContext context,
        PlanEntitlementService planEntitlementService)
    {
        _logger = logger;
        _httpClient = httpClient;
        _context = context;
        _planEntitlementService = planEntitlementService;
    }

    [HttpPost("analyze-url")]
    public async Task<IActionResult> AnalyzeUrl([FromBody] AnalysisRequest request)
    {
        try
        {
            if (!await HasAdvancedReportingAccessAsync())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "AI analysis is available with an active Pro or Custom plan."
                });
            }

            _logger.LogInformation("Starting AI analysis for URL: {Url}", request.Url);

            // Validate URL
            if (!Uri.TryCreate(request.Url, UriKind.Absolute, out var uri))
            {
                return BadRequest(new AIAnalysisResponse
                {
                    Success = false,
                    Url = request.Url,
                    Error = "Invalid URL format"
                });
            }

            var detectedBugs = await PerformAIAnalysis(request);

            var response = new AIAnalysisResponse
            {
                Success = true,
                Url = request.Url,
                DetectedBugs = detectedBugs,
                AnalysisTime = DateTime.UtcNow
            };

            _logger.LogInformation("AI analysis completed for {Url}. Found {BugCount} issues", 
                request.Url, detectedBugs.Count);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during AI analysis of URL: {Url}", request.Url);
            
            return StatusCode(500, new AIAnalysisResponse
            {
                Success = false,
                Url = request.Url,
                Error = $"Analysis failed: {ex.Message}"
            });
        }
    }

    [HttpPost("test-claude")]
    public async Task<ActionResult> TestClaude()
    {
        try
        {
            if (!await HasAdvancedReportingAccessAsync())
            {
                return StatusCode(StatusCodes.Status403Forbidden, new
                {
                    message = "AI analysis is available with an active Pro or Custom plan."
                });
            }

            _logger.LogInformation("Testing Claude API connectivity");
            
            var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = configuration["Claude:ApiKey"] ?? "your-claude-api-key-here";
            
            if (apiKey == "your-claude-api-key-here")
            {
                return BadRequest("Claude API key not configured");
            }

            using var claudeClient = new HttpClient();
            claudeClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            claudeClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var requestBody = new
            {
                model = "claude-3-sonnet-20240229",
                max_tokens = 100,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = "Just respond with 'Hello from Claude!' and nothing else."
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");

            var response = await claudeClient.PostAsync("https://api.anthropic.com/v1/messages", httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();

            _logger.LogInformation("Claude test response: {StatusCode} - {Content}", response.StatusCode, responseContent);

            if (response.IsSuccessStatusCode)
            {
                return Ok(new { status = "success", response = responseContent });
            }
            else
            {
                return BadRequest(new { status = "error", response = responseContent });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing Claude API");
            return StatusCode(500, new { status = "error", message = ex.Message });
        }
    }

    private async Task<List<DetectedBug>> PerformAIAnalysis(AnalysisRequest request)
    {
        var detectedBugs = new List<DetectedBug>();

        try
        {
            // Fetch the webpage content
            var response = await _httpClient.GetAsync(request.Url);
            var content = await response.Content.ReadAsStringAsync();

            // Basic URL accessibility check
            if (!response.IsSuccessStatusCode)
            {
                detectedBugs.Add(new DetectedBug
                {
                    Title = $"HTTP Error {(int)response.StatusCode}",
                    Description = $"The URL returned status code {response.StatusCode}. This indicates the page may not be accessible to users.",
                    Severity = "High",
                    Location = "HTTP Response"
                });
            }

            // Use Claude AI for intelligent analysis
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Calling Claude AI for analysis of URL: {Url}", request.Url);
                _logger.LogInformation("HTML content length: {Length} characters", content.Length);
                
                try
                {
                    var claudeAnalysis = await CallClaudeForAnalysis(content, request.Url, request);
                    _logger.LogInformation("Claude AI returned {Count} issues for URL: {Url}", claudeAnalysis.Count, request.Url);
                    detectedBugs.AddRange(claudeAnalysis);
                    
                    if (claudeAnalysis.Count == 0)
                    {
                        _logger.LogInformation("Claude AI found no issues for URL: {Url}", request.Url);
                        
                        // Add a fallback analysis to ensure we always return something useful
                        var fallbackAnalysis = AnalyzeHtmlContent(content, request);
                        if (fallbackAnalysis.Count > 0)
                        {
                            _logger.LogInformation("Using fallback analysis which found {Count} issues", fallbackAnalysis.Count);
                            detectedBugs.AddRange(fallbackAnalysis);
                        }
                        else
                        {
                            // Add a status message when truly no issues are found
                            detectedBugs.Add(new DetectedBug
                            {
                                Title = "No Issues Detected",
                                Description = $"Claude AI analysis found no accessibility, SEO, performance, or security issues on {request.Url}. This indicates a well-designed webpage.",
                                Severity = "Low",
                                Location = "Overall Analysis"
                            });
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Claude AI analysis failed for URL: {Url}", request.Url);
                    
                    // Use fallback analysis when Claude fails
                    var fallbackAnalysis = AnalyzeHtmlContent(content, request);
                    if (fallbackAnalysis.Count > 0)
                    {
                        _logger.LogInformation("Using fallback analysis due to Claude failure, found {Count} issues", fallbackAnalysis.Count);
                        detectedBugs.AddRange(fallbackAnalysis);
                        
                        // Add a note about Claude being unavailable
                        detectedBugs.Add(new DetectedBug
                        {
                            Title = "AI Analysis Service Temporarily Unavailable",
                            Description = $"Advanced AI analysis could not be completed: {ex.Message}. Showing basic analysis results instead.",
                            Severity = "Low",
                            Location = "System Status"
                        });
                    }
                    else
                    {
                        detectedBugs.Add(new DetectedBug
                        {
                            Title = "Analysis Service Unavailable",
                            Description = $"Could not analyze the webpage: {ex.Message}. Please try again later.",
                            Severity = "Medium",
                            Location = "System Error"
                        });
                    }
                }
            }
        }
        catch (HttpRequestException ex)
        {
            detectedBugs.Add(new DetectedBug
            {
                Title = "Network Connectivity Issue",
                Description = $"Unable to reach the specified URL: {ex.Message}",
                Severity = "Critical",
                Location = "Network Layer"
            });
        }

        return detectedBugs;
    }

    private async Task<bool> HasAdvancedReportingAccessAsync()
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return !string.IsNullOrWhiteSpace(userId) && await _planEntitlementService.HasPaidPlanAsync(userId);
    }

    private List<DetectedBug> AnalyzeHtmlContent(string content, AnalysisRequest request)
    {
        var detectedBugs = new List<DetectedBug>();

        // Hardcoded bug detection based on HTML content analysis
        var contentLower = content.ToLower();

        // Check for missing title tag
        if (!contentLower.Contains("<title>") || contentLower.Contains("<title></title>") || contentLower.Contains("<title> </title>"))
        {
            detectedBugs.Add(new DetectedBug
            {
                Title = "Missing or Empty Page Title",
                Description = "The page is missing a title tag or has an empty title. This is crucial for SEO and accessibility.",
                Severity = "High",
                Location = "HTML Head Section"
            });
        }

        // Check for missing meta description
        if (!contentLower.Contains("name=\"description\""))
        {
            detectedBugs.Add(new DetectedBug
            {
                Title = "Missing Meta Description",
                Description = "The page lacks a meta description tag, which is important for search engine optimization.",
                Severity = "Medium",
                Location = "HTML Head Section"
            });
        }

        // Check for images without alt attributes
        if (contentLower.Contains("<img") && !contentLower.Contains("alt="))
        {
            detectedBugs.Add(new DetectedBug
            {
                Title = "Images Missing Alt Text",
                Description = "One or more images are missing alt attributes, which are essential for screen readers and accessibility.",
                Severity = "High",
                Location = "Image Elements"
            });
        }

        // Check for missing lang attribute
        if (!contentLower.Contains("lang="))
        {
            detectedBugs.Add(new DetectedBug
            {
                Title = "Missing Language Declaration",
                Description = "The HTML document is missing a lang attribute, which helps screen readers and search engines.",
                Severity = "Medium",
                Location = "HTML Element"
            });
        }

        // Check for missing viewport meta tag
        if (!contentLower.Contains("name=\"viewport\""))
        {
            detectedBugs.Add(new DetectedBug
            {
                Title = "Missing Viewport Meta Tag",
                Description = "The page lacks a viewport meta tag, which may cause display issues on mobile devices.",
                Severity = "High",
                Location = "HTML Head Section"
            });
        }

        return detectedBugs;
    }

    private async Task<List<DetectedBug>> CallClaudeForAnalysis(string htmlContent, string url, AnalysisRequest request)
    {
        try
        {
            var prompt = BuildAnalysisPrompt(htmlContent, url, request);
            _logger.LogInformation("Generated prompt for Claude analysis, length: {PromptLength}", prompt.Length);
            
            // Get Claude API key from configuration
            var configuration = HttpContext.RequestServices.GetRequiredService<IConfiguration>();
            var apiKey = configuration["Claude:ApiKey"] ?? "your-claude-api-key-here";
            _logger.LogInformation("Using Claude API key: {HasKey}", !string.IsNullOrEmpty(apiKey) && apiKey != "your-claude-api-key-here");
            
            // Create Claude API request
            using var claudeClient = new HttpClient();
            claudeClient.DefaultRequestHeaders.Add("x-api-key", apiKey);
            claudeClient.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");

            var requestBody = new
            {
                model = "claude-3-sonnet-20240229",
                max_tokens = 4000,
                temperature = 0.3,
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            };

            var jsonContent = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
            _logger.LogInformation("Sending request to Claude API, content length: {ContentLength}", jsonContent.Length);

            var response = await claudeClient.PostAsync("https://api.anthropic.com/v1/messages", httpContent);
            var responseContent = await response.Content.ReadAsStringAsync();
            _logger.LogInformation("Claude API response: {StatusCode}, content length: {ResponseLength}", response.StatusCode, responseContent.Length);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Claude API returned error: {StatusCode} - {Content}", response.StatusCode, responseContent);
                return new List<DetectedBug>();
            }

            // Parse Claude's response
            _logger.LogInformation("Claude API raw response: {Response}", responseContent.Substring(0, Math.Min(responseContent.Length, 1000)));
            using var doc = JsonDocument.Parse(responseContent);
            var content = doc.RootElement.GetProperty("content");
            if (content.GetArrayLength() > 0)
            {
                var textContent = content[0].GetProperty("text").GetString() ?? "";
                _logger.LogInformation("Extracted text content from Claude response, length: {TextLength}", textContent.Length);
                _logger.LogInformation("Claude text response: {TextContent}", textContent.Substring(0, Math.Min(textContent.Length, 1000)));
                var bugs = ParseClaudeResponse(textContent);
                _logger.LogInformation("Parsed {BugCount} bugs from Claude response", bugs.Count);
                return bugs;
            }

            _logger.LogWarning("No content found in Claude response");
            return new List<DetectedBug>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling Claude API");
            return new List<DetectedBug>();
        }
    }

    private string BuildAnalysisPrompt(string htmlContent, string url, AnalysisRequest request)
    {
        // Truncate HTML content to avoid token limits
        var truncatedHtml = htmlContent.Substring(0, Math.Min(htmlContent.Length, 12000));
        _logger.LogInformation("Building analysis prompt for URL: {Url}, HTML length: {Length}, truncated to: {TruncatedLength}", 
            url, htmlContent.Length, truncatedHtml.Length);
        
        var prompt = $@"
You are an expert web developer and QA engineer. Analyze the following HTML content from the URL: {url}

CRITICAL INSTRUCTION: Only report ACTUAL, REAL issues that genuinely exist in the provided HTML content. Do not report hypothetical, generic, or assumed issues. Base your analysis entirely on what you can see in the HTML.

Analyze this webpage content for these specific types of REAL issues:

1. **Accessibility Issues:**
   - Images with missing alt attributes (look for <img> tags without alt="""")
   - Missing page title or empty <title> tags
   - Forms with inputs that lack proper labels
   - Missing lang attribute on <html> element
   - Poor heading hierarchy (missing h1, skipped heading levels)

2. **SEO Issues:**
   - Missing or empty <title> tag
   - Missing meta description (<meta name=""description"">)
   - Missing or duplicate h1 tags
   - Missing meta viewport tag

3. **Performance Issues:**
   - Blocking scripts in <head> without async/defer
   - Missing resource optimization attributes
   - Large inline CSS or JavaScript

4. **Security Issues:**
   - External links without rel=""noopener"" when using target=""_blank""
   - Forms without proper security measures
   - Mixed content issues (HTTP resources on HTTPS)

5. **HTML Validation Issues:**
   - Unclosed tags
   - Invalid HTML structure
   - Deprecated elements or attributes

IMPORTANT: For each REAL issue you find, provide:
- title: Specific, actionable title describing the exact problem
- description: Detailed explanation with specific examples from the HTML showing the issue
- severity: ""Critical"", ""High"", ""Medium"", or ""Low"" based on impact
- location: Exact HTML element, tag, or section where the issue occurs

Return ONLY valid JSON in this exact format:
[
  {{
    ""title"": ""Specific issue title"",
    ""description"": ""Detailed description with HTML examples"",
    ""severity"": ""High"",
    ""location"": ""Specific element or section""
  }}
]

If you cannot find any actual issues in the HTML content, return an empty array: []

HTML Content to analyze:
{truncatedHtml}";

        _logger.LogInformation("Generated prompt with length: {PromptLength}", prompt.Length);
        return prompt;
    }

    private List<DetectedBug> ParseClaudeResponse(string response)
    {
        try
        {
            _logger.LogInformation("Parsing Claude response, length: {ResponseLength}", response.Length);
            
            // Try to extract JSON from the response
            var jsonStart = response.IndexOf('[');
            var jsonEnd = response.LastIndexOf(']');
            
            _logger.LogInformation("JSON extraction: start={JsonStart}, end={JsonEnd}", jsonStart, jsonEnd);
            
            if (jsonStart >= 0 && jsonEnd > jsonStart)
            {
                var jsonContent = response.Substring(jsonStart, jsonEnd - jsonStart + 1);
                _logger.LogInformation("Extracted JSON content: {JsonContent}", jsonContent.Substring(0, Math.Min(jsonContent.Length, 1000)));
                
                using var doc = JsonDocument.Parse(jsonContent);
                var bugs = new List<DetectedBug>();
                
                var arrayLength = doc.RootElement.GetArrayLength();
                _logger.LogInformation("JSON array contains {ArrayLength} elements", arrayLength);
                
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var bug = new DetectedBug
                    {
                        Title = element.TryGetProperty("title", out var title) ? title.GetString() ?? "Unknown Issue" : "Unknown Issue",
                        Description = element.TryGetProperty("description", out var desc) ? desc.GetString() ?? "No description" : "No description",
                        Severity = element.TryGetProperty("severity", out var sev) ? sev.GetString() ?? "Medium" : "Medium",
                        Location = element.TryGetProperty("location", out var loc) ? loc.GetString() ?? "Unknown" : "Unknown"
                    };
                    bugs.Add(bug);
                    _logger.LogInformation("Added bug: {Title} - {Severity}", bug.Title, bug.Severity);
                }
                
                _logger.LogInformation("Successfully parsed {BugCount} bugs from Claude response", bugs.Count);
                return bugs;
            }
            
            _logger.LogWarning("No valid JSON found in Claude response. Response: {Response}", response.Substring(0, Math.Min(response.Length, 500)));
            // If JSON parsing fails, return empty list
            return new List<DetectedBug>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to parse Claude response: {Response}", response.Substring(0, Math.Min(response.Length, 500)));
            return new List<DetectedBug>();
        }
    }
}

// Request/Response models
public class AnalysisRequest
{
    public string Url { get; set; } = string.Empty;
    public string AnalysisType { get; set; } = "comprehensive";
    public bool IncludeAccessibility { get; set; } = true;
    public bool IncludeSecurity { get; set; } = true;
    public bool IncludePerformance { get; set; } = true;
}

public class DetectedBug
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
}

// Response model
public class AIAnalysisResponse
{
    public bool Success { get; set; }
    public string Url { get; set; } = string.Empty;
    public List<DetectedBug> DetectedBugs { get; set; } = new();
    public DateTime AnalysisTime { get; set; }
    public string? Error { get; set; }
}