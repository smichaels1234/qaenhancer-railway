namespace backend.Models;

/// <summary>
/// Request model for AI bug detection
/// </summary>
public class BugDetectionRequest
{
    public string? Code { get; set; }
    public string? Url { get; set; }
    public string? Language { get; set; } // e.g., "csharp", "javascript", "typescript"
    public string? AnalysisType { get; set; } // "code" or "url"
}

/// <summary>
/// Response model for AI bug detection
/// </summary>
public class BugDetectionResponse
{
    public bool Success { get; set; }
    public List<DetectedBug> Bugs { get; set; } = new();
    public string? Summary { get; set; }
    public DateTime AnalysisTime { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Represents a detected bug
/// </summary>
public class DetectedBug
{
    public string Severity { get; set; } = string.Empty; // "Critical", "High", "Medium", "Low"
    public string Type { get; set; } = string.Empty; // "Security", "Performance", "Logic", "Syntax", etc.
    public string Description { get; set; } = string.Empty;
    public string? Location { get; set; } // Line number or URL section
    public string? Suggestion { get; set; } // How to fix it
    public string? CodeSnippet { get; set; } // The problematic code
}
