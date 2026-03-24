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
    public async Task<ActionResult<List<ProjectDto>>> GetAll([FromQuery] int? userId = null)
    {
        var projects = await _projectRepository.GetAllAsync();
        if (userId.HasValue && userId.Value > 0)
            projects = projects.Where(p => p.UserId == userId.Value).ToList();
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
    public async Task<ActionResult<ProjectDto>> Create([FromBody] CreateProjectRequest request)
    {
        // Validate template
        var template = await _templateService.GetByIdAsync(request.TemplateId);
        if (template == null) return BadRequest("Invalid template ID");

        // Create project — audio file stays on the client machine
        var project = new Project
        {
            Title = request.Title,
            AudioPath = request.AudioPath ?? string.Empty,
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
            PrivacyStatus = request.PrivacyStatus,
            UserId = request.UserId
        };

        await _projectRepository.CreateAsync(project);

        // Pipeline handled by client
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
        project.Status = ProjectStatus.Uploading;
        await _projectRepository.UpdateAsync(project);
        
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

        // Retry handled by client
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

        _logger.LogInformation("Project {Id} review approved — pipeline will be resumed by client", id);
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

    [HttpPut("{id}/client-update")]
    public async Task<ActionResult> ClientUpdate(int id, [FromBody] ProjectDto updateDto)
    {
        var project = await _projectRepository.GetByIdAsync(id);
        if (project == null) return NotFound();

        project.Status = updateDto.Status;
        project.PipelineProgress = updateDto.PipelineProgress;
        
        if (!string.IsNullOrEmpty(updateDto.ErrorMessage)) project.ErrorMessage = updateDto.ErrorMessage;
        if (!string.IsNullOrEmpty(updateDto.AudioPath)) project.AudioPath = updateDto.AudioPath;
        if (!string.IsNullOrEmpty(updateDto.OutputVideoPath)) project.OutputVideoPath = updateDto.OutputVideoPath;
        if (!string.IsNullOrEmpty(updateDto.TimelineJson)) project.TimelineJson = updateDto.TimelineJson;
        if (updateDto.BPM > 0) project.BPM = updateDto.BPM;
        if (updateDto.Duration > 0) project.Duration = updateDto.Duration;
        if (updateDto.SceneCount > 0) project.SceneCount = updateDto.SceneCount;
        if (updateDto.SceneDuration > 0) project.SceneDuration = updateDto.SceneDuration;
        
        if (!string.IsNullOrEmpty(updateDto.SeoTitle)) project.SeoTitle = updateDto.SeoTitle;
        if (!string.IsNullOrEmpty(updateDto.SeoDescription)) project.SeoDescription = updateDto.SeoDescription;
        if (!string.IsNullOrEmpty(updateDto.SeoTags)) project.SeoTags = updateDto.SeoTags;
        if (!string.IsNullOrEmpty(updateDto.SeoHashtags)) project.SeoHashtags = updateDto.SeoHashtags;
        if (!string.IsNullOrEmpty(updateDto.YouTubeVideoId)) project.YouTubeVideoId = updateDto.YouTubeVideoId;

        project.UpdatedAt = DateTime.UtcNow;
        if (updateDto.Status == ProjectStatus.Completed && project.CompletedAt == null)
            project.CompletedAt = DateTime.UtcNow;

        await _projectRepository.UpdateAsync(project);
        return Ok(MapToDto(project));
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> Delete(int id)
    {
        try
        {
            var project = await _projectRepository.GetByIdAsync(id);
            if (project == null) return NotFound();

            await _projectRepository.DeleteAsync(id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting project {Id}", id);
            return StatusCode(500, ex.InnerException?.Message ?? ex.Message);
        }
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
