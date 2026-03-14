using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.Image;

public class StabilityImageService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<StabilityImageService> _logger;
    private readonly string _cacheDir;
    private readonly Random _random = new();

    private const int MaxRetries = 3;
    private const int BaseRetryDelayMs = 3000;

    private static readonly string[] PromptVariations =
    {
        "cinematic lighting, high detail, 8k quality",
        "dramatic atmosphere, ultra realistic, masterpiece",
        "volumetric fog, epic composition, photorealistic",
        "moody lighting, sharp focus, professional quality",
        "atmospheric perspective, stunning detail, award winning",
        "ray tracing, hyper detailed, concept art quality"
    };

    public StabilityImageService(HttpClient httpClient, ILogger<StabilityImageService> logger, string? cacheDir = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _cacheDir = cacheDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "images");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<List<string>> GenerateImagesAsync(string basePrompt, int count, int width, int height,
        string? model = null, int? maxUniqueImages = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating {Count} images with prompt: {Prompt}", count, basePrompt);
        
        var tasks = new List<Task<string>>();
        using var semaphore = new SemaphoreSlim(3); // Max 3 concurrent requests

        for (int i = 0; i < count; i++)
        {
            var currentIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var variation = PromptVariations[_random.Next(PromptVariations.Length)];
                    var prompt = $"{basePrompt}, {variation}, scene {currentIndex + 1} of {count}";
                    var seed = _random.Next(1, 999999999);

                    var imagePath = await GenerateSingleImageAsync(prompt, width, height, seed, cancellationToken);
                    _logger.LogInformation("Generated image {Index}/{Total}: {Path}", currentIndex + 1, count, imagePath);
                    return imagePath;
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        var results = await Task.WhenAll(tasks);
        return results.ToList();
    }

    public async Task<string> GenerateSingleImageAsync(string prompt, int width, int height, int? seed = null, CancellationToken cancellationToken = default)
    {
        var actualSeed = seed ?? _random.Next(1, 999999999);

        // Check cache first
        var cacheKey = $"{prompt}_{width}x{height}_{actualSeed}".GetHashCode().ToString("X8");
        var cachedPath = Path.Combine(_cacheDir, $"{cacheKey}.png");
        if (File.Exists(cachedPath))
        {
            _logger.LogDebug("Image cache hit: {Path}", cachedPath);
            return cachedPath;
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Stability AI API call
                var requestBody = new
                {
                    text_prompts = new[]
                    {
                        new { text = prompt, weight = 1.0 }
                    },
                    cfg_scale = 7,
                    height,
                    width,
                    samples = 1,
                    steps = 30,
                    seed = actualSeed
                };

                var json = JsonSerializer.Serialize(requestBody);
                var content = new StringContent(json, Encoding.UTF8, "application/json");

                _logger.LogInformation("Stability AI attempt {Attempt}/{Max}: requesting image...", attempt, MaxRetries);
                var response = await _httpClient.PostAsync(
                    "https://api.stability.ai/v1/generation/stable-diffusion-xl-1024-v1-0/text-to-image",
                    content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<StabilityResponse>(cancellationToken: cancellationToken);
                    if (result?.Artifacts?.Length > 0)
                    {
                        var imageBytes = Convert.FromBase64String(result.Artifacts[0].Base64);
                        await File.WriteAllBytesAsync(cachedPath, imageBytes, cancellationToken);
                        return cachedPath;
                    }
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Stability API failed attempt {Attempt}: {Status} - {Error}", attempt, response.StatusCode, errorContent.Length > 200 ? errorContent[..200] : errorContent);
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Stability AI attempt {Attempt} timed out.", attempt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Stability AI attempt {Attempt} generic error.", attempt);
            }

            if (attempt < MaxRetries)
            {
                var delay = BaseRetryDelayMs * attempt;
                _logger.LogInformation("Retrying Stability AI in {Delay}ms...", delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new Exception($"Failed to generate image via Stability AI after {MaxRetries} attempts. Please check your API key and connection.");
    }

    private class StabilityResponse
    {
        public StabilityArtifact[]? Artifacts { get; set; }
    }

    public Task<string> GenerateSingleImageAsync(string prompt, int width, int height, int? seed = null,
        string? model = null, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException("Use GenerateImagesAsync instead");
    }

    private class StabilityArtifact
    {
        public string Base64 { get; set; } = string.Empty;
        public string FinishReason { get; set; } = string.Empty;
    }
}
