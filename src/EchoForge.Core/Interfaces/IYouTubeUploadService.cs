using EchoForge.Core.DTOs;

namespace EchoForge.Core.Interfaces;

public interface IYouTubeUploadService
{
    Task LoadCredentialsAsync();
    Task<YouTubeUploadResult> UploadVideoAsync(YouTubeUploadRequest request, CancellationToken cancellationToken = default);
    Task<bool> IsAuthenticatedAsync(string channelId, CancellationToken cancellationToken = default);
    Task<EchoForge.Core.Entities.YouTubeChannel> ConnectAsync(int userId, CancellationToken cancellationToken = default);
    Task<EchoForge.Core.Entities.YouTubeChannel> SaveTokenAndConnectAsync(int userId, string tokenJson, CancellationToken cancellationToken = default);
    Task<string> GetChannelNameAsync(string channelId, CancellationToken cancellationToken = default);
    Task<List<YouTubeVideoDto>> GetChannelVideosAsync(string channelId, int maxResults = 50, CancellationToken cancellationToken = default);
}
