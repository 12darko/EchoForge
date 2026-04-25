using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using EchoForge.Infrastructure.Services.Audio;
using EchoForge.Infrastructure.Services.Image;
using EchoForge.Infrastructure.Services.SEO;
using EchoForge.Infrastructure.Services.Video;
using EchoForge.Infrastructure.Services.YouTube;
using Microsoft.Extensions.Logging.Abstractions;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace EchoForge.WPF.Services;

public class ClientJobOrchestrator
{
    private readonly ApiClient _apiClient;
    private readonly HttpClient _httpClient;

    public ClientJobOrchestrator(ApiClient apiClient)
    {
        _apiClient = apiClient;
        _httpClient = new HttpClient();
    }

    private class SettingConfig
    {
        public string FFmpegPath { get; set; } = "ffmpeg";
        public string? HuggingFaceKey { get; set; }
        public string? GeminiKey { get; set; }
        public string? GrokKey { get; set; }
        public int VideoFps { get; set; } = 30;
        public string OutputDir { get; set; } = "";
        public string? IntroVideoPath { get; set; }
        public string? OutroVideoPath { get; set; }
        public string SeoLanguage { get; set; } = "English";
    }

    private class DummySettingsService : EchoForge.Core.Interfaces.IAppSettingsService
    {
        private readonly string _settingKey;
        private readonly string _settingValue;
        public DummySettingsService(string key, string value) { _settingKey = key; _settingValue = value; }
        public Task<string?> GetSettingAsync(string key) => Task.FromResult(key == _settingKey ? _settingValue : null);
        public Task UpdateSettingAsync(string key, string value) => Task.CompletedTask;
        public Task<List<EchoForge.Core.Models.AppSetting>> GetAllSettingsAsync(bool decrypt) => Task.FromResult(new List<EchoForge.Core.Models.AppSetting>());
    }

    private async Task<SettingConfig> GetSettingsAsync()
    {
        var settingsList = await _apiClient.GetAllSettingsAsync(true); // Attempt to fetch all keys
        var config = new SettingConfig();

        if (settingsList != null)
        {
            config.FFmpegPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            config.HuggingFaceKey = settingsList.FirstOrDefault(s => s.Key == "HuggingFace:ApiKey")?.Value;
            config.GeminiKey = settingsList.FirstOrDefault(s => s.Key == "Gemini:ApiKey")?.Value;
            config.GrokKey = settingsList.FirstOrDefault(s => s.Key == "Groq:ApiKey")?.Value;
            config.VideoFps = int.TryParse(settingsList.FirstOrDefault(s => s.Key == "Video:Fps")?.Value, out var fps) ? fps : 30;
            
            var defaultDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Publishing");
            config.OutputDir = settingsList.FirstOrDefault(s => s.Key == "Output:Directory")?.Value ?? defaultDir;
            
            config.IntroVideoPath = settingsList.FirstOrDefault(s => s.Key == "Branding:IntroVideoPath")?.Value;
            config.OutroVideoPath = settingsList.FirstOrDefault(s => s.Key == "Branding:OutroVideoPath")?.Value;
            config.SeoLanguage = settingsList.FirstOrDefault(s => s.Key == "Seo:Language")?.Value ?? "English";
        }
        return config;
    }

    public async Task StartPipelineAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await _apiClient.GetProjectAsync(projectId);
        if (project == null) return;
        
