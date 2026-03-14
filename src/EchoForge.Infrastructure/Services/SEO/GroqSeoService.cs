using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.SEO;

/// <summary>
/// Free SEO generation using Groq API (Llama 3.3 70B model).
/// Free tier: ~30 requests/minute, 15,000 tokens/minute.
/// Get free key at: https://console.groq.com
/// Uses OpenAI-compatible API format.
/// </summary>
public class GroqSeoService : ISeoService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GroqSeoService> _logger;
    private readonly IAppSettingsService _appSettingsService;
    private readonly Random _random = new();

    private static readonly string[] TitleVariations =
    {
        "Create a catchy YouTube title",
        "Generate an engaging video title",
        "Write a compelling click-worthy title",
        "Craft a viral-worthy video title"
    };

    public GroqSeoService(HttpClient httpClient, ILogger<GroqSeoService> logger, IAppSettingsService appSettingsService)
    {
        _httpClient = httpClient;
        _logger = logger;
        _appSettingsService = appSettingsService;
    }

    public async Task<SeoResult> GenerateSeoAsync(string projectTitle, string templateName, string genre, string language = "English", string? customInstructions = null, string? targetPlatforms = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating SEO via Groq for: {Title} in {Language}. Platforms: {Plat}", projectTitle, language, targetPlatforms ?? "Default");

        try
        {
            var apiKey = await _appSettingsService.GetSettingAsync("Groq:ApiKey");
            if (string.IsNullOrEmpty(apiKey))
            {
                _logger.LogWarning("Groq API Key is not set in Settings! Falling back to default SEO.");
                return GenerateFallbackSeo(projectTitle, templateName, genre);
            }

            var titleInstruction = TitleVariations[_random.Next(TitleVariations.Length)];
            var prompt = BuildSeoPrompt(projectTitle, templateName, genre, language, titleInstruction, customInstructions, targetPlatforms);

            var requestBody = new
            {
                model = "llama-3.3-70b-versatile",
                messages = new[]
                {
                    new
                    {
                        role = "system",
                        content = $"You are a YouTube SEO expert. You generate optimized titles, descriptions, tags, and hashtags for music videos. Output language: {language}. Always respond in valid JSON format."
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

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.groq.com/openai/v1/chat/completions");
            request.Content = content;
            request.Headers.Add("Authorization", $"Bearer {apiKey}");

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GroqResponse>(cancellationToken: cancellationToken);
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
            else
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogWarning("Groq API call failed with status {Status}: {Error}", response.StatusCode, errorBody);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "SEO generation failed, using fallback");
        }

        return GenerateFallbackSeo(projectTitle, templateName, genre);
    }

    private static string BuildSeoPrompt(string title, string template, string genre, string language, string titleInstruction, string? customInstructions, string? targetPlatforms)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{titleInstruction} for a music video in {language}. Details:");
        sb.AppendLine($"- Song/Project Title: {title}");
        sb.AppendLine($"- Visual Style: {template}");
        sb.AppendLine($"- Genre: {genre}");
        sb.AppendLine($"- Target Language: {language}");
        
        if (!string.IsNullOrWhiteSpace(targetPlatforms))
        {
            sb.AppendLine($"- Target Platforms: {targetPlatforms}");
        }

        if (!string.IsNullOrWhiteSpace(customInstructions))
        {
            sb.AppendLine($"- CUSTOM USER INSTRUCTIONS (CRITICAL): {customInstructions}");
        }

        sb.AppendLine(@"
Requirements:
1. Title: Must be under 60 characters, include relevant emoji, be catchy and SEO-optimized in " + language + @"
2. Description: 2-3 paragraphs with timestamps, relevant keywords, subscription call-to-action in " + language + (string.IsNullOrWhiteSpace(customInstructions) ? "" : " Ensure custom instructions are seamlessly integrated.") + @"
3. Tags: 15-20 relevant tags for search algorithms (mix of " + language + @" and English)
4. Hashtags: 5-8 relevant hashtags" + (string.IsNullOrWhiteSpace(targetPlatforms) ? "" : $" optimize for {targetPlatforms} algorithms.") + @"

Respond in this exact JSON format:
{""title"": ""..."", ""description"": ""..."", ""tags"": [""...""], ""hashtags"": [""...""]}");

        return sb.ToString();
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

    // Groq API response models (OpenAI-compatible format)
    private class GroqResponse
    {
        [JsonPropertyName("choices")]
        public GroqChoice[]? Choices { get; set; }
    }

    private class GroqChoice
    {
        [JsonPropertyName("message")]
        public GroqMessage? Message { get; set; }
    }

    private class GroqMessage
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
