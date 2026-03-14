using EchoForge.Core.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class YouTubeController : ControllerBase
{
    private readonly IYouTubeUploadService _youTubeService;
    private readonly ILogger<YouTubeController> _logger;

    public YouTubeController(IYouTubeUploadService youTubeService, ILogger<YouTubeController> logger)
    {
        _youTubeService = youTubeService;
        _logger = logger;
    }

    [HttpPost("connect")]
    public async Task<IActionResult> Connect([FromQuery] int userId)
    {
        try
        {
            // This will open the system browser on the machine running the API
            var channel = await _youTubeService.ConnectAsync(userId);
            return Ok(new { Message = "Authentication completed successfully", Channel = channel });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication failed");
            return BadRequest(new { Error = ex.Message });
        }
    }

    [HttpGet("channel")]
    public async Task<IActionResult> GetChannelName([FromQuery] string channelId)
    {
        if (string.IsNullOrEmpty(channelId)) return BadRequest("channelId is required");
        var name = await _youTubeService.GetChannelNameAsync(channelId);
        return Ok(new { Name = name });
    }


}
