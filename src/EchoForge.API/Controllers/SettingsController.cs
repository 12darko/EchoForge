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

    public SettingsController(EchoForgeDbContext context, IEncryptionService encryptionService)
    {
        _context = context;
        _encryptionService = encryptionService;
    }

    [HttpGet]
    public async Task<ActionResult> GetAll()
    {
        var settings = await _context.AppSettings.ToListAsync();
        
        var result = new List<object>();
        foreach (var s in settings)
        {
            string val = s.Value;
            if (s.IsEncrypted)
            {
                try { val = _encryptionService.Decrypt(s.Value); }
                catch { val = ""; } // Return empty if decryption fails (corrupted)
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

    [HttpGet("{key}")]
    public async Task<ActionResult> GetSetting(string key)
    {
        var setting = await _context.AppSettings.FirstOrDefaultAsync(s => s.Key == key);
        if (setting == null) return NotFound();

        var value = setting.IsEncrypted ? _encryptionService.Decrypt(setting.Value) : setting.Value;
        return Ok(new { Key = setting.Key, Value = value });
    }
}

public class SaveSettingRequest
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public bool Encrypt { get; set; } = false;
}


