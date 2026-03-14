using System.ComponentModel.DataAnnotations;

namespace EchoForge.Core.Models;

public class Project
{
    public int Id { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    [Required, MaxLength(500)]
    public string AudioPath { get; set; } = string.Empty;

    public int TemplateId { get; set; }
    public Template? Template { get; set; }

    public double? BPM { get; set; }
    public double? Duration { get; set; }
    public int? SceneCount { get; set; }
    public double? SceneDuration { get; set; }

    public FormatType FormatType { get; set; } = FormatType.Vertical_9x16;

    public bool ExtractAutoShorts { get; set; } = false; // Trims the best 60-second highlight

    [MaxLength(50)]
    public string ImageModel { get; set; } = "flux"; // Pollinations model: flux, turbo, flux-realism

    public int UniqueImageCount { get; set; } = 8; // Number of unique AI images to generate (1-20)

    public double? ManualImageDurationSec { get; set; } // If set, overrides automatic scene duration

    [MaxLength(200)]
    public string ImageStyle { get; set; } = string.Empty; // User-defined style: anime, cyberpunk, watercolor, etc.

    public int? CustomWidth { get; set; }
    public int? CustomHeight { get; set; }

    [MaxLength(50)]
    public string? TransitionStyle { get; set; } // e.g. fade, zoompan, slideleft

    [MaxLength(50)]
    public string? VisualEffect { get; set; } // e.g. none, bw, sepia, vhs, cinematic, dreamy

    public ProjectStatus Status { get; set; } = ProjectStatus.Created;

    public int? PipelineProgress { get; set; } // 0 to 100 percentage for Video Composition progress
    
    [MaxLength(20)]
    public string PrivacyStatus { get; set; } = "private"; // public, unlisted, private

    [MaxLength(500)]
    public string? OutputVideoPath { get; set; }

    [MaxLength(50)]
    public string? YouTubeVideoId { get; set; }

    [MaxLength(200)]
    public string? SeoTitle { get; set; }

    [MaxLength(5000)]
    public string? SeoDescription { get; set; }

    [MaxLength(2000)]
    public string? SeoTags { get; set; }

    [MaxLength(1000)]
    public string? SeoHashtags { get; set; }

    [MaxLength(1000)]
    public string? CustomInstructions { get; set; } // Otonom açıklamaya eklenecek kullanıcı notları

    [MaxLength(200)]
    public string? TargetPlatforms { get; set; } // Örn: "YouTube Shorts, TikTok, Instagram Reels"

    public int? TargetChannelId { get; set; } // Which YouTube channel to upload to
    public EchoForge.Core.Entities.YouTubeChannel? TargetChannel { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(8000)] // Store serialized JSON of scenes
    public string? TimelineJson { get; set; }

    public ICollection<UploadLog> UploadLogs { get; set; } = new List<UploadLog>();
}
