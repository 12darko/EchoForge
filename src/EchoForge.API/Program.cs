using EchoForge.Core.Interfaces;
using EchoForge.Infrastructure.Data;
using EchoForge.Infrastructure.Repositories;
using EchoForge.Infrastructure.Services;
using EchoForge.Infrastructure.Services.Audio;
using EchoForge.Infrastructure.Services.Image;
using EchoForge.Infrastructure.Services.SEO;
using EchoForge.Infrastructure.Services.Video;
using EchoForge.Infrastructure.Services.YouTube;
using Hangfire;
using Hangfire.InMemory;
using Microsoft.EntityFrameworkCore;

using EchoForge.API.Logging;

var builder = WebApplication.CreateBuilder(args);

// ========================
// Logging (File Logger)
// ========================
var logPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs.txt");
builder.Logging.ClearProviders(); // clear default console if wanted, or just append
builder.Logging.AddConsole();
builder.Logging.AddFileLogger(logPath);

// ========================
// Database (MySQL via Pomelo)
// ========================
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Server=localhost;Database=echoforge;User=root;Password=;";

builder.Services.AddDbContext<EchoForgeDbContext>(options =>
    options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString)));

// ========================
// Hangfire (In-Memory for dev, switch to MySQL for prod)
// ========================
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseInMemoryStorage());

builder.Services.AddHangfireServer();

// ========================
// Core Services DI
// ========================
builder.Services.AddScoped<IProjectRepository, ProjectRepository>();
builder.Services.AddScoped<ITemplateService, TemplateService>();
builder.Services.AddSingleton<IEncryptionService>(new EncryptionService(
    builder.Configuration["Encryption:Key"] ?? "EchoForge_Default_Key_2024!@#$"));

builder.Services.AddScoped<IAudioService>(sp =>
    new AudioAnalysisService(
        sp.GetRequiredService<ILogger<AudioAnalysisService>>(),
        builder.Configuration["FFmpeg:Path"]));

builder.Services.AddScoped<IAppSettingsService, AppSettingsService>();

// Image Generation — HuggingFace API (Dynamic Key via DB)
builder.Services.AddHttpClient<IImageGenerationService, HuggingFaceImageService>();
builder.Services.AddScoped<IImageGenerationService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<HuggingFaceImageService>>();
    var appSettingsService = sp.GetRequiredService<IAppSettingsService>();
    var client = httpClientFactory.CreateClient(nameof(HuggingFaceImageService));
    return new HuggingFaceImageService(client, logger, appSettingsService, null);
});

builder.Services.AddScoped<IVideoComposerService>(sp =>
    new VideoComposerService(
        sp.GetRequiredService<ILogger<VideoComposerService>>(),
        builder.Configuration["FFmpeg:Path"],
        builder.Configuration["Output:Directory"]));

builder.Services.AddHttpClient<ISeoService, GroqSeoService>();
builder.Services.AddScoped<ISeoService>(sp =>
{
    var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
    var logger = sp.GetRequiredService<ILogger<GroqSeoService>>();
    var appSettingsService = sp.GetRequiredService<IAppSettingsService>();
    var client = httpClientFactory.CreateClient(nameof(GroqSeoService));
    return new GroqSeoService(client, logger, appSettingsService);
});

builder.Services.AddScoped<IYouTubeUploadService, YouTubeUploadService>();

