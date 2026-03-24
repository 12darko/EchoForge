using EchoForge.Core.Models;

namespace EchoForge.Core.Interfaces;

public interface IProjectRepository
{
    Task<List<Project>> GetAllAsync(int? userId = null, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default);
    Task UpdateAsync(Project project, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task UpdateStatusAsync(int projectId, ProjectStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);
    Task UpdateProgressAsync(int projectId, int progress, CancellationToken cancellationToken = default);
}
