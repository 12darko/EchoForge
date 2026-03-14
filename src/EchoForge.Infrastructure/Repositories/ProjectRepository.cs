using EchoForge.Core.Interfaces;
using EchoForge.Core.Models;
using EchoForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.Infrastructure.Repositories;

public class ProjectRepository : IProjectRepository
{
    private readonly EchoForgeDbContext _context;

    public ProjectRepository(EchoForgeDbContext context)
    {
        _context = context;
    }

    public async Task<List<Project>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Include(p => p.Template)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public async Task<Project?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _context.Projects
            .Include(p => p.Template)
            .Include(p => p.UploadLogs)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
    }

    public async Task<Project> CreateAsync(Project project, CancellationToken cancellationToken = default)
    {
        _context.Projects.Add(project);
        await _context.SaveChangesAsync(cancellationToken);
        return project;
    }

    public async Task UpdateAsync(Project project, CancellationToken cancellationToken = default)
    {
        project.UpdatedAt = DateTime.UtcNow;
        _context.Projects.Update(project);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync(new object[] { id }, cancellationToken);
        if (project != null)
        {
            // Explicitly load and remove related UploadLogs to ensure deletion works even if DB cascade is missing
            var logs = await _context.UploadLogs.Where(l => l.ProjectId == id).ToListAsync(cancellationToken);
            if (logs.Any())
            {
                _context.UploadLogs.RemoveRange(logs);
            }

            _context.Projects.Remove(project);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateStatusAsync(int projectId, ProjectStatus status, string? errorMessage = null, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync(new object[] { projectId }, cancellationToken);
        if (project != null)
        {
            project.Status = status;
            project.UpdatedAt = DateTime.UtcNow;
            if (errorMessage != null)
                project.ErrorMessage = errorMessage;
            if (status == ProjectStatus.Completed)
                project.CompletedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task UpdateProgressAsync(int projectId, int progress, CancellationToken cancellationToken = default)
    {
        var project = await _context.Projects.FindAsync(new object[] { projectId }, cancellationToken);
        if (project != null && project.PipelineProgress != progress)
        {
            project.PipelineProgress = progress;
            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
