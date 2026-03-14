using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.Image;

/// <summary>
/// Free image generation using Pollinations.ai — no API key required.
/// Endpoint: GET https://image.pollinations.ai/prompt/{prompt}?width=W&amp;height=H&amp;seed=S&amp;model=M&amp;nologo=true
/// </summary>
public class PollinationsImageService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<PollinationsImageService> _logger;
    private readonly string _cacheDir;
    private readonly Random _random = new();

    /// <summary>Available Pollinations models</summary>
    public static readonly string[] AvailableModels = { "flux", "turbo", "flux-realism" };

    private static readonly string[] PromptVariations =
    {
        "cinematic lighting, high detail, 8k quality",
        "dramatic atmosphere, ultra realistic, masterpiece",
        "volumetric fog, epic composition, photorealistic",
        "moody lighting, sharp focus, professional quality",
        "atmospheric perspective, stunning detail, award winning",
        "ray tracing, hyper detailed, concept art quality",
        "golden hour lighting, dreamlike, ethereal glow",
        "neon lights, cyberpunk mood, high contrast"
    };

    private const int MaxRetries = 5;
    private const int BaseRetryDelayMs = 5000;
    private const int DefaultMaxUniqueImages = 8;

    public PollinationsImageService(HttpClient httpClient, ILogger<PollinationsImageService> logger, string? cacheDir = null)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(3);

        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.Add("User-Agent",
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36");
        }

        _logger = logger;
        _cacheDir = cacheDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "images");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<List<string>> GenerateImagesAsync(string basePrompt, int count, int width, int height,
        string? model = null, int? maxUniqueImages = null, CancellationToken cancellationToken = default)
    {
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? "flux" : model;
        var uniqueCount = Math.Min(count, maxUniqueImages ?? DefaultMaxUniqueImages);
        uniqueCount = Math.Clamp(uniqueCount, 1, 20);

        _logger.LogInformation(
            "Generating {UniqueCount} unique images via Pollinations.ai (model={Model}, for {TotalScenes} scenes) with prompt: {Prompt}",
            uniqueCount, effectiveModel, count, basePrompt);

        var uniqueImages = new string[uniqueCount];
        var tasks = new List<Task>();
        using var semaphore = new SemaphoreSlim(3); // Allow 3 parallel requests

        for (int i = 0; i < uniqueCount; i++)
        {
            var currentIndex = i;
            tasks.Add(Task.Run(async () =>
            {
                await semaphore.WaitAsync(cancellationToken);
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var variation = PromptVariations[currentIndex % PromptVariations.Length];
                    var prompt = $"{basePrompt}, {variation}";
                    var seed = _random.Next(1, 999999999);

                    var imagePath = await GenerateSingleImageAsync(prompt, width, height, seed, effectiveModel, cancellationToken);
                    uniqueImages[currentIndex] = imagePath;

                    _logger.LogInformation("Generated unique image {Index}/{Total}: {Path}", currentIndex + 1, uniqueCount, imagePath);
                }
                finally
                {
                    semaphore.Release();
                }
            }, cancellationToken));
        }

        await Task.WhenAll(tasks);

        // Distribute unique images across all scenes (round-robin)
        var imagePaths = new List<string>();
        for (int i = 0; i < count; i++)
        {
            imagePaths.Add(uniqueImages[i % uniqueImages.Length]);
        }

        _logger.LogInformation("Distributed {UniqueCount} unique images across {TotalScenes} scenes", uniqueImages.Length, count);
        return imagePaths;
    }

    public async Task<string> GenerateSingleImageAsync(string prompt, int width, int height,
        int? seed = null, string? model = null, CancellationToken cancellationToken = default)
    {
        var actualSeed = seed ?? _random.Next(1, 999999999);
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? "flux" : model;

        // Check cache first
        var cacheKey = $"{prompt}_{width}x{height}_{actualSeed}_{effectiveModel}".GetHashCode().ToString("X8");
        var cachedPath = Path.Combine(_cacheDir, $"{cacheKey}.png");
        if (File.Exists(cachedPath))
        {
            _logger.LogDebug("Image cache hit: {Path}", cachedPath);
            return cachedPath;
        }

        var encodedPrompt = Uri.EscapeDataString(prompt);

        // Model fallback order: requested model → flux → no model parameter
        var modelsToTry = new List<string> { effectiveModel };
        if (effectiveModel != "flux") modelsToTry.Add("flux");
        modelsToTry.Add(""); // Empty = let API choose default

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // Cycle through model fallbacks on failures
            var tryModel = modelsToTry[Math.Min(attempt - 1, modelsToTry.Count - 1)];
            var modelParam = string.IsNullOrEmpty(tryModel) ? "" : $"&model={tryModel}";

            var url = $"https://image.pollinations.ai/prompt/{encodedPrompt}?width={width}&height={height}&seed={actualSeed}{modelParam}&nologo=true&nofeed=true";

            try
            {
                _logger.LogInformation("Pollinations attempt {Attempt}/{Max} (model={Model}): requesting image...",
                    attempt, MaxRetries, string.IsNullOrEmpty(tryModel) ? "default" : tryModel);

                var response = await _httpClient.GetAsync(url, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);

                    if (imageBytes.Length > 1000)
                    {
                        await File.WriteAllBytesAsync(cachedPath, imageBytes, cancellationToken);
                        _logger.LogInformation("Image saved ({Size} KB): {Path}", imageBytes.Length / 1024, cachedPath);
                        return cachedPath;
                    }

                    _logger.LogWarning("Pollinations returned tiny response ({Size} bytes), retrying...", imageBytes.Length);
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    _logger.LogWarning("Pollinations attempt {Attempt} failed: {Status} — {Error}",
                        attempt, (int)response.StatusCode, errorContent.Length > 200 ? errorContent[..200] : errorContent);
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Pollinations attempt {Attempt} timed out", attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Pollinations attempt {Attempt} network error", attempt);
            }

            if (attempt < MaxRetries)
            {
                // Exponential backoff: 5s, 10s, 15s, 20s
                var delay = BaseRetryDelayMs * attempt;
                _logger.LogInformation("Retrying in {Delay}ms...", delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new Exception($"Failed to generate image via Pollinations AI (model={effectiveModel}) after {MaxRetries} attempts. The API may be temporarily overloaded.");
    }
}
