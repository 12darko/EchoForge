using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using EchoForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Auth.OAuth2.Flows;
using Google.Apis.Auth.OAuth2.Responses;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.YouTube.v3;
using Google.Apis.YouTube.v3.Data;
using Google.Apis.Util.Store;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.YouTube;

public class YouTubeUploadService : IYouTubeUploadService
{
    private readonly ILogger<YouTubeUploadService> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IEncryptionService _encryptionService;
    private readonly EchoForgeDbContext _context;
    private string? _clientId;
    private string? _clientSecret;

    public YouTubeUploadService(
        ILogger<YouTubeUploadService> logger, 
        ILoggerFactory loggerFactory,
        IEncryptionService encryptionService,
        EchoForgeDbContext context)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _encryptionService = encryptionService;
        _context = context;
    }

    public async Task LoadCredentialsAsync()
    {
        var settings = await _context.AppSettings.ToListAsync();
        
        var clientIdEncrypted = settings.FirstOrDefault(s => s.Key == "YouTube:ClientId");
        if (clientIdEncrypted != null) 
            _clientId = _encryptionService.Decrypt(clientIdEncrypted.Value);

        var clientSecretEncrypted = settings.FirstOrDefault(s => s.Key == "YouTube:ClientSecret");
        if (clientSecretEncrypted != null) 
            _clientSecret = _encryptionService.Decrypt(clientSecretEncrypted.Value);


    }

    public void Configure(string clientId, string clientSecret)
    {
        _clientId = clientId;
        _clientSecret = clientSecret;
    }

    public async Task<bool> IsAuthenticatedAsync(string channelId, CancellationToken cancellationToken = default)
    {
        return await _context.YouTubeChannels.AnyAsync(c => c.ChannelId == channelId && c.AccessToken != null, cancellationToken);
    }

    public async Task<EchoForge.Core.Entities.YouTubeChannel> ConnectAsync(int userId, CancellationToken cancellationToken = default)
    {
        await LoadCredentialsAsync();

        if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_clientSecret))
            throw new InvalidOperationException("YouTube API Client ID or Secret not found in settings.");

        var secrets = new ClientSecrets
        {
            ClientId = _clientId,
            ClientSecret = _clientSecret
        };

        // 1. Authorize using a temporary local FileDataStore first so we can find out who the user is
        var tempFolder = Path.Combine(Path.GetTempPath(), "EchoForge_OAuth_" + Guid.NewGuid().ToString());
        var tempStore = new FileDataStore(tempFolder, true);
        
        var credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets,
            new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.Youtube },
            "temp_user",
            cancellationToken,
            tempStore
        );
        
        _logger.LogInformation("YouTube authentication completed. Fetching channel info...");

        // 2. Use the credential to fetch the user's Channel ID and Name
        using var youtubeService = new YouTubeService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "EchoForge"
        });

        var request = youtubeService.Channels.List("snippet");
        request.Mine = true;
        var response = await request.ExecuteAsync(cancellationToken);

        var channel = response.Items.FirstOrDefault();
        if (channel == null)
        {
            throw new Exception("No YouTube channel found for the authenticated Google account.");
        }

        var channelId = channel.Id;
        var channelName = channel.Snippet.Title;

        // 3. Now that we know the Channel ID, save it to our database DataStore
        var dbDataStore = new YouTubeChannelDataStore(
            _context, 
            _encryptionService, 
            _loggerFactory.CreateLogger<YouTubeChannelDataStore>(),
            channelId);

        // Read the token from temp store and write to DB store
        var token = await tempStore.GetAsync<Google.Apis.Auth.OAuth2.Responses.TokenResponse>("temp_user");
        await dbDataStore.StoreAsync("user", token); // The key "user" is what GetCredentialAsync expects

        // Clean up temp store
        try { Directory.Delete(tempFolder, true); } catch { /* ignore */ }

        // 4. Update the ChannelName in the DB (YouTubeChannelDataStore creates the row)
        var dbChannel = await _context.YouTubeChannels.FirstOrDefaultAsync(c => c.ChannelId == channelId, cancellationToken);
        if (dbChannel != null)
        {
            dbChannel.ChannelName = channelName;
            await _context.SaveChangesAsync(cancellationToken);
        }

        _logger.LogInformation("Successfully connected and saved channel: {ChannelName} ({ChannelId}) for UserId: {UserId}", channelName, channelId, userId);

        if (dbChannel == null)
        {
            dbChannel = new EchoForge.Core.Entities.YouTubeChannel { ChannelId = channelId, ChannelName = channelName, UserId = userId };
        }
        else
        {
            dbChannel.UserId = userId; // update if it already existed without user
            await _context.SaveChangesAsync(cancellationToken);
        }

        return dbChannel;
    }

    private async Task<UserCredential?> GetCredentialAsync(string channelId, CancellationToken cancellationToken)
    {
        await LoadCredentialsAsync();
        if (string.IsNullOrEmpty(_clientId)) return null;

        var dataStore = new YouTubeChannelDataStore(
            _context, 
            _encryptionService, 
            _loggerFactory.CreateLogger<YouTubeChannelDataStore>(),
            channelId);
        
        return await GoogleWebAuthorizationBroker.AuthorizeAsync(
            new ClientSecrets { ClientId = _clientId, ClientSecret = _clientSecret },
            new[] { YouTubeService.Scope.YoutubeUpload, YouTubeService.Scope.Youtube },
            "user",
            cancellationToken,
            dataStore
        );
    }

    public async Task<string> GetChannelNameAsync(string channelId, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthenticatedAsync(channelId, cancellationToken)) return "Not Connected";

        try
        {
            var credential = await GetCredentialAsync(channelId, cancellationToken);
            if (credential == null) return "Not Connected";

            using var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "EchoForge"
            });

            var request = youtubeService.Channels.List("snippet");
            request.Mine = true;
            var response = await request.ExecuteAsync(cancellationToken);

            var channel = response.Items.FirstOrDefault();
            return channel?.Snippet.Title ?? "Unknown Channel";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get channel info");
            return "Connection Error";
        }
    }

    public async Task<List<YouTubeVideoDto>> GetChannelVideosAsync(string channelId, int maxResults = 50, CancellationToken cancellationToken = default)
    {
        if (!await IsAuthenticatedAsync(channelId, cancellationToken)) return new List<YouTubeVideoDto>();

        try
        {
            var credential = await GetCredentialAsync(channelId, cancellationToken);
            if (credential == null) return new List<YouTubeVideoDto>();

            using var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "EchoForge"
            });

            // First, get the 'uploads' playlist id of the channel
            var channelRequest = youtubeService.Channels.List("contentDetails");
            channelRequest.Mine = true;
            var channelResponse = await channelRequest.ExecuteAsync(cancellationToken);
            var channel = channelResponse.Items.FirstOrDefault();
            var uploadsListId = channel?.ContentDetails.RelatedPlaylists.Uploads;

            if (string.IsNullOrEmpty(uploadsListId)) return new List<YouTubeVideoDto>();

            // Second, fetch videos from the 'uploads' playlist
            var playlistRequest = youtubeService.PlaylistItems.List("snippet");
            playlistRequest.PlaylistId = uploadsListId;
            playlistRequest.MaxResults = maxResults;
            var playlistResponse = await playlistRequest.ExecuteAsync(cancellationToken);
            
            var videoIds = playlistResponse.Items.Select(x => x.Snippet.ResourceId.VideoId).ToList();
            if (!videoIds.Any()) return new List<YouTubeVideoDto>();

            // Third, fetch video statistics (views, likes, privacy status)
            var videoRequest = youtubeService.Videos.List("snippet,statistics,status");
            videoRequest.Id = string.Join(",", videoIds);
            var videoResponse = await videoRequest.ExecuteAsync(cancellationToken);

            var videos = new List<YouTubeVideoDto>();
            foreach (var v in videoResponse.Items)
            {
                videos.Add(new YouTubeVideoDto
                {
                    VideoId = v.Id,
                    Title = v.Snippet.Title,
                    Description = v.Snippet.Description,
                    ThumbnailUrl = v.Snippet.Thumbnails?.High?.Url ?? v.Snippet.Thumbnails?.Default__?.Url ?? "",
                    PublishedAt = v.Snippet.PublishedAtDateTimeOffset?.DateTime,
                    ViewCount = v.Statistics.ViewCount,
                    LikeCount = v.Statistics.LikeCount,
                    CommentCount = v.Statistics.CommentCount,
                    PrivacyStatus = v.Status.PrivacyStatus
                });
            }

            return videos;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get videos for channel {ChannelId}", channelId);
            return new List<YouTubeVideoDto>();
        }
    }

    public async Task<YouTubeUploadResult> UploadVideoAsync(YouTubeUploadRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation("Starting YouTube upload: {Title}", request.Title);

            // Fetch Project to find the TargetChannelId
            var project = await _context.Projects.FindAsync(new object[] { request.ProjectId }, cancellationToken);
            if (project?.TargetChannelId == null)
            {
                return new YouTubeUploadResult { Success = false, ErrorMessage = "No target channel selected." };
            }

            var channel = await _context.YouTubeChannels.FindAsync(new object[] { project.TargetChannelId }, cancellationToken);
            if (channel == null)
            {
                return new YouTubeUploadResult { Success = false, ErrorMessage = "Selected channel not found." };
            }

            var credential = await GetCredentialAsync(channel.ChannelId, cancellationToken);
            if (credential == null)
            {
                 return new YouTubeUploadResult
                 {
                     Success = false,
                     ErrorMessage = "Not authenticated with YouTube. Please configure OAuth credentials."
                 };
            }

            using var youtubeService = new YouTubeService(new BaseClientService.Initializer
            {
                HttpClientInitializer = credential,
                ApplicationName = "EchoForge"
            });

            var video = new Google.Apis.YouTube.v3.Data.Video
            {
                Snippet = new VideoSnippet
                {
                    Title = request.Title,
                    Description = request.Description,
                    Tags = request.Tags,
                    CategoryId = request.CategoryId
                },
                Status = new VideoStatus
                {
                    PrivacyStatus = request.PrivacyStatus,
                    PublishAtDateTimeOffset = request.ScheduledPublishAt.HasValue ? new DateTimeOffset(request.ScheduledPublishAt.Value) : null
                }
            };

            using var fileStream = new FileStream(request.VideoFilePath, FileMode.Open);

            var videosInsertRequest = youtubeService.Videos.Insert(video, "snippet,status", fileStream, "video/*");
            videosInsertRequest.ProgressChanged += progress =>
            {
                _logger.LogDebug("Upload progress: {Status} - {Bytes} bytes", progress.Status, progress.BytesSent);
            };

            var uploadResponse = await videosInsertRequest.UploadAsync(cancellationToken);

            if (uploadResponse.Status == UploadStatus.Completed)
            {
                var videoId = videosInsertRequest.ResponseBody?.Id;
                _logger.LogInformation("Upload complete! Video ID: {VideoId}", videoId);

                return new YouTubeUploadResult
                {
                    Success = true,
                    VideoId = videoId,
                    VideoUrl = $"https://www.youtube.com/watch?v={videoId}"
                };
            }

            return new YouTubeUploadResult
            {
                Success = false,
                ErrorMessage = $"Upload failed with status: {uploadResponse.Status}. {uploadResponse.Exception?.Message}"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "YouTube upload failed");
            return new YouTubeUploadResult
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}
