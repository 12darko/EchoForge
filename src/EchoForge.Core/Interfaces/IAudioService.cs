using EchoForge.Core.DTOs;

namespace EchoForge.Core.Interfaces;

public interface IAudioService
{
    Task<AudioAnalysisResult> AnalyzeAsync(string audioFilePath, string? ffmpegPath = null, CancellationToken cancellationToken = default);
    Task<string> ConvertToWavAsync(string inputPath, string? ffmpegPath = null, CancellationToken cancellationToken = default);
    Task<string> ExtractBestPartAsync(string inputPath, int durationSec = 60, string? ffmpegPath = null, CancellationToken cancellationToken = default);
}
