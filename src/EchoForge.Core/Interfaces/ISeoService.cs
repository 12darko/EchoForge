using EchoForge.Core.DTOs;

namespace EchoForge.Core.Interfaces;

public interface ISeoService
{
    Task<SeoResult> GenerateSeoAsync(string projectTitle, string templateName, string genre, string language = "English", string? customInstructions = null, string? targetPlatforms = null, CancellationToken cancellationToken = default);
}
