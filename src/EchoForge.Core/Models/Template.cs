using System.ComponentModel.DataAnnotations;

namespace EchoForge.Core.Models;

public class Template
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(1000)]
    public string ImagePromptBase { get; set; } = string.Empty;

    [MaxLength(50)]
    public string Transition { get; set; } = "fade";

    [MaxLength(100)]
    public string OverlayFont { get; set; } = "BebasNeue";

    [MaxLength(50)]
    public string CutMode { get; set; } = "beat";

    [MaxLength(50)]
    public string ColorTheme { get; set; } = "dark";

    [MaxLength(5000)]
    public string? SettingsJson { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Project> Projects { get; set; } = new List<Project>();
}
