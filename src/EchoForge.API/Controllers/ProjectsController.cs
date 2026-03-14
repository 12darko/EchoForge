using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using Hangfire;
using Microsoft.AspNetCore.Mvc;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProjectsController : ControllerBase
{
    private readonly IProjectRepository _projectRepository;
    private readonly ITemplateService _templateService;
    private readonly IBackgroundJobClient _jobClient;
    private readonly ILogger<ProjectsController> _logger;

    public ProjectsController(
        IProjectRepository projectRepository,
        ITemplateService templateService,
        IBackgroundJobClient jobClient,
        ILogger<ProjectsController> logger)
    {
        _projectRepository = projectRepository;
        _templateService = templateService;
        _jobClient = jobClient;
        _logger = logger;
    }

    [HttpGet]
    public async Task<ActionResult<List<ProjectDto>>> GetAll()
    {
        var projects = await _projectRepository.GetAllAsync();
        return Ok(projects.Select(MapToDto).ToList());
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<ProjectDto>> GetById(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();
        return Ok(MapToDto(project));
    }

    [HttpPost]
    public async Task<ActionResult<ProjectDto>> Create([FromForm] CreateProjectRequest request, IFormFile audioFile)
    {
        // Validate template
        var template = await _templateService.GetByIdAsync(request.TemplateId);
        if (template == null) return BadRequest("Invalid template ID");

        // Save audio file
        var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "audio");
        Directory.CreateDirectory(uploadsDir);
        var audioFileName = $"{Guid.NewGuid()}{Path.GetExtension(audioFile.FileName)}";
        var audioPath = Path.Combine(uploadsDir, audioFileName);

        using (var stream = new FileStream(audioPath, FileMode.Create))
        {
            await audioFile.CopyToAsync(stream);
        }

        // Create project
        var project = new Project
        {
            Title = request.Title,
            AudioPath = audioPath,
            TemplateId = request.TemplateId,
            FormatType = request.FormatType,
            ExtractAutoShorts = request.ExtractAutoShorts,
            CustomWidth = request.CustomWidth,
            CustomHeight = request.CustomHeight,
            ImageModel = request.ImageModel,
            UniqueImageCount = Math.Clamp(request.UniqueImageCount, 1, 20),
            ManualImageDurationSec = request.ManualImageDurationSec,
            TransitionStyle = request.TransitionStyle,
            VisualEffect = request.VisualEffect,
            ImageStyle = request.ImageStyle ?? string.Empty,
            CustomInstructions = request.CustomInstructions,
            TargetPlatforms = request.TargetPlatforms,
            TargetChannelId = request.TargetChannelId,
            Status = ProjectStatus.Created,
            PrivacyStatus = request.PrivacyStatus
        };

        await _projectRepository.CreateAsync(project);

        // Queue the job pipeline
        _jobClient.Enqueue<Jobs.JobOrchestrator>(x => x.StartPipelineAsync(project.Id, CancellationToken.None));

        _logger.LogInformation("Project created: {Id} - {Title}", project.Id, project.Title);
        return CreatedAtAction(nameof(GetById), new { id = project.Id }, MapToDto(project));
    }
    
    [HttpPost("{id}/approve")]
    public async Task<ActionResult> Approve(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        if (project.Status != ProjectStatus.AwaitingApproval)
            return BadRequest("Project is not awaiting approval");

        // Queue upload job
        _jobClient.Enqueue<Jobs.JobOrchestrator>(x => x.UploadAsync(id, CancellationToken.None));
        
        return Ok();
    }

    [HttpPost("{id}/retry")]
    public async Task<ActionResult> Retry(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        if (project.Status != ProjectStatus.Failed && 
            project.Status != ProjectStatus.ComposingVideo &&
            project.Status != ProjectStatus.GeneratingImages &&
            project.Status != ProjectStatus.GeneratingSEO)
        {
            return BadRequest("Project cannot be retried in its current state");
        }

        project.Status = ProjectStatus.Created;
        project.ErrorMessage = null;
        project.UpdatedAt = DateTime.UtcNow;
        await _projectRepository.UpdateAsync(project);

        _jobClient.Enqueue<Jobs.JobOrchestrator>(x => x.StartPipelineAsync(id, CancellationToken.None));
        _logger.LogInformation("Project {Id} retried — pipeline re-queued", id);
        return Ok();
    }

    [HttpPost("{id}/reject")]
    public async Task<ActionResult> Reject(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        await _projectRepository.UpdateStatusAsync(id, ProjectStatus.Failed, "Rejected by user");
        return Ok();
    }

    [HttpPost("{id}/render")]
    public async Task<ActionResult> RenderVideo(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        if (project.Status != ProjectStatus.ReviewingScenes)
            return BadRequest("Project is not in ReviewingScenes status.");

        project.Status = ProjectStatus.ComposingVideo;
        await _projectRepository.UpdateAsync(project);

        _jobClient.Enqueue<Jobs.JobOrchestrator>(x => x.ResumePipelineAsync(id, CancellationToken.None));
        _logger.LogInformation("Project {Id} review approved — resuming pipeline for render", id);
        return Ok();
    }

    [HttpPut("{id}/scenes")]
    public async Task<ActionResult> UpdateScenes(int id, [FromBody] List<TimelineItemDto> updatedScenes)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        if (project.Status != ProjectStatus.ReviewingScenes)
            return BadRequest("Scenes can only be updated while in ReviewingScenes status.");

        project.TimelineJson = System.Text.Json.JsonSerializer.Serialize(updatedScenes);
        project.UpdatedAt = DateTime.UtcNow;
        await _projectRepository.UpdateAsync(project);

        return Ok(MapToDto(project));
    }

    public class RegenerateSceneRequest
    {
        public int SceneNumber { get; set; }
        public string Prompt { get; set; } = string.Empty;
    }

    [HttpPost("{id}/scenes/regenerate")]
    public async Task<ActionResult> RegenerateScene(int id, [FromBody] RegenerateSceneRequest request)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        if (project.Status != ProjectStatus.ReviewingScenes)
            return BadRequest("Can only regenerate scenes in ReviewingScenes status.");

        if (string.IsNullOrEmpty(project.TimelineJson))
            return BadRequest("Project timeline is missing.");

        var scenes = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(project.TimelineJson);
        var targetScene = scenes?.FirstOrDefault(s => s.SceneNumber == request.SceneNumber);

        if (targetScene == null)
            return NotFound($"Scene {request.SceneNumber} not found.");

        try
        {
            var imageService = HttpContext.RequestServices.GetRequiredService<IImageGenerationService>();
            var renderSettings = VideoRenderSettings.FromFormatType(project.FormatType, project.CustomWidth, project.CustomHeight);

            var newImagePaths = await imageService.GenerateImagesAsync(
                request.Prompt,
                1, 
                renderSettings.Width,
                renderSettings.Height,
                project.ImageModel,
                project.UniqueImageCount,
                CancellationToken.None);

            if (newImagePaths != null && newImagePaths.Count > 0)
            {
                targetScene.ImagePath = newImagePaths[0];
                targetScene.Prompt = request.Prompt; 
                project.TimelineJson = System.Text.Json.JsonSerializer.Serialize(scenes);
                project.UpdatedAt = DateTime.UtcNow;
                await _projectRepository.UpdateAsync(project);

                return Ok(MapToDto(project));
            }
            return StatusCode(500, "Image generation returned empty.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to regenerate scene {SceneNum} for project {ProjectId}", request.SceneNumber, id);
            return StatusCode(500, "Failed to regenerate scene: " + ex.Message);
        }
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        await _projectRepository.DeleteAsync(id);
        return NoContent();
    }

    private static ProjectDto MapToDto(Project p) => new()
    {
        Id = p.Id,
        Title = p.Title,
        AudioPath = p.AudioPath,
        TemplateId = p.TemplateId,
        TemplateName = p.Template?.Name,
        BPM = p.BPM,
        Duration = p.Duration,
        SceneCount = p.SceneCount,
        SceneDuration = p.SceneDuration,
        FormatType = p.FormatType,
        ExtractAutoShorts = p.ExtractAutoShorts,
        ImageModel = p.ImageModel,
        UniqueImageCount = p.UniqueImageCount,
        ImageStyle = p.ImageStyle,
        TransitionStyle = p.TransitionStyle,
        VisualEffect = p.VisualEffect,
        ManualImageDurationSec = p.ManualImageDurationSec,
        CustomInstructions = p.CustomInstructions,
        TargetPlatforms = p.TargetPlatforms,
        Status = p.Status,
        PipelineProgress = p.PipelineProgress,
        PrivacyStatus = p.PrivacyStatus,
        OutputVideoPath = p.OutputVideoPath,
        YouTubeVideoId = p.YouTubeVideoId,
        SeoTitle = p.SeoTitle,
        SeoDescription = p.SeoDescription,
        SeoTags = p.SeoTags,
        SeoHashtags = p.SeoHashtags,
        TargetChannelId = p.TargetChannelId,
        TimelineJson = p.TimelineJson,
        ErrorMessage = p.ErrorMessage,
        CreatedAt = p.CreatedAt,
        CompletedAt = p.CompletedAt
    };
}
