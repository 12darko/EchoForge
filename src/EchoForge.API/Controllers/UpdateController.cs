using EchoForge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UpdateController : ControllerBase
{
    private readonly EchoForgeDbContext _context;
    private readonly ILogger<UpdateController> _logger;
    private readonly IWebHostEnvironment _env;

    public UpdateController(EchoForgeDbContext context, ILogger<UpdateController> logger, IWebHostEnvironment env)
    {
        _context = context;
        _logger = logger;
        _env = env;
    }

    /// <summary>
    /// Check for updates. Reads version info from AppSettings DB table.
    /// Keys: Update:LatestVersion, Update:ReleaseNotes, Update:DownloadUrl (optional external)
    /// </summary>
    [HttpGet("check")]
    public async Task<IActionResult> CheckForUpdate()
    {
        // Read version info from DB
        var settings = await _context.AppSettings
            .Where(s => s.Key.StartsWith("Update:"))
            .ToListAsync();

        var latestVersion = settings.FirstOrDefault(s => s.Key == "Update:LatestVersion")?.Value ?? "2.0.0";
        var releaseNotes = settings.FirstOrDefault(s => s.Key == "Update:ReleaseNotes")?.Value ?? "";
        var externalUrl = settings.FirstOrDefault(s => s.Key == "Update:DownloadUrl")?.Value ?? "";

        // Check if ZIP exists in wwwroot/updates/
        var updateFileName = $"EchoForge-v{latestVersion}.zip";
        var wwwroot = _env.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
        var updateFilePath = Path.Combine(wwwroot, "updates", updateFileName);

        long fileSizeBytes = 0;
        string downloadUrl = "";

        if (System.IO.File.Exists(updateFilePath))
        {
            fileSizeBytes = new FileInfo(updateFilePath).Length;
            var request = HttpContext.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}{request.PathBase}";
            downloadUrl = $"{baseUrl}/updates/{updateFileName}";
            _logger.LogInformation("Update file found locally: {Path} ({Size} bytes)", updateFilePath, fileSizeBytes);
        }
        else if (!string.IsNullOrEmpty(externalUrl))
        {
            // Use external CDN / Coolify hosted URL
            downloadUrl = externalUrl;
            _logger.LogInformation("Using external download URL: {Url}", externalUrl);
        }
        else
        {
            _logger.LogDebug("No update file found for v{Version}", latestVersion);
        }

        return Ok(new UpdateCheckResponse
        {
            Version = latestVersion,
            DownloadUrl = downloadUrl,
            ReleaseNotes = releaseNotes,
            FileSizeBytes = fileSizeBytes
        });
    }

    /// <summary>
    /// Set new version info (admin only, called during deploy)
    /// </summary>
    [HttpPost("publish")]
    public async Task<IActionResult> PublishVersion([FromBody] PublishVersionRequest request)
    {
        await UpsertSettingAsync("Update:LatestVersion", request.Version);
        await UpsertSettingAsync("Update:ReleaseNotes", request.ReleaseNotes ?? "");
        
        if (!string.IsNullOrEmpty(request.DownloadUrl))
            await UpsertSettingAsync("Update:DownloadUrl", request.DownloadUrl);

        _logger.LogInformation("Published update v{Version}", request.Version);
        return Ok(new { Message = $"Version {request.Version} published." });
    }

    private async Task UpsertSettingAsync(string key, string value)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting != null)
        {
            setting.Value = value;
        }
        else
        {
            _context.AppSettings.Add(new EchoForge.Core.Models.AppSetting { Key = key, Value = value });
        }
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Temporary endpoint to rescue the deleted Projects table.
    /// </summary>
    [HttpGet("fix-db")]
    public async Task<IActionResult> FixDb()
    {
        var sql = @"
        CREATE TABLE IF NOT EXISTS `Projects` (
          `Id` int NOT NULL AUTO_INCREMENT,
          `Title` varchar(200) NOT NULL,
          `AudioPath` varchar(500) NOT NULL,
          `TemplateId` int NOT NULL,
          `BPM` double DEFAULT NULL,
          `Duration` double DEFAULT NULL,
          `SceneCount` int DEFAULT NULL,
          `SceneDuration` double DEFAULT NULL,
          `FormatType` int NOT NULL DEFAULT '0',
          `ExtractAutoShorts` tinyint(1) NOT NULL DEFAULT '0',
          `ImageModel` varchar(50) NOT NULL DEFAULT 'flux',
          `UniqueImageCount` int NOT NULL DEFAULT '8',
          `ManualImageDurationSec` double DEFAULT NULL,
          `ImageStyle` varchar(200) NOT NULL DEFAULT '',
          `CustomWidth` int DEFAULT NULL,
          `CustomHeight` int DEFAULT NULL,
          `TransitionStyle` varchar(50) DEFAULT NULL,
          `VisualEffect` varchar(50) DEFAULT NULL,
          `Status` int NOT NULL DEFAULT '0',
          `PipelineProgress` int DEFAULT NULL,
          `PrivacyStatus` varchar(20) NOT NULL DEFAULT 'private',
          `OutputVideoPath` varchar(500) DEFAULT NULL,
          `YouTubeVideoId` varchar(50) DEFAULT NULL,
          `SeoTitle` varchar(200) DEFAULT NULL,
          `SeoDescription` text,
          `SeoTags` varchar(2000) DEFAULT NULL,
          `SeoHashtags` varchar(1000) DEFAULT NULL,
          `CustomInstructions` varchar(1000) DEFAULT NULL,
          `TargetPlatforms` varchar(200) DEFAULT NULL,
          `TargetChannelId` int DEFAULT NULL,
          `ErrorMessage` varchar(2000) DEFAULT NULL,
          `CreatedAt` datetime(6) NOT NULL,
          `UpdatedAt` datetime(6) DEFAULT NULL,
          `CompletedAt` datetime(6) DEFAULT NULL,
          `TimelineJson` text,
          PRIMARY KEY (`Id`),
          KEY `IX_Projects_TemplateId` (`TemplateId`),
          KEY `IX_Projects_TargetChannelId` (`TargetChannelId`),
          CONSTRAINT `FK_Projects_Templates` FOREIGN KEY (`TemplateId`) REFERENCES `Templates` (`Id`) ON DELETE CASCADE,
          CONSTRAINT `FK_Projects_YouTubeChannels` FOREIGN KEY (`TargetChannelId`) REFERENCES `YouTubeChannels` (`Id`) ON DELETE SET NULL
        ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_0900_ai_ci;";

        try 
        {
            await _context.Database.ExecuteSqlRawAsync(sql);
            return Ok("Projects table has been successfully recreated with all modern columns! You can now load the Dashboard. \n(Note: Old project data is gone because the table was manually dropped earlier.)");
        }
        catch (Exception ex)
        {
            return StatusCode(500, "Error recreating table: " + ex.Message);
        }
    }
}

public class UpdateCheckResponse
{
    public string Version { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ReleaseNotes { get; set; } = "";
    public long FileSizeBytes { get; set; }
}

public class PublishVersionRequest
{
    public string Version { get; set; } = "";
    public string? ReleaseNotes { get; set; }
    public string? DownloadUrl { get; set; }
}
