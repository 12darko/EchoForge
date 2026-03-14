namespace EchoForge.Core.Interfaces;

public interface IImageGenerationService
{
    Task<List<string>> GenerateImagesAsync(string basePrompt, int count, int width, int height,
        string? model = null, int? maxUniqueImages = null, CancellationToken cancellationToken = default);
    Task<string> GenerateSingleImageAsync(string prompt, int width, int height, int? seed = null,
        string? model = null, CancellationToken cancellationToken = default);
}
