using EchoForge.Core.Models;

namespace EchoForge.Core.DTOs;

public class CreateProjectRequest
{
    public string Title { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public FormatType FormatType { get; set; } = FormatType.Vertical_9x16;
    public bool ExtractAutoShorts { get; set; } = false;
    public string PrivacyStatus { get; set; } = "private";
    public int? CustomWidth { get; set; }
    public int? CustomHeight { get; set; }
    public string? TransitionStyle { get; set; }
    public string? VisualEffect { get; set; }

    /// <summary>Pollinations AI model: flux, turbo, flux-realism</summary>
    public string ImageModel { get; set; } = "flux";

    /// <summary>Number of unique AI images to generate (1-20), distributed across scenes</summary>
    public int UniqueImageCount { get; set; } = 8;

    /// <summary>Manual image duration in seconds. If null, calculates automatically based on beat/duration.</summary>
    public double? ManualImageDurationSec { get; set; }

    /// <summary>Visual style for AI images: anime, cyberpunk, watercolor, realistic photo, etc.</summary>
    public string ImageStyle { get; set; } = string.Empty;

    public string? CustomInstructions { get; set; }
    public string? TargetPlatforms { get; set; }

    /// <summary>Which YouTube channel this project will be uploaded to (optional)</summary>
    public int? TargetChannelId { get; set; }
}
