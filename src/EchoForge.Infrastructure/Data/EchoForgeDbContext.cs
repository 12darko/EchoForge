using EchoForge.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace EchoForge.Infrastructure.Data;

public class EchoForgeDbContext : DbContext
{
    public EchoForgeDbContext(DbContextOptions<EchoForgeDbContext> options) : base(options) { }

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<Template> Templates => Set<Template>();
    public DbSet<UploadLog> UploadLogs => Set<UploadLog>();
    public DbSet<AppSetting> AppSettings => Set<AppSetting>();
    public DbSet<EchoForge.Core.Entities.YouTubeChannel> YouTubeChannels => Set<EchoForge.Core.Entities.YouTubeChannel>();
    public DbSet<User> Users => Set<User>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.CreatedAt);
            entity.HasOne(e => e.Template)
                  .WithMany(t => t.Projects)
                  .HasForeignKey(e => e.TemplateId)
                  .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne(e => e.TargetChannel)
                  .WithMany()
                  .HasForeignKey(e => e.TargetChannelId)
                  .OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<EchoForge.Core.Entities.YouTubeChannel>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ChannelId).IsUnique();
        });

        modelBuilder.Entity<Template>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<UploadLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.ProjectId);
            entity.HasOne(e => e.Project)
                  .WithMany(p => p.UploadLogs)
                  .HasForeignKey(e => e.ProjectId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppSetting>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Key).IsUnique();
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Username).IsUnique();
            
            // Seed default admin user (Password object requires hashing, we will use BCrypt)
            // The hash for 'admin' with BCrypt usually looks like $2a$11$...
            // We'll generate a known hash for 'admin' (using BCrypt Net Next)
            entity.HasData(new User 
            {
                Id = 1,
                Username = "admin",
                // This is the BCrypt hash for "admin"
                PasswordHash = "$2a$11$tDWeS4K7Jj7hA.D0BqP5b.p5Y8Yt8Gj.1YyYqPjP.yO/Qp9/g5A.y",
                Email = "admin@echoforge.local",
                CreatedAt = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                IsActive = true,
                IsAdmin = true
            });
        });
    }
}