// ========================
// API Configuration
// ========================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "EchoForge API", Version = "v1" });
});

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// ========================
// Database Migration & Seeding
// ========================
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<EchoForge.Infrastructure.Data.EchoForgeDbContext>();
    await db.Database.EnsureCreatedAsync();

    // Helper method to safely add columns without throwing EF Core log errors
    async Task AddColumnIfNotExistsAsync(Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade database, string tableName, string columnName, string columnDefinition)
    {
        var checkExistsSql = $"SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = '{tableName}' AND COLUMN_NAME = '{columnName}';";
        
        using var command = database.GetDbConnection().CreateCommand();
        command.CommandText = checkExistsSql;
        await database.OpenConnectionAsync();
        
        var count = Convert.ToInt32(await command.ExecuteScalarAsync());
        if (count == 0)
        {
            try { await database.ExecuteSqlRawAsync($"ALTER TABLE {tableName} ADD COLUMN {columnName} {columnDefinition};"); } catch { }
        }
    }

    // TEMPORARY DB SCHEMA PATCH FOR PHASE 3 AND 4
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "ExtractAutoShorts", "TINYINT(1) NOT NULL DEFAULT 0");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "TransitionStyle", "VARCHAR(50) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "CustomInstructions", "VARCHAR(1000) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "TargetPlatforms", "VARCHAR(200) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "VisualEffect", "VARCHAR(50) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "ManualImageDurationSec", "DOUBLE NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "ImageStyle", "VARCHAR(200) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "UserId", "INT NULL");

    // TEMPORARY DB SCHEMA PATCH FOR PHASE 5 AND LATER
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "PipelineProgress", "INT NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "PrivacyStatus", "VARCHAR(20) NOT NULL DEFAULT 'private'");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "OutputVideoPath", "VARCHAR(500) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "YouTubeVideoId", "VARCHAR(50) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "SeoTitle", "VARCHAR(200) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "SeoDescription", "TEXT NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "SeoTags", "VARCHAR(2000) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "SeoHashtags", "VARCHAR(1000) NULL");
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "ErrorMessage", "VARCHAR(2000) NULL");

    // TEMPORARY DB SCHEMA PATCH FOR PHASE 6
    try { await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS YouTubeChannels (Id INT AUTO_INCREMENT PRIMARY KEY, ChannelName VARCHAR(200) NOT NULL, ChannelId VARCHAR(100) NOT NULL, AccessToken TEXT NULL, RefreshToken TEXT NULL, TokenExpiration DATETIME NULL, CreatedAt DATETIME NOT NULL, UNIQUE (ChannelId));"); } catch { }
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "TargetChannelId", "INT NULL");
    try { await db.Database.ExecuteSqlRawAsync("ALTER TABLE Projects ADD CONSTRAINT FK_Projects_YouTubeChannels FOREIGN KEY (TargetChannelId) REFERENCES YouTubeChannels(Id) ON DELETE SET NULL;"); } catch { }
    await AddColumnIfNotExistsAsync(db.Database, "Projects", "TimelineJson", "TEXT NULL");

    try { await db.Database.ExecuteSqlRawAsync("CREATE TABLE IF NOT EXISTS UploadLogs (Id INT AUTO_INCREMENT PRIMARY KEY, ProjectId INT NOT NULL, Status VARCHAR(50) NOT NULL DEFAULT 'Pending', ResponseJson TEXT NULL, ErrorMessage VARCHAR(2000) NULL, CreatedAt DATETIME NOT NULL, CONSTRAINT FK_UploadLogs_Projects FOREIGN KEY (ProjectId) REFERENCES Projects(Id) ON DELETE CASCADE);"); } catch { }

    // TEMPORARY DB SCHEMA PATCH FOR PHASE 5 (Users & Auth)
    try {
        await db.Database.ExecuteSqlRawAsync(@"
            CREATE TABLE IF NOT EXISTS Users (
                Id INT AUTO_INCREMENT PRIMARY KEY, 
                Username VARCHAR(100) NOT NULL, 
                PasswordHash VARCHAR(255) NOT NULL, 
                Email VARCHAR(255) NOT NULL, 
                CreatedAt DATETIME(6) NOT NULL, 
                LastLoginAt DATETIME(6) NULL, 
                IsActive TINYINT(1) NOT NULL DEFAULT 1, 
                UNIQUE (Username)
            );"); 
            
        var checkAdmin = "SELECT COUNT(*) FROM Users WHERE Username = 'admin'";
        using var cmd = db.Database.GetDbConnection().CreateCommand();
        cmd.CommandText = checkAdmin;
        if (db.Database.GetDbConnection().State != System.Data.ConnectionState.Open)
            await db.Database.OpenConnectionAsync();
        var adminCount = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        if (adminCount == 0)
        {
            var insertAdmin = "INSERT INTO Users (Username, PasswordHash, Email, CreatedAt, IsActive) VALUES ('admin', '$2a$11$tDWeS4K7Jj7hA.D0BqP5b.p5Y8Yt8Gj.1YyYqPjP.yO/Qp9/g5A.y', 'admin@echoforge.local', UTC_TIMESTAMP(), 1)";
            cmd.CommandText = insertAdmin;
            await cmd.ExecuteNonQueryAsync();
        }
    } catch { }

    var templateService = scope.ServiceProvider.GetRequiredService<ITemplateService>();
    await templateService.SeedDefaultTemplatesAsync();
}

// ========================
// Middleware Pipeline
// ========================
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // Required to serve .zip files from wwwroot/updates
app.UseCors();
app.UseHangfireDashboard("/hangfire");
app.MapControllers();

app.Run();
