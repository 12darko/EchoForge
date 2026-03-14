using EchoForge.Core.Models;

namespace EchoForge.Core.DTOs;

public class ProjectDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public double? BPM { get; set; }
    public double? Duration { get; set; }
    public int? SceneCount { get; set; }
    public double? SceneDuration { get; set; }
    public FormatType FormatType { get; set; }
    public bool ExtractAutoShorts { get; set; }
    public string ImageModel { get; set; } = "flux";
    public int UniqueImageCount { get; set; } = 8;
    public string ImageStyle { get; set; } = string.Empty;
    public double? ManualImageDurationSec { get; set; }
    public string? TransitionStyle { get; set; }
    public string? VisualEffect { get; set; }
    public ProjectStatus Status { get; set; }
    public int? PipelineProgress { get; set; }
    public string PrivacyStatus { get; set; } = "private";
    public string? OutputVideoPath { get; set; }
    public string? YouTubeVideoId { get; set; }
    public string? CustomInstructions { get; set; }
    public string? TargetPlatforms { get; set; }
    public int? TargetChannelId { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoTags { get; set; }
    public string? SeoHashtags { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TimelineJson { get; set; }
    
    // Virtual property for UI usage
    public System.Collections.ObjectModel.ObservableCollection<TimelineItemDto> Scenes { get; set; } = new();
}

public class TimelineItemDto
{
    public int SceneNumber { get; set; }
    public double Duration { get; set; }
    public string DurationStr => $"{Duration:F1}s";
    public string Prompt { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    public string Transition { get; set; } = string.Empty;
    
    // Visual effects per scene
    public double FadeInDuration { get; set; } = 0;
    public double FadeOutDuration { get; set; } = 0;
    public double Speed { get; set; } = 1.0;
    public string Filter { get; set; } = "none";
}