        string audioFilePath = project.AudioPath;
        if (string.IsNullOrEmpty(audioFilePath) || !File.Exists(audioFilePath))
        {
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.Failed, "Local audio file not found.");
            return;
        }

        try
        {
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.Analyzing);
            var config = await GetSettingsAsync();
            
            var audioService = new AudioAnalysisService(NullLogger<AudioAnalysisService>.Instance, config.FFmpegPath);
            
            // Audio Analysis
            string currentAudioPath = audioFilePath;
            if (project.ExtractAutoShorts)
            {
                currentAudioPath = await audioService.ExtractBestPartAsync(currentAudioPath, 60, config.FFmpegPath, cancellationToken);
                project.AudioPath = currentAudioPath; // Update local reference
            }

            var analysis = await audioService.AnalyzeAsync(currentAudioPath, config.FFmpegPath, cancellationToken);

            if (project.ManualImageDurationSec.HasValue && project.ManualImageDurationSec.Value > 0)
            {
                var manualSec = project.ManualImageDurationSec.Value;
                analysis.SceneDuration = manualSec;
                analysis.SceneCount = Math.Min(Math.Max(1, (int)Math.Ceiling(analysis.Duration / manualSec)), 100);
            }

            // Ensure scene count respects user's UniqueImageCount selection
            var userRequestedImages = project.UniqueImageCount > 0 ? project.UniqueImageCount : analysis.SceneCount;
            var targetSceneCount = Math.Max(userRequestedImages, analysis.SceneCount);
            if (targetSceneCount < 1) targetSceneCount = 1;

            // Moved UpdateProjectStatusAsync down to be able to access the provider name
            
            string provider = "";
            string modelName = project.ImageModel;

            if (project.ImageModel != null && project.ImageModel.Contains(":"))
            {
                var parts = project.ImageModel.Split(':');
                provider = parts[0];
                modelName = parts[1];
            }
            else
            {
                if (project.ImageModel == "gemini-2.5-flash") provider = "gemini";
                else if (project.ImageModel == "local" || project.ImageModel == "comfyui") provider = "comfyui";
                else if (!string.IsNullOrEmpty(config.HuggingFaceKey)) provider = "huggingface";
                else provider = "pollinations";
            }

            if (provider == "comfyui" && (modelName == "local" || modelName == "comfyui" || string.IsNullOrEmpty(modelName)))
            {
                modelName = "sd_xl_base_1.0.safetensors";
            }

            // Generate Images
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.GeneratingImages, $"Generating {targetSceneCount} images via {provider} ({modelName})...");

            IImageGenerationService imageService;
            bool usingFallback = false;
            
            if (provider == "gemini")
            {
                if (string.IsNullOrEmpty(config.GeminiKey))
                {
                    throw new Exception("Gemini API key is missing. Please configure it in settings.");
                }
                var geminiClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                var dummySettingsGemini = new DummySettingsService("Gemini:ApiKey", config.GeminiKey);
                imageService = new GeminiImageService(geminiClient, NullLogger<GeminiImageService>.Instance, dummySettingsGemini);
            }
            else if (provider == "huggingface" && !string.IsNullOrEmpty(config.HuggingFaceKey))
            {
                var hfClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                hfClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.HuggingFaceKey);
                var dummySettingsHf = new DummySettingsService("HuggingFace:ApiKey", config.HuggingFaceKey);
                imageService = new HuggingFaceImageService(hfClient, NullLogger<HuggingFaceImageService>.Instance, dummySettingsHf);
            }
            else if (provider == "comfyui" || provider == "local")
            {
                var comfyClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
                imageService = new ComfyUIImageService(comfyClient);
            }
            else
            {
                // Pollinations is the fallback or explicit choice
                var pollClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                imageService = new PollinationsImageService(pollClient, NullLogger<PollinationsImageService>.Instance);
                usingFallback = true;
            }

            var renderSettings = VideoRenderSettings.FromFormatType(project.FormatType, 0, 0);
            renderSettings.FPS = config.VideoFps;

            // Simplified prompt generation for client side
            var basePrompt = $"{project.Title}, cinematic lighting, high quality";
            if (!string.IsNullOrWhiteSpace(project.ImageStyle)) basePrompt = $"{project.ImageStyle} style, {basePrompt}";

            int aiWidth = renderSettings.Width;
            int aiHeight = renderSettings.Height;

            List<string> imagePaths;
            try
            {
                imagePaths = await imageService.GenerateImagesAsync(
                    basePrompt, targetSceneCount, aiWidth, aiHeight,
                    modelName, targetSceneCount, null, cancellationToken);
            }
            catch (Exception imgEx) when (!usingFallback)
            {
                // HuggingFace/Gemini failed → fallback to Pollinations (free, no key)
                var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orchestrator_errors.txt");
                File.AppendAllText(logPath, $"[{DateTime.Now}] Primary image service failed, falling back to Pollinations.ai: {imgEx.Message}\n\n");
                
                var pollClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
                var fallbackService = new PollinationsImageService(pollClient, NullLogger<PollinationsImageService>.Instance);
                imagePaths = await fallbackService.GenerateImagesAsync(
                    basePrompt, targetSceneCount, aiWidth, aiHeight,
                    "flux", targetSceneCount, null, cancellationToken);
            }
                
            // Timeline Json creation
            var sceneDuration = Math.Min(analysis.Duration, renderSettings.MaxDurationSeconds) / imagePaths.Count;
            var transitionStyle = !string.IsNullOrEmpty(project.TransitionStyle) ? project.TransitionStyle : "none";
            
            var timelineItems = new List<TimelineItemDto>();
            for (int i = 0; i < imagePaths.Count; i++)
            {
                timelineItems.Add(new TimelineItemDto
                {
                    SceneNumber = i + 1,
                    Duration = sceneDuration,
                    ImagePath = imagePaths[i],
                    Transition = transitionStyle,
                    Prompt = basePrompt
                });
            }
            
            project.BPM = analysis.BPM;
            project.Duration = analysis.Duration;
            project.SceneCount = analysis.SceneCount;
            project.SceneDuration = analysis.SceneDuration;
            project.TimelineJson = System.Text.Json.JsonSerializer.Serialize(timelineItems);
            project.Status = ProjectStatus.ReviewingScenes;
            
            await _apiClient.ClientUpdateAsync(projectId, project);
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orchestrator_errors.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now}] StartPipelineAsync Error ({projectId}): {ex}\n\n");
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.Failed, ex.Message);
        }
    }

    public async Task<string> GenerateSingleImageAsync(int projectId, string prompt, string? overrideImageModel = null, int? customWidth = null, int? customHeight = null, Action<int, string>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var project = await _apiClient.GetProjectAsync(projectId);
        if (project == null) throw new Exception("Project not found.");

        var config = await GetSettingsAsync();

        string provider = "";
        string modelName = overrideImageModel ?? project.ImageModel;

        if (modelName != null && modelName.Contains(":"))
        {
            var parts = modelName.Split(':');
            provider = parts[0];
            modelName = parts[1];
        }
        else
        {
            if (modelName == "gemini-2.5-flash") provider = "gemini";
            else if (modelName == "local" || modelName == "comfyui") provider = "comfyui";
            else if (!string.IsNullOrEmpty(config.HuggingFaceKey)) provider = "huggingface";
            else provider = "pollinations";
        }

        if (provider == "comfyui" && (modelName == "local" || modelName == "comfyui" || string.IsNullOrEmpty(modelName)))
        {
            modelName = "sd_xl_base_1.0.safetensors";
        }

        IImageGenerationService imageService;
        bool usingFallback = false;
        
        if (provider == "gemini")
        {
            if (string.IsNullOrEmpty(config.GeminiKey)) throw new Exception("Gemini API key is missing. Please configure it in settings.");
            var geminiClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
            var dummySettingsGemini = new DummySettingsService("Gemini:ApiKey", config.GeminiKey);
            imageService = new GeminiImageService(geminiClient, NullLogger<GeminiImageService>.Instance, dummySettingsGemini);
        }
        else if (provider == "huggingface" && !string.IsNullOrEmpty(config.HuggingFaceKey))
        {
            var hfClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            hfClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.HuggingFaceKey);
            var dummySettingsHf = new DummySettingsService("HuggingFace:ApiKey", config.HuggingFaceKey);
            imageService = new HuggingFaceImageService(hfClient, NullLogger<HuggingFaceImageService>.Instance, dummySettingsHf);
        }
        else if (provider == "comfyui" || provider == "local")
        {
            var comfyClient = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            imageService = new ComfyUIImageService(comfyClient);
        }
        else
        {
            var pollClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            imageService = new PollinationsImageService(pollClient, NullLogger<PollinationsImageService>.Instance);
            usingFallback = true;
        }

        var renderSettings = VideoRenderSettings.FromFormatType(project.FormatType, customWidth, customHeight);
        int aiWidth = renderSettings.Width;
        int aiHeight = renderSettings.Height;

        try
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orchestrator_errors.txt");
            _ = File.AppendAllTextAsync(logPath, $"[{DateTime.Now}] Editor: Regenerating image with {provider} ({modelName})\n");
            
            var imagePaths = await imageService.GenerateImagesAsync(
                prompt, 1, aiWidth, aiHeight,
                modelName, 1, progressCallback, cancellationToken);
                
            return imagePaths.FirstOrDefault() ?? "";
        }
        catch (Exception imgEx) when (!usingFallback)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orchestrator_errors.txt");
            _ = File.AppendAllTextAsync(logPath, $"[{DateTime.Now}] Editor Regenerate Service failed, falling back to Pollinations.ai: {imgEx.Message}\n");
            
            var pollClient = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
            var fallbackService = new PollinationsImageService(pollClient, NullLogger<PollinationsImageService>.Instance);
            var imagePaths = await fallbackService.GenerateImagesAsync(
                prompt, 1, aiWidth, aiHeight,
                "flux", 1, null, cancellationToken);
            return imagePaths.FirstOrDefault() ?? "";
        }
    }

    public async Task ResumePipelineAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await _apiClient.GetProjectAsync(projectId);
        if (project == null || string.IsNullOrEmpty(project.TimelineJson)) return;

        try
        {
            var config = await GetSettingsAsync();
            
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.ComposingVideo);
            
            var timelineItems = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(project.TimelineJson) ?? new();
            var imagePaths = timelineItems.Select(t => t.ImagePath).ToList();
            var renderSettings = VideoRenderSettings.FromFormatType(project.FormatType, 0, 0);
            renderSettings.FPS = config.VideoFps;

            var videoService = new VideoComposerService(NullLogger<VideoComposerService>.Instance, config.FFmpegPath, config.OutputDir);

            var videoResult = await videoService.ComposeVideoAsync(
                imagePaths,
                project.AudioPath,
                renderSettings,
                project.TransitionStyle ?? "none",
                project.VisualEffect,
                null,
                config.OutputDir,
                config.IntroVideoPath,
                config.OutroVideoPath,
                async (progressPercent) => await _apiClient.UpdateProjectProgressAsync(projectId, progressPercent),
                cancellationToken,
                timelineItems);

            project.OutputVideoPath = videoResult.VideoFilePath;
            project.TimelineJson = videoResult.TimelineJson;
            project.Status = ProjectStatus.GeneratingSEO;
            await _apiClient.ClientUpdateAsync(projectId, project);

            // SEO Generation
            if (!string.IsNullOrEmpty(config.GrokKey))
            {
                var groqClient = new HttpClient { Timeout = TimeSpan.FromMinutes(2) };
                var seoService = new GroqSeoService(groqClient, NullLogger<GroqSeoService>.Instance, config.GrokKey);
                var seo = await seoService.GenerateSeoAsync(
                    project.Title, "Music Video", "General", config.SeoLanguage,
                    project.CustomInstructions, project.TargetPlatforms, cancellationToken);
                    
                project.SeoTitle = seo.Title;
                project.SeoDescription = seo.Description;
                project.SeoTags = string.Join(",", seo.Tags);
                project.SeoHashtags = string.Join(" ", seo.Hashtags);
            }
            
            project.Status = ProjectStatus.AwaitingApproval;
            project.PipelineProgress = 100;
            await _apiClient.ClientUpdateAsync(projectId, project);
        }
        catch (Exception ex)
        {
            var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "orchestrator_errors.txt");
            File.AppendAllText(logPath, $"[{DateTime.Now}] ResumePipelineAsync Error ({projectId}): {ex}\n\n");
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.Failed, ex.Message);
        }
    }
}
