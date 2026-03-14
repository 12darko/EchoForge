using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using EchoForge.Infrastructure.Data;
using Google.Apis.Util.Store;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace EchoForge.Infrastructure.Services.YouTube;

public class EFDataStore : IDataStore
{
    private readonly EchoForgeDbContext _context;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<EFDataStore> _logger;

    public EFDataStore(EchoForgeDbContext context, IEncryptionService encryptionService, ILogger<EFDataStore> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    public async Task StoreAsync<T>(string key, T value)
    {
        var json = JsonSerializer.Serialize(value);
        var encrypted = _encryptionService.Encrypt(json);
        var dbKey = $"YouTube:Token:{key}";

        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == dbKey);
        if (setting == null)
        {
            _context.AppSettings.Add(new AppSetting
            {
                Key = dbKey,
                Value = encrypted,
                IsEncrypted = true,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            setting.Value = encrypted;
            setting.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync<T>(string key)
    {
        var dbKey = $"YouTube:Token:{key}";
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == dbKey);
        if (setting != null)
        {
            _context.AppSettings.Remove(setting);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<T> GetAsync<T>(string key)
    {
        var dbKey = $"YouTube:Token:{key}";
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == dbKey);
        if (setting == null) return default;

        try
        {
            var json = _encryptionService.Decrypt(setting.Value);
            return JsonSerializer.Deserialize<T>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to decrypt/deserialize token for key {Key}", key);
            return default;
        }
    }

    public async Task ClearAsync()
    {
        var tokens = await _context.AppSettings.Where(s => s.Key.StartsWith("YouTube:Token:")).ToListAsync();
        _context.AppSettings.RemoveRange(tokens);
        await _context.SaveChangesAsync();
    }
}
