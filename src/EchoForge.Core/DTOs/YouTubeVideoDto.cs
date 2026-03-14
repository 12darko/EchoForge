using System;

namespace EchoForge.Core.DTOs;

public class YouTubeVideoDto
{
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ThumbnailUrl { get; set; } = string.Empty;
    public DateTime? PublishedAt { get; set; }
    public ulong? ViewCount { get; set; }
    public ulong? LikeCount { get; set; }
    public ulong? CommentCount { get; set; }
    public string PrivacyStatus { get; set; } = string.Empty;
}
