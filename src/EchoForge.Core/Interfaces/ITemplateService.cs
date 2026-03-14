using EchoForge.Core.Models;

namespace EchoForge.Core.Interfaces;

public interface ITemplateService
{
    Task<List<Template>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Template?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Template> CreateAsync(Template template, CancellationToken cancellationToken = default);
    Task<Template> UpdateAsync(Template template, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task SeedDefaultTemplatesAsync(CancellationToken cancellationToken = default);
}
