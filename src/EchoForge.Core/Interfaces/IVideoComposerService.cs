using EchoForge.Core.DTOs;

namespace EchoForge.Core.Interfaces;

public class VideoCompositionResult
{
    public string VideoFilePath { get; set; } = string.Empty;
    public string TimelineJson { get; set; } = string.Empty;
}

public interface IVideoComposerService
{
    Task<VideoCompositionResult> ComposeVideoAsync(
        List<string> imagePaths,
        string audioPath,
        VideoRenderSettings settings,
        string transition,
        string? visualEffect = null,
        string? overlayText = null,
        string? outputDirectory = null,
        string? introVideoPath = null,
        string? outroVideoPath = null,
        Action<int>? progressCallback = null,
        CancellationToken cancellationToken = default);
}
