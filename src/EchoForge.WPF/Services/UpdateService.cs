using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using System.Reflection;
using System.Threading.Tasks;

namespace EchoForge.WPF.Services;

public class UpdateService
{
    private static readonly string CurrentVersion = Assembly.GetExecutingAssembly()
        .GetName().Version?.ToString(3) ?? "2.0.0";

    private static readonly string UpdateDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EchoForge", "Updates");

    private readonly string _updateCheckUrl;

    public UpdateService(string updateCheckUrl = "")
    {
        _updateCheckUrl = updateCheckUrl;
        Directory.CreateDirectory(UpdateDir);
    }

    // ─── Data Models ────────────────────────────────────────

    public record UpdateInfo(
        bool Available,
        string LatestVersion,
        string DownloadUrl,
        string ReleaseNotes,
        long FileSizeBytes
    );

    // ─── Check ──────────────────────────────────────────────

    /// <summary>
    /// Check for updates from the configured URL.
    /// Expected JSON response:
    /// {
    ///   "version": "2.1.0",
    ///   "downloadUrl": "https://your-server.com/releases/EchoForge-v2.1.0.zip",
    ///   "releaseNotes": "Bug fixes and new animation module.",
    ///   "fileSizeBytes": 52428800
    /// }
    /// </summary>
    public async Task<UpdateInfo> CheckForUpdateAsync()
    {
        if (string.IsNullOrEmpty(_updateCheckUrl))
            return new UpdateInfo(false, CurrentVersion, "", "", 0);

        try
        {
            using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(10) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("EchoForge-Updater/1.0");
            var response = await client.GetFromJsonAsync<UpdateResponse>(_updateCheckUrl);

            if (response != null && IsNewerVersion(response.Version, CurrentVersion))
            {
                return new UpdateInfo(
                    true,
                    response.Version,
                    response.DownloadUrl ?? "",
                    response.ReleaseNotes ?? "",
                    response.FileSizeBytes
                );
            }
        }
        catch
        {
            // Silent fail — no internet or bad URL
        }

        return new UpdateInfo(false, CurrentVersion, "", "", 0);
    }

    // ─── Download ───────────────────────────────────────────

    /// <summary>
    /// Downloads the update ZIP to local disk. Returns local file path.
    /// Provides progress callback (0.0 to 1.0).
    /// </summary>
    public async Task<string?> DownloadUpdateAsync(UpdateInfo info, Action<double>? onProgress = null)
    {
        if (!info.Available || string.IsNullOrEmpty(info.DownloadUrl))
            return null;

        try
        {
            var fileName = $"EchoForge-v{info.LatestVersion}.zip";
            var localPath = Path.Combine(UpdateDir, fileName);

            // If already downloaded, skip
            if (File.Exists(localPath))
            {
                onProgress?.Invoke(1.0);
                return localPath;
            }

            using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            using var response = await client.GetAsync(info.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? info.FileSizeBytes;
            long receivedBytes = 0;

            await using var contentStream = await response.Content.ReadAsStreamAsync();
            await using var fileStream = new FileStream(localPath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead));
                receivedBytes += bytesRead;

                if (totalBytes > 0)
                    onProgress?.Invoke((double)receivedBytes / totalBytes);
            }

            onProgress?.Invoke(1.0);
            return localPath;
        }
        catch
        {
            return null;
        }
    }

    // ─── Install (Extract + Restart) ────────────────────────

    /// <summary>
    /// Extracts the downloaded update and restarts the application.
    /// Creates a batch script that waits for the current process to exit,
    /// then overwrites the application files and relaunches.
    /// </summary>
    public bool InstallAndRestart(string zipPath)
    {
        try
        {
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var ps1Path = Path.Combine(UpdateDir, "update_installer.ps1");

            // Create a PowerShell script that:
            // 1. Waits for the app to close
            // 2. Extracts the ZIP over the app directory
            // 3. Relaunches the app
            // 4. Cleans up
            var ps1Content = $@"
Start-Sleep -Seconds 3
Expand-Archive -Path '{zipPath}' -DestinationPath '{appDir}' -Force
Start-Process -FilePath '{Path.Combine(appDir, "EchoForge.WPF.exe")}'
Remove-Item -Path '{zipPath}' -Force -ErrorAction SilentlyContinue
Remove-Item -Path `$MyInvocation.MyCommand.Path -Force -ErrorAction SilentlyContinue
";
            File.WriteAllText(ps1Path, ps1Content);

            // Launch the installer script with Admin rights (UAC prompt will be shown)
            Process.Start(new ProcessStartInfo
            {
                FileName = "powershell.exe",
                Arguments = $"-ExecutionPolicy Bypass -WindowStyle Hidden -File \"{ps1Path}\"",
                UseShellExecute = true,
                Verb = "runas"
            });

            // Exit the current app so the updater can replace files
            Environment.Exit(0);
            return true;
        }
        catch
        {
            return false;
        }
    }

    // ─── Helpers ────────────────────────────────────────────

    public static string GetCurrentVersion() => CurrentVersion;

    private static bool IsNewerVersion(string remote, string current)
    {
        if (Version.TryParse(remote, out var remoteVer) && Version.TryParse(current, out var currentVer))
            return remoteVer > currentVer;
        return false;
    }

    private class UpdateResponse
    {
        public string Version { get; set; } = "";
        public string? DownloadUrl { get; set; }
        public string? ReleaseNotes { get; set; }
        public long FileSizeBytes { get; set; }
    }
}
