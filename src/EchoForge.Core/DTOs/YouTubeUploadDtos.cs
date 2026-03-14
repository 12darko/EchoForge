namespace EchoForge.Core.DTOs;

public class YouTubeUploadRequest
{
    public int ProjectId { get; set; }
    public string VideoFilePath { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public string CategoryId { get; set; } = "10"; // Music category
    public string PrivacyStatus { get; set; } = "public"; // public, unlisted, private
    public DateTime? ScheduledPublishAt { get; set; }
}

public class YouTubeUploadResult
{
    public bool Success { get; set; }
    public string? VideoId { get; set; }
    public string? VideoUrl { get; set; }
    public string? ErrorMessage { get; set; }
}
