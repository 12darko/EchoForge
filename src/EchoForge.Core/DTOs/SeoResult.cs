namespace EchoForge.Core.DTOs;

public class SeoResult
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public List<string> Hashtags { get; set; } = new();
}
