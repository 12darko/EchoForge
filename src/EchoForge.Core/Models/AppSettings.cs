using System.ComponentModel.DataAnnotations;

namespace EchoForge.Core.Models;

public class AppSetting
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Key { get; set; } = string.Empty;

    [MaxLength(5000)]
    public string Value { get; set; } = string.Empty;

    public bool IsEncrypted { get; set; } = false;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
