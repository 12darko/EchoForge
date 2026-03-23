using EchoForge.Core.Entities;
using EchoForge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YouTubeChannelsController : ControllerBase
{
    private readonly EchoForgeDbContext _dbContext;

    public YouTubeChannelsController(EchoForgeDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    [HttpGet]
    public async Task<ActionResult<List<YouTubeChannel>>> GetChannels([FromQuery] int userId)
    {
        var channels = await _dbContext.YouTubeChannels
            .Where(c => c.UserId == userId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
        return Ok(channels);
    }

    [HttpDelete("{id}")]
    public async Task<ActionResult> DeleteChannel(int id)
    {
        var channel = await _dbContext.YouTubeChannels.FindAsync(id);
        if (channel == null) return NotFound();

        _dbContext.YouTubeChannels.Remove(channel);
        await _dbContext.SaveChangesAsync();

        return Ok();
    }

    [HttpPost("connect")]
    public async Task<ActionResult> Connect([FromQuery] int userId, [FromServices] EchoForge.Core.Interfaces.IYouTubeUploadService ytService)
    {
        try
        {
            var channel = await ytService.ConnectAsync(userId);
            return Ok(channel);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpPost("save-token")]
    public async Task<ActionResult> SaveToken([FromQuery] int userId, [FromBody] System.Text.Json.JsonElement tokenJsonElement, [FromServices] EchoForge.Core.Interfaces.IYouTubeUploadService ytService)
    {
        try
        {
            var tokenStr = tokenJsonElement.GetRawText();
            if (string.IsNullOrWhiteSpace(tokenStr)) return BadRequest("Invalid token");

            var channel = await ytService.SaveTokenAndConnectAsync(userId, tokenStr);
            return Ok(channel);
        }
        catch (Exception ex)
        {
            return StatusCode(500, ex.Message);
        }
    }

    [HttpGet("{channelId}/videos")]
    public async Task<ActionResult<List<EchoForge.Core.DTOs.YouTubeVideoDto>>> GetVideos(string channelId, [FromServices] EchoForge.Core.Interfaces.IYouTubeUploadService ytService)
    {
        var videos = await ytService.GetChannelVideosAsync(channelId);
        return Ok(videos);
    }
}
