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
        string? model = null, int? maxUniqueImages = null, CancellationToken cancellationToken = default)
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
            tasks.Add(GenerateSingleImageInternalAsync(variedPrompt, width, height, apiKey, cancellationToken));
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
        string? model = null, CancellationToken cancellationToken = default)
    {
        var apiKey = await _appSettingsService.GetSettingAsync("Gemini:ApiKey");
        if (string.IsNullOrEmpty(apiKey))
        {
            throw new Exception("Gemini API Key is missing. Please add your token via Settings.");
        }

        return await GenerateSingleImageInternalAsync(prompt, width, height, apiKey, cancellationToken);
    }

    private async Task<string> GenerateSingleImageInternalAsync(string prompt, int width, int height, string apiKey, CancellationToken cancellationToken)
    {
        string endpoint = $"https://generativelanguage.googleapis.com/v1beta/models/gemini-2.5-flash:generateContent?key={apiKey}";
        
        var requestBody = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = $"Generate an image: {prompt}" } } }
            },
            generationConfig = new
            {
                temperature = 0.7
            }
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content, cancellationToken);
        var responseContent = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("Gemini API returned {StatusCode}: {Error}", response.StatusCode, responseContent);
            throw new Exception($"Gemini image generation failed: {response.StatusCode} - {responseContent}");
        }

        // Parse Gemini response
        using var document = JsonDocument.Parse(responseContent);
        var root = document.RootElement;
        
        // Search for inlineData base64 image in the response parts
        var candidates = root.GetProperty("candidates");
        if (candidates.GetArrayLength() > 0)
        {
            var parts = candidates[0].GetProperty("content").GetProperty("parts");
            foreach (var part in parts.EnumerateArray())
            {
                if (part.TryGetProperty("inlineData", out var inlineData))
                {
                    string mimeType = inlineData.GetProperty("mimeType").GetString() ?? "image/jpeg";
                    string b64 = inlineData.GetProperty("data").GetString() ?? "";
                    
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
        }
        
        // Fallback for cases where Gemini API structure might be different or no image was generated
        _logger.LogError("No inlineData image found in Gemini response: {Response}", responseContent);
        throw new Exception("Gemini 2.5 Flash API returned success but no image inlineData was found in the response parts.");
    }
}
