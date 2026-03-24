using EchoForge.Core.DTOs;
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
        public string? GrokKey { get; set; }
        public int VideoFps { get; set; } = 30;
        public string OutputDir { get; set; } = "";
        public string? IntroVideoPath { get; set; }
        public string? OutroVideoPath { get; set; }
        public string SeoLanguage { get; set; } = "English";
    }

    private async Task<SettingConfig> GetSettingsAsync()
    {
        var settingsList = await _apiClient.GetAllSettingsAsync(true); // Attempt to fetch all keys
        var config = new SettingConfig();

        if (settingsList != null)
        {
            config.FFmpegPath = settingsList.FirstOrDefault(s => s.Key == "FFmpeg:Path")?.Value ?? "ffmpeg";
            config.HuggingFaceKey = settingsList.FirstOrDefault(s => s.Key == "HuggingFace:ApiKey")?.Value;
            config.GrokKey = settingsList.FirstOrDefault(s => s.Key == "Grok:ApiKey")?.Value;
            config.VideoFps = int.TryParse(settingsList.FirstOrDefault(s => s.Key == "Video:Fps")?.Value, out var fps) ? fps : 30;
            
            var userDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "EchoForge", "Publishing");
            config.OutputDir = settingsList.FirstOrDefault(s => s.Key == "Output:Directory")?.Value ?? userDir;
            
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

            // Generate Images
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.GeneratingImages);
            
            if (string.IsNullOrEmpty(config.HuggingFaceKey))
            {
                throw new Exception("HuggingFace API key is missing. Please configure it in settings.");
            }
            
            _httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.HuggingFaceKey);
            
            var imageService = new StabilityImageService(_httpClient, NullLogger<StabilityImageService>.Instance);
            var renderSettings = VideoRenderSettings.FromFormatType(project.FormatType, 0, 0);
            renderSettings.FPS = config.VideoFps;

            // Simplified prompt generation for client side
            var basePrompt = $"{project.Title}, cinematic lighting, high quality";
            if (!string.IsNullOrWhiteSpace(project.ImageStyle)) basePrompt = $"{project.ImageStyle} style, {basePrompt}";

            var imagePaths = await imageService.GenerateImagesAsync(
                basePrompt, analysis.SceneCount, renderSettings.Width, renderSettings.Height,
                project.ImageModel, project.UniqueImageCount, cancellationToken);
                
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
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.Failed, ex.Message);
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
            
            var transitionTarget = timelineItems.FirstOrDefault()?.Transition ?? "none";

            var videoResult = await videoService.ComposeVideoAsync(
                imagePaths,
                project.AudioPath,
                renderSettings,
                transitionTarget,
                project.VisualEffect,
                null,
                config.OutputDir,
                config.IntroVideoPath,
                config.OutroVideoPath,
                async (progressPercent) => await _apiClient.UpdateProjectProgressAsync(projectId, progressPercent),
                cancellationToken);

            project.OutputVideoPath = videoResult.VideoFilePath;
            project.TimelineJson = videoResult.TimelineJson;
            project.Status = ProjectStatus.GeneratingSEO;
            await _apiClient.ClientUpdateAsync(projectId, project);

            // SEO Generation
            if (!string.IsNullOrEmpty(config.GrokKey))
            {
                var seoService = new GroqSeoService(_httpClient, NullLogger<GroqSeoService>.Instance, config.GrokKey);
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
            await _apiClient.UpdateProjectStatusAsync(projectId, ProjectStatus.Failed, ex.Message);
        }
    }
}
