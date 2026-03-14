namespace EchoForge.Core.Interfaces;

public interface IAppSettingsService
{
    Task<string?> GetSettingAsync(string key);
}
