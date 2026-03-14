using System;
using System.ComponentModel.DataAnnotations;

namespace EchoForge.Core.Entities;

public class YouTubeChannel
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string ChannelName { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(100)]
    public string ChannelId { get; set; } = string.Empty;

    public int UserId { get; set; }

    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? TokenExpiration { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
