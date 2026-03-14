using EchoForge.Core.Entities;
using EchoForge.Core.Interfaces;
using EchoForge.Infrastructure.Data;
using Google.Apis.Util.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EchoForge.Infrastructure.Services.YouTube;

public class YouTubeChannelDataStore : IDataStore
{
    private readonly EchoForgeDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<YouTubeChannelDataStore> _logger;
    private readonly string _channelId;

    public YouTubeChannelDataStore(EchoForgeDbContext context, IEncryptionService encryptionService, ILogger<YouTubeChannelDataStore> logger, string channelId)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
        _channelId = channelId;
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        var encrypted = _encryptionService.Encrypt(json);

        var channel = await _context.YouTubeChannels.FirstOrDefaultAsync(c => c.ChannelId == _channelId);
        if (channel == null)
        {
            channel = new YouTubeChannel
            {
                ChannelId = _channelId,
                ChannelName = "Pending Verification...",
                CreatedAt = DateTime.UtcNow
            };
            _context.YouTubeChannels.Add(channel);
        }

        channel.AccessToken = encrypted;
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync<T>(string key)
    {
        var channel = await _context.YouTubeChannels.FirstOrDefaultAsync(c => c.ChannelId == _channelId);
        if (channel != null)
        {
            channel.AccessToken = null;
            await _context.SaveChangesAsync();
        }
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var channel = await _context.YouTubeChannels.FirstOrDefaultAsync(c => c.ChannelId == _channelId);
        if (string.IsNullOrEmpty(channel?.AccessToken)) return default;

        try
        {
            var json = _encryptionService.Decrypt(channel.AccessToken);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt token for channel {ChannelId}", _channelId);
            return default;
        }
    }

    public Task ClearAsync()
    {
        return Task.CompletedTask; // Used broadly, avoid deleting records blindly.
    }
}
