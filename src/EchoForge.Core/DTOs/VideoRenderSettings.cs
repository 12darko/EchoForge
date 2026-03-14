using EchoForge.Core.Models;

namespace EchoForge.Core.DTOs;

public class VideoRenderSettings
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int FPS { get; set; } = 30;
    public string Codec { get; set; } = "libx264";
    public int MaxDurationSeconds { get; set; } = 60;
    public FormatType FormatType { get; set; }

    public static VideoRenderSettings FromFormatType(FormatType format, int? customWidth = null, int? customHeight = null)
    {
        return format switch
        {
            FormatType.Vertical_9x16 => new VideoRenderSettings { Width = 1080, Height = 1920, FormatType = format },
            FormatType.Standard_16x9 => new VideoRenderSettings { Width = 1920, Height = 1080, FormatType = format, MaxDurationSeconds = 600 },
            FormatType.Square_1x1 => new VideoRenderSettings { Width = 1080, Height = 1080, FormatType = format },
            FormatType.Custom => new VideoRenderSettings
            {
                Width = customWidth ?? 1080,
                Height = customHeight ?? 1920,
                FormatType = format,
                MaxDurationSeconds = 600
            },
            _ => new VideoRenderSettings { Width = 1080, Height = 1920, FormatType = FormatType.Vertical_9x16 }
        };
    }
}
