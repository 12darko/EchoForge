using System.Text;
using System.Text.Json;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.Image;

public class GeminiImageService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GeminiImageService> _logger;
    private readonly IAppSettingsService _appSettingsService;
    private readonly string _cacheDir;

    public GeminiImageService(HttpClient httpClient, ILogger<GeminiImageService> logger, IAppSettingsService appSettingsService, string? cacheDir = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appSettingsService = appSettingsService;
        _cacheDir = cacheDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "images");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<List<string>> GenerateImagesAsync(string basePrompt, int count, int width, int height,
        string? model = null, int? maxUniqueImages = null, Action<int, string>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await _appSettingsService.GetSettingAsync("Gemini:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("Gemini API Key is missing. Please add your token via Settings.");
        }

        var uniqueCount = Math.Min(count, maxUniqueImages ?? 8);
        uniqueCount = Math.Clamp(uniqueCount, 1, 20);

        _logger.LogInformation("Generating {UniqueCount} unique images via Gemini API (for {TotalScenes} scenes) with prompt: {Prompt}", uniqueCount, count, basePrompt);

        var generatedFiles = new List<string>();
        // Using Task.WhenAll to generate images in parallel
        var tasks = new List<Task<string>>();
        for (int i = 0; i < uniqueCount; i++)
        {
            // Alter prompt slightly to ensure varied images if Gemini caches or generates identical outputs
            string variedPrompt = $"{basePrompt} - variation {i + 1}";
            tasks.Add(GenerateSingleImageInternalAsync(variedPrompt, width, height, apiKey, progressCallback, cancellationToken));
            await Task.Delay(500, cancellationToken); // Slight delay to respect rate limits
        }

        var uniqueImages = await Task.WhenAll(tasks);
        
        // Distribute generated unique images across requested count
        for (int i = 0; i < count; i++)
        {
            generatedFiles.Add(uniqueImages[i % uniqueCount]);
        }

        return generatedFiles;
    }

    public async Task<string> GenerateSingleImageAsync(string prompt, int width, int height, int? seed = null,
        string? model = null, Action<int, string>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await _appSettingsService.GetSettingAsync("Gemini:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("Gemini API Key is missing. Please add your token via Settings.");
        }

        return await GenerateSingleImageInternalAsync(prompt, width, height, apiKey, progressCallback, cancellationToken);
    }

    private async Task<string> GenerateSingleImageInternalAsync(string prompt, int width, int height, string apiKey, Action<int, string>? progressCallback, CancellationToken cancellationToken)
    {
        // Use Imagen 3 for Image Generation via Generative Language API
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/imagen-3.0-generate-001:predict?key={apiKey}";
        
        string aspectRatio = "1:1";
        if (width > height + 200) aspectRatio = "16:9";
        else if (height > width + 200) aspectRatio = "9:16";
        else if (width > height + 100) aspectRatio = "4:3";
        else if (height > width + 100) aspectRatio = "3:4";

        var requestBody = new
        {
            instances = new[]
            {
                new { prompt = $"{prompt}. Cinematic composition, highly detailed, stunning." }
            },
            parameters = new
            {
                sampleCount = 1,
                aspectRatio = aspectRatio,
                outputOptions = new { mimeType = "image/jpeg" }
            }
        };

        var json = System.Text.Json.JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini Imagen API returned {StatusCode}: {Error}", response.StatusCode, responseContent.Length > 300 ? responseContent[..300] : responseContent);
            throw new Exception($"Gemini image generation failed: {response.StatusCode} - {(responseContent.Length > 200 ? responseContent[..200] : responseContent)}");
        }

        // Parse Imagen 3 response
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;
        
        var predictions = root.GetProperty("predictions");
        if (predictions.GetArrayLength() > 0)
        {
            // predictions[0] has 'bytesBase64Encoded' and 'mimeType'
            var prediction = predictions[0];
            if (prediction.TryGetProperty("bytesBase64Encoded", out var dataElement))
            {
                string b64 = dataElement.GetString() ?? "";
                string mimeType = prediction.TryGetProperty("mimeType", out var mimeEl) ? mimeEl.GetString() ?? "image/jpeg" : "image/jpeg";
                
                if (!string.IsNullOrEmpty(b64))
                {
                    var bytes = Convert.FromBase64String(b64);
                    var ext = mimeType.Split('/').LastOrDefault() ?? "jpeg";
                    var fileName = $"gemini_{Guid.NewGuid():N}.{ext}";
                    var outputPath = Path.Combine(_cacheDir, fileName);
                    await File.WriteAllBytesAsync(outputPath, bytes, cancellationToken);
                    return outputPath;
                }
            }
        }
        
        throw new Exception("Gemini Imagen API succeeded but returned no image data.");
    }
}
