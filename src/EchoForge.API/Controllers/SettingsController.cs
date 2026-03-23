using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using EchoForge.Infrastructure.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class SettingsController : ControllerBase
{
    private readonly EchoForgeDbContext _context;
    private readonly IEncryptionService _encryptionService;

    // Sunucu tarafında yönetilen (admin-only) ayar anahtarları
    // Bu anahtarlar normal kullanıcılara maskelenmiş döner
    private static readonly HashSet<string> _serverManagedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Grok:ApiKey",
        "YouTube:ClientId",
        "YouTube:ClientSecret",
        "ApiBaseUrl"
    };

    public SettingsController(EchoForgeDbContext context, IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    /// <summary>
    /// Tüm ayarları döndürür.
    /// isAdmin=false ise şifreli ve sunucu-yönetimli anahtarlar maskelenir.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult> GetAll([FromQuery] bool isAdmin = false)
    {
        var settings = await _context.AppSettings.ToListAsync();
        
        var result = new List<object>();
        foreach (var s in settings)
        {
            string val = s.Value;

            if (s.IsEncrypted)
            {
                if (!isAdmin)
                {
                    // Normal kullanıcıya maskelenmiş değer döndür
                    val = "••••••••";
                }
                else
                {
                    try { val = _encryptionService.Decrypt(s.Value); }
                    catch { val = ""; }
                }
            }
            else if (!isAdmin && _serverManagedKeys.Contains(s.Key))
            {
                // Şifrelenmemiş ama sunucu-yönetimli anahtar
                val = "••••••••";
            }
            
            result.Add(new
            {
                s.Key,
                Value = val,
                s.IsEncrypted,
                s.UpdatedAt
            });
        }
        return Ok(result);
    }

    /// <summary>
    /// Ayar kaydet — sadece admin kullanılmalı (WPF tarafında kontrol edilir)
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> SaveSetting([FromBody] SaveSettingRequest request)
    {
        var existing = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == request.Key);

        var value = request.Encrypt ? _encryptionService.Encrypt(request.Value) : request.Value;

        if (existing != null)
        {
            existing.Value = value;
            existing.IsEncrypted = request.Encrypt;
            existing.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            _context.AppSettings.Add(new AppSetting
            {
                Key = request.Key,
                Value = value,
                IsEncrypted = request.Encrypt,
                UpdatedAt = DateTime.UtcNow
            });
        }

        await _context.SaveChangesAsync();
        return Ok(new { message = "Setting saved" });
    }

    /// <summary>
    /// Tek bir ayar oku. isAdmin=false ise şifreli değer maskelenir.
    /// </summary>
    [HttpGet("{key}")]
    public async Task<ActionResult> GetSetting(string key, [FromQuery] bool isAdmin = false)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null) return NotFound();

        string value;
        if (setting.IsEncrypted)
        {
            if (!isAdmin)
                value = "••••••••";
            else
            {
                try { value = _encryptionService.Decrypt(setting.Value); }
                catch { value = ""; }
            }
        }
        else if (!isAdmin && _serverManagedKeys.Contains(setting.Key))
        {
            value = "••••••••";
        }
        else
        {
            value = setting.Value;
        }

        return Ok(new { Key = setting.Key, Value = value });
    }

    /// <summary>
    /// Bir ayarı sil (admin-only)
    /// </summary>
    [HttpDelete("{key}")]
    public async Task<ActionResult> DeleteSetting(string key)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null) return NotFound(new { message = "Setting not found" });

        _context.AppSettings.Remove(setting);
        await _context.SaveChangesAsync();
        return Ok(new { message = $"Setting '{key}' deleted." });
    }
}

public class SaveSettingRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Encrypt { get; set; } = false;
}


