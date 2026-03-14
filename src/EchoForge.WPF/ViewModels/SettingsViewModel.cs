using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace EchoForge.WPF.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly Services.ApiClient _apiClient;

    [ObservableProperty]
    private string _grokApiKey = string.Empty;

    [ObservableProperty]
    private string _seoLanguage = "English";

    [ObservableProperty]
    private string _appLanguage = "en";

    partial void OnAppLanguageChanged(string value)
    {
        EchoForge.WPF.Localization.TranslationManager.Instance.CurrentLanguage = value;
    }



    [ObservableProperty]
    private int _videoFps = 30;



    public List<string> AvailableLanguages { get; } = new() { "English", "Turkish", "German", "Spanish", "French" };
    public List<string> AvailableAppLanguages { get; } = new() { "en", "tr" };
    public List<int> AvailableFps { get; } = new() { 24, 30, 60 };

    [ObservableProperty]
    private string _youTubeClientId = string.Empty;

    [ObservableProperty]
    private string _youTubeClientSecret = string.Empty;

    [ObservableProperty]
    private string _ffmpegPath = "ffmpeg";

    [ObservableProperty]
    private string _apiBaseUrl = "http://localhost:5000";

    [ObservableProperty]
    private int _dailyUploadLimit = 5;

    [ObservableProperty]
    private string _outputDirectory = string.Empty;

    [ObservableProperty]
    private string _defaultIntroVideoPath = string.Empty;

    [ObservableProperty]
    private string _defaultOutroVideoPath = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isAdmin;

    public SettingsViewModel(Services.ApiClient apiClient, bool isAdmin = false)
    {
        _apiClient = apiClient;
        _isAdmin = isAdmin;
        LoadInitialSettings();
    }

    private async void LoadInitialSettings()
    {
        try
        {
            var settings = await _apiClient.GetAllSettingsAsync();
            if (settings.Count > 0)
            {
                var grokKey = settings.FirstOrDefault(s => s.Key == "Grok:ApiKey")?.Value;
                if (!string.IsNullOrEmpty(grokKey)) GrokApiKey = grokKey;

                var seoLang = settings.FirstOrDefault(s => s.Key == "Seo:Language")?.Value;
                if (!string.IsNullOrEmpty(seoLang)) SeoLanguage = seoLang;

                var appLang = settings.FirstOrDefault(s => s.Key == "App:Language")?.Value;
                if (!string.IsNullOrEmpty(appLang)) AppLanguage = appLang;

                var fps = settings.FirstOrDefault(s => s.Key == "Video:Fps")?.Value;
                if (int.TryParse(fps, out int f)) VideoFps = f;

                var clientId = settings.FirstOrDefault(s => s.Key == "YouTube:ClientId")?.Value;
                if (!string.IsNullOrEmpty(clientId)) YouTubeClientId = clientId;

                var clientSecret = settings.FirstOrDefault(s => s.Key == "YouTube:ClientSecret")?.Value;
                if (!string.IsNullOrEmpty(clientSecret)) YouTubeClientSecret = clientSecret;

                var ffmpeg = settings.FirstOrDefault(s => s.Key == "FFmpeg:Path")?.Value;
                if (!string.IsNullOrEmpty(ffmpeg)) FfmpegPath = ffmpeg;

                var limit = settings.FirstOrDefault(s => s.Key == "DailyUploadLimit")?.Value;
                if (int.TryParse(limit, out int l)) DailyUploadLimit = l;

                var outputDir = settings.FirstOrDefault(s => s.Key == "Output:Directory")?.Value;
                if (!string.IsNullOrEmpty(outputDir)) OutputDirectory = outputDir;
                else OutputDirectory = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");

                var introPath = settings.FirstOrDefault(s => s.Key == "Branding:IntroVideoPath")?.Value;
                if (!string.IsNullOrEmpty(introPath)) DefaultIntroVideoPath = introPath;

                var outroPath = settings.FirstOrDefault(s => s.Key == "Branding:OutroVideoPath")?.Value;
                if (!string.IsNullOrEmpty(outroPath)) DefaultOutroVideoPath = outroPath;
                
                var baseUrl = settings.FirstOrDefault(s => s.Key == "ApiBaseUrl")?.Value;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load settings: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveSettings()
    {
        try
        {
            StatusMessage = "Saving settings...";

            if (!string.IsNullOrEmpty(GrokApiKey))
                await _apiClient.SaveSettingAsync("Grok:ApiKey", GrokApiKey, true);
            
            await _apiClient.SaveSettingAsync("Seo:Language", SeoLanguage, false);
            await _apiClient.SaveSettingAsync("App:Language", AppLanguage, false);
            await _apiClient.SaveSettingAsync("Video:Fps", VideoFps.ToString(), false);

            if (!string.IsNullOrEmpty(YouTubeClientId))
                await _apiClient.SaveSettingAsync("YouTube:ClientId", YouTubeClientId, true);

            if (!string.IsNullOrEmpty(YouTubeClientSecret))
                await _apiClient.SaveSettingAsync("YouTube:ClientSecret", YouTubeClientSecret, true);

            await _apiClient.SaveSettingAsync("FFmpeg:Path", FfmpegPath, false);
            await _apiClient.SaveSettingAsync("DailyUploadLimit", DailyUploadLimit.ToString(), false);
            await _apiClient.SaveSettingAsync("Output:Directory", OutputDirectory, false);
            await _apiClient.SaveSettingAsync("Branding:IntroVideoPath", DefaultIntroVideoPath, false);
            await _apiClient.SaveSettingAsync("Branding:OutroVideoPath", DefaultOutroVideoPath, false);
            await _apiClient.SaveSettingAsync("ApiBaseUrl", ApiBaseUrl, false);
            
            // Also update the active API Client's URL
            _apiClient.SetBaseUrl(ApiBaseUrl);

            StatusMessage = "✅ Settings saved successfully!";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
    }

    // Channels are now managed via the Channels View.
    [RelayCommand]
    private void ConnectYouTube()
    {
        StatusMessage = "ℹ️ Please use the 'My Channels' menu to connect and manage YouTube accounts.";
    }

    [RelayCommand]
    private async Task TestConnection()
    {
        try
        {
            StatusMessage = "Testing connection...";
            
            // Temporary set URL to test
            _apiClient.SetBaseUrl(ApiBaseUrl);
            
            // We can check /health or /api/settings as a ping
            var isConnected = await _apiClient.TestConnectionAsync();
            if (isConnected)
            {
                StatusMessage = "✅ Connection successful!";
                EchoForge.WPF.Views.EchoMessageBox.Show($"Successfully connected to: {ApiBaseUrl}", "Connection Test", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Success);
            }
            else
            {
                StatusMessage = "❌ Server reached, but returned unexpected response.";
                EchoForge.WPF.Views.EchoMessageBox.Show("Server responded, but not as expected.", "Connection Test", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Connection failed: {ex.Message}";
            EchoForge.WPF.Views.EchoMessageBox.Show($"Could not connect to the remote server.\n\nError: {ex.Message}", "Connection Test Failed", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
    }

    [RelayCommand]
    private void BrowseFfmpeg()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "FFmpeg|ffmpeg.exe|All Files|*.*",
            Title = "Select FFmpeg executable"
        };

        if (dialog.ShowDialog() == true)
        {
            FfmpegPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseOutputDirectory()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog
        {
            Title = "Select Output Directory",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
        {
             OutputDirectory = dialog.FolderName;
        }
    }

    [RelayCommand]
    private void BrowseIntroVideo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov|All Files|*.*",
            Title = "Select Default Intro Video"
        };
        if (dialog.ShowDialog() == true)
        {
            DefaultIntroVideoPath = dialog.FileName;
        }
    }

    [RelayCommand]
    private void BrowseOutroVideo()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Filter = "Video Files|*.mp4;*.mkv;*.avi;*.mov|All Files|*.*",
            Title = "Select Default Outro Video"
        };
        if (dialog.ShowDialog() == true)
        {
            DefaultOutroVideoPath = dialog.FileName;
        }
    }

    // ─── Update System ──────────────────────────────────────
    
    [ObservableProperty]
    private string _currentVersion = Services.UpdateService.GetCurrentVersion();

    [ObservableProperty]
    private string _updateStatusText = "";

    [ObservableProperty]
    private double _updateProgress;

    [ObservableProperty]
    private bool _isCheckingUpdate;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _latestVersionText = "";

    private Services.UpdateService.UpdateInfo? _pendingUpdate;

    [RelayCommand]
    private async Task CheckForUpdates()
    {
        IsCheckingUpdate = true;
        UpdateStatusText = "🔍 Checking for updates...";
        UpdateProgress = 0;
        IsUpdateAvailable = false;

        try
        {
            var apiBase = Services.ApiClient.Instance?.BaseUrl?.TrimEnd('/') ?? ApiBaseUrl.TrimEnd('/');
            var updateUrl = $"{apiBase}/api/update/check";
            var updater = new Services.UpdateService(updateUrl);
            var info = await updater.CheckForUpdateAsync();

            if (info.Available)
            {
                _pendingUpdate = info;
                IsUpdateAvailable = true;
                LatestVersionText = info.LatestVersion;
                UpdateStatusText = $"🆕 New version v{info.LatestVersion} available!\n{info.ReleaseNotes}";
            }
            else
            {
                UpdateStatusText = "✅ You are on the latest version.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"❌ Update check failed: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }

    [RelayCommand]
    private async Task DownloadAndInstallUpdate()
    {
        if (_pendingUpdate == null || !_pendingUpdate.Available) return;

        IsCheckingUpdate = true;
        UpdateStatusText = "⬇️ Downloading update...";
        UpdateProgress = 0;

        try
        {
            var apiBase = Services.ApiClient.Instance?.BaseUrl?.TrimEnd('/') ?? ApiBaseUrl.TrimEnd('/');
            var updateUrl = $"{apiBase}/api/update/check";
            var updater = new Services.UpdateService(updateUrl);

            var zipPath = await updater.DownloadUpdateAsync(_pendingUpdate, progress =>
            {
                System.Windows.Application.Current.Dispatcher.Invoke(() =>
                {
                    UpdateProgress = progress * 100;
                    UpdateStatusText = $"⬇️ Downloading... {(progress * 100):F0}%";
                });
            });

            if (!string.IsNullOrEmpty(zipPath))
            {
                UpdateStatusText = "🔄 Installing update and restarting...";
                await Task.Delay(500);
                updater.InstallAndRestart(zipPath);
            }
            else
            {
                UpdateStatusText = "❌ Download failed.";
            }
        }
        catch (Exception ex)
        {
            UpdateStatusText = $"❌ Update failed: {ex.Message}";
        }
        finally
        {
            IsCheckingUpdate = false;
        }
    }
}
