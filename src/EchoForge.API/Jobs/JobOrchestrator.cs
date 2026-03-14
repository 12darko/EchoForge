using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using EchoForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.API.Jobs;

public class JobOrchestrator
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITemplateService _templateService;
    private readonly IAudioService _audioService;
    private readonly IImageGenerationService _imageService;
    private readonly IVideoComposerService _videoService;
    private readonly ISeoService _seoService;
    private readonly IYouTubeUploadService _uploadService;
    private readonly EchoForgeDbContext _context;
    private readonly ILogger<JobOrchestrator> _logger;

    public JobOrchestrator(
        IProjectRepository projectRepository,
        ITemplateService templateService,
        IAudioService audioService,
        IImageGenerationService imageService,
        IVideoComposerService videoService,
        ISeoService seoService,
        IYouTubeUploadService uploadService,
        EchoForgeDbContext context,
        ILogger<JobOrchestrator> logger)
    {
        _projectRepository = projectRepository;
        _templateService = templateService;
        _audioService = audioService;
        _imageService = imageService;
        _videoService = videoService;
        _seoService = seoService;
        _uploadService = uploadService;
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Main pipeline: Audio Analysis → Generate Images → Compose Video → Generate SEO → Await Approval
    /// </summary>
    public async Task StartPipelineAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null)
        {
            _logger.LogError("Project {Id} not found", projectId);
            return;
        }

        try
        {
            // Step 1: Audio Analysis
            _logger.LogInformation("[{ProjectId}] Step 1/5: Audio Analysis", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.Analyzing);

            // Fetch settings
            var ffmpegSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "FFmpeg:Path");
            var ffmpegPath = ffmpegSetting?.Value;

            if (project.ExtractAutoShorts)
            {
                _logger.LogInformation("[{ProjectId}] Auto-Shorts enabled. Trimming best 60s highlight...", projectId);
                var croppedPath = await _audioService.ExtractBestPartAsync(project.AudioPath, 60, ffmpegPath, cancellationToken);
                project.AudioPath = croppedPath;
                await _projectRepository.UpdateAsync(project);
            }

            var analysis = await _audioService.AnalyzeAsync(project.AudioPath, ffmpegPath, cancellationToken);
            
            // Override with manual duration if provided
            if (project.ManualImageDurationSec.HasValue && project.ManualImageDurationSec.Value > 0)
            {
                var manualSec = project.ManualImageDurationSec.Value;
                var totalDuration = analysis.Duration;
                
                analysis.SceneDuration = manualSec;
                var calculatedScenes = Math.Max(1, (int)Math.Ceiling(totalDuration / manualSec));
                
                // Allow up to 100 scenes if user manually sets a very short duration
                analysis.SceneCount = Math.Min(calculatedScenes, 100); 
                _logger.LogInformation("Using manual image duration {Sec}s -> {Count} scenes", manualSec, analysis.SceneCount);
            }

            project.BPM = analysis.BPM;
            project.Duration = analysis.Duration;
            project.SceneCount = analysis.SceneCount;
            project.SceneDuration = analysis.SceneDuration;
            await _projectRepository.UpdateAsync(project);

            // Step 2: Generate Images
            _logger.LogInformation("[{ProjectId}] Step 2/5: Generating {Count} images", projectId, analysis.SceneCount);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.GeneratingImages);

            var template = await _templateService.GetByIdAsync(project.TemplateId);
            var renderSettings = VideoRenderSettings.FromFormatType(project.FormatType, project.CustomWidth, project.CustomHeight);

            // Apply FPS setting
            var fpsSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Video:Fps");
            if (fpsSetting != null && int.TryParse(fpsSetting.Value, out int fps))
            {
                renderSettings.FPS = fps;
            }

            // Build prompt with user's title and image style
            var basePrompt = template?.ImagePromptBase ?? "cinematic landscape, high quality";
            if (!string.IsNullOrWhiteSpace(project.Title))
            {
                basePrompt = $"{project.Title}, {basePrompt}";
            }

            if (!string.IsNullOrWhiteSpace(project.ImageStyle))
            {
                basePrompt = $"{project.ImageStyle} style, {basePrompt}";
            }

            var imagePaths = await _imageService.GenerateImagesAsync(
                basePrompt,
                analysis.SceneCount,
                renderSettings.Width,
                renderSettings.Height,
                project.ImageModel,
                project.UniqueImageCount,
                cancellationToken);

            // Create initial TimelineJson
            var sceneDuration = Math.Min(analysis.Duration, renderSettings.MaxDurationSeconds) / imagePaths.Count;
            var transitionStyle = !string.IsNullOrEmpty(project.TransitionStyle) ? project.TransitionStyle : (template?.Transition ?? "none");
            
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
            project.TimelineJson = System.Text.Json.JsonSerializer.Serialize(timelineItems);

            // Phase 7: Pause Pipeline for Review
            _logger.LogInformation("[{ProjectId}] Video scenes generated. Pausing pipeline for user review.", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.ReviewingScenes);
            await _projectRepository.UpdateAsync(project);
            
            return; // Pause here, resume via ResumePipelineAsync
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ProjectId}] Pipeline failed at step 1-2", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Resumes the pipeline (Steps 3-5) after user reviews and edits scenes.
    /// </summary>
    public async Task ResumePipelineAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null || string.IsNullOrEmpty(project.TimelineJson))
        {
            _logger.LogError("Project {Id} not found or missing timeline", projectId);
            return;
        }

        try
        {
            var timelineItems = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(project.TimelineJson) ?? new List<TimelineItemDto>();
            var imagePaths = timelineItems.Select(t => t.ImagePath).ToList();
            var template = await _templateService.GetByIdAsync(project.TemplateId);
            var renderSettings = VideoRenderSettings.FromFormatType(project.FormatType, project.CustomWidth, project.CustomHeight);

            // Step 3: Compose Video
            _logger.LogInformation("[{ProjectId}] Step 3/5: Composing video", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.ComposingVideo);

            // Fetch output directory setting
            var outputDirSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Output:Directory");
            var outputDir = outputDirSetting?.Value;

            var introSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Branding:IntroVideoPath");
            var introVideoPath = introSetting?.Value;

            var outroSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Branding:OutroVideoPath");
            var outroVideoPath = outroSetting?.Value;

            // We construct transition from the first item, though VideoComposerService currently only supports one transition per run.
            var transitionTarget = timelineItems.FirstOrDefault()?.Transition ?? "none";

            var videoResult = await _videoService.ComposeVideoAsync(
                imagePaths,
                project.AudioPath,
                renderSettings,
                transitionTarget,
                project.VisualEffect,
                null, 
                outputDir,
                introVideoPath,
                outroVideoPath,
                async (progressPercent) => 
                {
                    await _projectRepository.UpdateProgressAsync(projectId, progressPercent, cancellationToken);
                },
                cancellationToken);

            project.OutputVideoPath = videoResult.VideoFilePath;
            project.TimelineJson = videoResult.TimelineJson;
            await _projectRepository.UpdateAsync(project);

            // Step 4: Generate SEO
            _logger.LogInformation("[{ProjectId}] Step 4/5: Generating SEO metadata", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.GeneratingSEO);

            var langSetting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == "Seo:Language");
            var language = langSetting?.Value ?? "English";
            
            var seo = await _seoService.GenerateSeoAsync(
                project.Title,
                template?.Name ?? "Music",
                template?.ColorTheme ?? "general",
                language,
                project.CustomInstructions,
                project.TargetPlatforms,
                cancellationToken);

            project.SeoTitle = seo.Title;
            project.SeoDescription = seo.Description;
            project.SeoTags = string.Join(",", seo.Tags);
            project.SeoHashtags = string.Join(" ", seo.Hashtags);
            await _projectRepository.UpdateAsync(project);

            // Step 5: Await user approval
            _logger.LogInformation("[{ProjectId}] Step 5/5: Awaiting user approval", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.AwaitingApproval);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ProjectId}] Pipeline failed at step 3-5", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.Failed, ex.Message);
        }
    }

    /// <summary>
    /// Upload step: triggered after user approval
    /// </summary>
    public async Task UploadAsync(int projectId, CancellationToken cancellationToken)
    {
        var project = await _projectRepository.GetByIdAsync(projectId);
        if (project == null) return;

        try
        {
            _logger.LogInformation("[{ProjectId}] Uploading to YouTube", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.Uploading);

            var uploadRequest = new YouTubeUploadRequest
            {
                VideoFilePath = project.OutputVideoPath!,
                Title = project.SeoTitle ?? project.Title,
                Description = project.SeoDescription ?? "",
                Tags = (project.SeoTags ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries).ToList(),
                CategoryId = "10", // Music
                PrivacyStatus = project.PrivacyStatus ?? "private"
            };

            var result = await _uploadService.UploadVideoAsync(uploadRequest, cancellationToken);

            if (result.Success)
            {
                project.YouTubeVideoId = result.VideoId;
                await _projectRepository.UpdateAsync(project);
                await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.Completed);
                _logger.LogInformation("[{ProjectId}] Upload complete! YouTube ID: {VideoId}", projectId, result.VideoId);
            }
            else
            {
                await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.Failed, result.ErrorMessage);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{ProjectId}] Upload failed", projectId);
            await _projectRepository.UpdateStatusAsync(projectId, ProjectStatus.Failed, ex.Message);
        }
    }
}
