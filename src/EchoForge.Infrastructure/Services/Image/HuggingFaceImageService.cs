using System.Text;
using System.Text.Json;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.Image;

public class HuggingFaceImageService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<HuggingFaceImageService> _logger;
    private readonly IAppSettingsService _appSettingsService;
    private readonly string _cacheDir;
    private readonly Random _random = new();

    private const int MaxRetries = 5;
    private const int BaseRetryDelayMs = 4000;
    private const int DefaultMaxUniqueImages = 8;

    private static readonly string[] PromptVariations =
    {
        "cinematic lighting, high detail, 8k quality",
        "dramatic atmosphere, masterpiece",
        "volumetric fog, epic composition, photorealistic",
        "moody lighting, sharp focus, professional quality",
        "stunning detail, award winning",
        "ray tracing, hyper detailed",
        "neon lights, cyberpunk mood"
    };

    public HuggingFaceImageService(HttpClient httpClient, ILogger<HuggingFaceImageService> logger, IAppSettingsService appSettingsService, string? cacheDir = null)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _logger = logger;
        _appSettingsService = appSettingsService;
        _cacheDir = cacheDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "images");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<List<string>> GenerateImagesAsync(string basePrompt, int count, int width, int height,
        string? model = null, int? maxUniqueImages = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await _appSettingsService.GetSettingAsync("HuggingFace:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("HuggingFace API Key is missing. Please add your token via Settings.");
        }

        var effectiveModel = string.IsNullOrWhiteSpace(model) ? "stabilityai/stable-diffusion-xl-base-1.0" : model;
        // Check if user selected UI standard model and map to HF proper model format, or use SDXL as safe default.
        if (effectiveModel == "flux" || effectiveModel == "turbo" || effectiveModel == "flux-realism") {
            effectiveModel = "stabilityai/stable-diffusion-xl-base-1.0"; // Free and very stable API tier
        }

        var uniqueCount = Math.Min(count, maxUniqueImages ?? DefaultMaxUniqueImages);
        uniqueCount = Math.Clamp(uniqueCount, 1, 20);

        _logger.LogInformation(
            "Generating {UniqueCount} unique images via HuggingFace API (model={Model}, for {TotalScenes} scenes) with prompt: {Prompt}",
            uniqueCount, effectiveModel, count, basePrompt);

        var uniqueImages = new string[uniqueCount];
        var tasks = new List<Task>();
        using var semaphore = new SemaphoreSlim(2); // HuggingFace free tier limits concurrent connections

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

        // Distribute images round-robin to fit exact scene count
        var imagePaths = new List<string>();
        for (int i = 0; i < count; i++)
        {
            imagePaths.Add(uniqueImages[i % uniqueImages.Length]);
        }

        return imagePaths;
    }

    public async Task<string> GenerateSingleImageAsync(string prompt, int width, int height, int? seed = null,
        string? model = null, CancellationToken cancellationToken = default)
    {
        var actualSeed = seed ?? _random.Next(1, 999999999);
        var effectiveModel = string.IsNullOrWhiteSpace(model) ? "stabilityai/stable-diffusion-xl-base-1.0" : model;

        var cacheKey = $"{prompt}_{width}x{height}_{actualSeed}_{effectiveModel}".GetHashCode().ToString("X8");
        var cachedPath = Path.Combine(_cacheDir, $"{cacheKey}.jpg");

        if (File.Exists(cachedPath))
        {
            _logger.LogDebug("Image cache hit: {Path}", cachedPath);
            return cachedPath;
        }

        var url = $"https://router.huggingface.co/hf-inference/models/{effectiveModel}";

        var requestBody = new
        {
            inputs = prompt,
            parameters = new
            {
                width = width,
                height = height,
                seed = actualSeed
            }
        };

        var json = JsonSerializer.Serialize(requestBody);

        var apiKey = await _appSettingsService.GetSettingAsync("HuggingFace:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("HuggingFace API Key is missing. Please add your token via Settings.");
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var content = new StringContent(json, Encoding.UTF8, "application/json");
                _logger.LogInformation("HuggingFace attempt {Attempt}/{Max}: requesting image...", attempt, MaxRetries);
                
                using var request = new HttpRequestMessage(HttpMethod.Post, url);
                request.Content = content;
                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    var imageBytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                    if (imageBytes.Length > 1000)
                    {
                        await File.WriteAllBytesAsync(cachedPath, imageBytes, cancellationToken);
                        _logger.LogInformation("Image saved to cache: {Path}", cachedPath);
                        return cachedPath;
                    }
                    else
                    {
                        _logger.LogWarning("HuggingFace returned tiny response, retrying...");
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                    
                    if (response.StatusCode == System.Net.HttpStatusCode.ServiceUnavailable && errorContent.Contains("is currently loading"))
                    {
                        _logger.LogWarning("HuggingFace Model is loading (Attempt {Attempt}). Will retry after delay.", attempt);
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
                    {
                        _logger.LogWarning("HuggingFace Rate Limited! Waiting before retry.");
                    }
                    else if (response.StatusCode == System.Net.HttpStatusCode.Forbidden && errorContent.Contains("sufficient permissions"))
                    {
                        _logger.LogError("HuggingFace Permission Error! Your token needs 'Make calls to the serverless Inference API' permission.");
                        throw new Exception("Hugging Face API Hatası: Token yetersiz. Hugging Face'te token oluştururken (Fine-grained) 'Make calls to the serverless Inference API' kutucuğunu işaretlemelisiniz.");
                    }
                    else
                    {
                        _logger.LogError("HuggingFace API failed {Status}: {Error}", response.StatusCode, errorContent.Length > 200 ? errorContent[..200] : errorContent);
                    }
                }
            }
            catch (TaskCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("HuggingFace attempt {Attempt} timed out", attempt);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "HuggingFace attempt {Attempt} network error", attempt);
            }

            if (attempt < MaxRetries)
            {
                // Exponential backoff
                var delay = BaseRetryDelayMs * attempt;
                _logger.LogInformation("Retrying HuggingFace in {Delay}ms...", delay);
                await Task.Delay(delay, cancellationToken);
            }
        }

        throw new Exception($"Failed to generate image via HuggingFace API after {MaxRetries} attempts.");
    }

    public Task<string> GenerateSingleImageAsync(string prompt, int width, int height, int? seed = null, CancellationToken cancellationToken = default)
    {
        return GenerateSingleImageAsync(prompt, width, height, seed, null, cancellationToken);
    }
}
