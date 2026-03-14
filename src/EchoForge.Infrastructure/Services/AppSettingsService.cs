using EchoForge.Core.Interfaces;
using EchoForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.Infrastructure.Services;

public class AppSettingsService : IAppSettingsService
{
    private readonly EchoForgeDbContext _context;
    private readonly IEncryptionService _encryptionService;

    public AppSettingsService(EchoForgeDbContext context, IEncryptionService encryptionService)
    {
        this._context = context;
        this._encryptionService = encryptionService;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null) return null;

        if (setting.IsEncrypted && !string.IsNullOrEmpty(setting.Value))
        {
            try
            {
                return _encryptionService.Decrypt(setting.Value);
            }
            catch
            {
                return null;
            }
        }
        
        return setting.Value;
    }
}
