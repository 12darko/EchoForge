using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.SEO;

public class GptSeoService : ISeoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GptSeoService> _logger;
    private readonly IAppSettingsService _appSettingsService;
    private readonly Random _random = new();

    private static readonly string[] TitleVariations =
    {
        "Create a catchy YouTube title",
        "Generate an engaging video title",
        "Write a compelling click-worthy title",
        "Craft a viral-worthy video title"
    };

    public GptSeoService(HttpClient httpClient, ILogger<GptSeoService> logger, IAppSettingsService appSettingsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appSettingsService = appSettingsService;
    }

    public async Task<SeoResult> GenerateSeoAsync(string projectTitle, string templateName, string genre, string language = "English", string? customInstructions = null, string? targetPlatforms = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating SEO via Grok for: {Title}", projectTitle);

        var apiKey = await _appSettingsService.GetSettingAsync("Grok:ApiKey");
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Grok API Key is missing. Falling back to default SEO.");
            return GenerateFallbackSeo(projectTitle, templateName, genre);
        }

        try
        {
            var titleInstruction = TitleVariations[_random.Next(TitleVariations.Length)];
            var prompt = BuildSeoPrompt(projectTitle, templateName, genre, titleInstruction, customInstructions, targetPlatforms);

            var requestBody = new
            {
                model = "grok-beta",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = "You are a YouTube SEO expert. You generate optimized titles, descriptions, tags, and hashtags for music videos. Always respond in valid JSON format."
                    },
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                },
                temperature = 0.8,
                max_tokens = 800,
                response_format = new { type = "json_object" }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);

            var response = await _httpClient.PostAsync("https://api.x.ai/v1/chat/completions", content, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GptResponse>(cancellationToken: cancellationToken);
                var messageContent = result?.Choices?.FirstOrDefault()?.Message?.Content;

                if (!string.IsNullOrEmpty(messageContent))
                {
                    var seoData = JsonSerializer.Deserialize<SeoJsonResponse>(messageContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                    if (seoData != null)
                    {
                        return new SeoResult
                        {
                            Title = TruncateTitle(seoData.Title ?? projectTitle, 60),
                            Description = seoData.Description ?? $"🎵 {projectTitle} | {templateName} style music video",
                            Tags = seoData.Tags ?? GenerateFallbackTags(projectTitle, genre),
                            Hashtags = seoData.Hashtags ?? GenerateFallbackHashtags(genre)
                        };
                    }
                }
            }

            _logger.LogWarning("GPT API call failed, using fallback SEO");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO generation failed, using fallback");
        }

        return GenerateFallbackSeo(projectTitle, templateName, genre);
    }

    private static string BuildSeoPrompt(string title, string template, string genre, string titleInstruction, string? customInstructions, string? targetPlatforms)
    {
        var platformsStr = string.IsNullOrWhiteSpace(targetPlatforms) ? "YouTube Shorts, TikTok, Instagram Reels" : targetPlatforms;
        var customInstrStr = string.IsNullOrWhiteSpace(customInstructions) ? "None." : customInstructions;

        return $@"{titleInstruction} for a music video. Details:
- Song/Project Title: {title}
- Visual Style: {template}
- Genre: {genre}
- Target Platforms: {platformsStr}
- Custom Additional Instructions: {customInstrStr}

Requirements:
1. Title: Must be under 60 characters, include relevant emoji, be catchy and SEO-optimized
2. Description: 2-3 paragraphs with timestamps, relevant keywords, subscription call-to-action
3. Tags: 15-20 relevant tags for YouTube search
4. Hashtags: 5-8 relevant hashtags

Respond in this exact JSON format:
{{""title"": ""..."", ""description"": ""..."", ""tags"": [""...""], ""hashtags"": [""...""]}}";
    }

    private static string TruncateTitle(string title, int maxLength)
    {
        if (title.Length <= maxLength) return title;
        return title[..(maxLength - 3)] + "...";
    }

    private static List<string> GenerateFallbackTags(string title, string genre)
    {
        return new List<string>
        {
            genre, "music", "music video", title,
            $"{genre} music", "new music", "official video",
            "2024", "trending", "viral", "beats",
            $"{genre} beats", "music visualization",
            "ai generated", "echoforge"
        };
    }

    private static List<string> GenerateFallbackHashtags(string genre)
    {
        return new List<string>
        {
            $"#{genre}", "#Music", "#MusicVideo",
            "#NewMusic", "#Trending", "#Viral"
        };
    }

    private static SeoResult GenerateFallbackSeo(string title, string template, string genre)
    {
        return new SeoResult
        {
            Title = TruncateTitle($"🎵 {title} | {template} Music Video", 60),
            Description = $"🎵 {title}\n\n{template} style music video powered by EchoForge AI.\n\n🔔 Subscribe for more {genre} content!\n\n#Music #{genre} #MusicVideo",
            Tags = GenerateFallbackTags(title, genre),
            Hashtags = GenerateFallbackHashtags(genre)
        };
    }

    // GPT API response models
    private class GptResponse
    {
        [JsonPropertyName("choices")]
        public GptChoice[]? Choices { get; set; }
    }

    private class GptChoice
    {
        [JsonPropertyName("message")]
        public GptMessage? Message { get; set; }
    }

    private class GptMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private class SeoJsonResponse
    {
        public string? Title { get; set; }
        public string? Description { get; set; }
        public List<string>? Tags { get; set; }
        public List<string>? Hashtags { get; set; }
    }
}
