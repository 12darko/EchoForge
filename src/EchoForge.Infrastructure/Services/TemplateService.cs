using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using EchoForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services;

public class TemplateService : ITemplateService
{
    private readonly EchoForgeDbContext _context;
    private readonly ILogger<TemplateService> _logger;

    public TemplateService(EchoForgeDbContext context, ILogger<TemplateService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Template>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Templates.OrderBy(t => t.Name).ToListAsync(cancellationToken);
    }

    public async Task<Template?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Templates.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Template> CreateAsync(Template template, CancellationToken cancellationToken = default)
    {
        _context.Templates.Add(template);
        await _context.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task<Template> UpdateAsync(Template template, CancellationToken cancellationToken = default)
    {
        _context.Templates.Update(template);
        await _context.SaveChangesAsync(cancellationToken);
        return template;
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var template = await _context.Templates.FindAsync(new object[] { id }, cancellationToken);
        if (template != null)
        {
            _context.Templates.Remove(template);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task SeedDefaultTemplatesAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Templates.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Templates already seeded, skipping");
            return;
        }

        var defaultTemplates = new List<Template>
        {
            new()
            {
                Name = "Dark Phonk",
                ImagePromptBase = "dark cyberpunk city, neon lights, cinematic lighting, rain-soaked streets, purple and red hues",
                Transition = "fade",
                OverlayFont = "BebasNeue",
                CutMode = "beat",
                ColorTheme = "dark"
            },
            new()
            {
                Name = "Lo-Fi Chill",
                ImagePromptBase = "cozy anime room, warm lighting, study desk with coffee, rainy window, lo-fi aesthetic, soft pastel colors",
                Transition = "fade",
                OverlayFont = "Poppins",
                CutMode = "beat",
                ColorTheme = "warm"
            },
            new()
            {
                Name = "Synthwave Retro",
                ImagePromptBase = "retrowave landscape, neon grid, sunset horizon, chrome palm trees, 80s retro futurism, vibrant pink and cyan",
                Transition = "fade",
                OverlayFont = "Orbitron",
                CutMode = "beat",
                ColorTheme = "neon"
            },
            new()
            {
                Name = "Epic Cinematic",
                ImagePromptBase = "epic fantasy landscape, dramatic mountains, golden hour lighting, cinematic wide shot, volumetric clouds",
                Transition = "fade",
                OverlayFont = "Montserrat",
                CutMode = "beat",
                ColorTheme = "cinematic"
            },
            new()
            {
                Name = "Abstract Minimal",
                ImagePromptBase = "abstract geometric shapes, clean minimal design, smooth gradients, modern art style, pastel tones",
                Transition = "fade",
                OverlayFont = "Inter",
                CutMode = "time",
                ColorTheme = "minimal"
            },
            new()
            {
                Name = "Dark Trap",
                ImagePromptBase = "dark urban nightscape, graffiti walls, smoke effects, neon signs, gritty atmosphere, red and blue lighting",
                Transition = "fade",
                OverlayFont = "BebasNeue",
                CutMode = "beat",
                ColorTheme = "dark"
            },
            new()
            {
                Name = "Nature Ambient",
                ImagePromptBase = "serene nature landscape, crystal clear lake, aurora borealis, misty forest, peaceful atmosphere, ethereal lighting",
                Transition = "fade",
                OverlayFont = "Lato",
                CutMode = "time",
                ColorTheme = "nature"
            },
            new()
            {
                Name = "Anime Style",
                ImagePromptBase = "anime scenery, cherry blossom trees, japanese street, golden sunset, studio ghibli inspired, detailed background art",
                Transition = "fade",
                OverlayFont = "NotoSansJP",
                CutMode = "beat",
                ColorTheme = "anime"
            }
        };

        _context.Templates.AddRange(defaultTemplates);
        await _context.SaveChangesAsync(cancellationToken);
        _logger.LogInformation("Seeded {Count} default templates", defaultTemplates.Count);
    }
}
