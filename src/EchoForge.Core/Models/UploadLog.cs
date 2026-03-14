using System.ComponentModel.DataAnnotations;

namespace EchoForge.Core.Models;

public class UploadLog
{
    public int Id { get; set; }

    public int ProjectId { get; set; }
    public Project? Project { get; set; }

    [MaxLength(50)]
    public string Status { get; set; } = "Pending";

    [MaxLength(10000)]
    public string? ResponseJson { get; set; }

    [MaxLength(2000)]
    public string? ErrorMessage { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
