using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.DTOs;
using EchoForge.Core.Models;

namespace EchoForge.WPF.ViewModels;

public partial class MixViewModel : ObservableObject
{
    private readonly Services.ApiClient _apiClient;

    // ─── Available Projects (Completed, with rendered video) ───
    [ObservableProperty]
    private ObservableCollection<MixTrackItem> _availableTracks = new();

    // ─── Mix Queue (User's selected order) ───
    [ObservableProperty]
    private ObservableCollection<MixTrackItem> _mixQueue = new();

    [ObservableProperty]
    private MixTrackItem? _selectedAvailableTrack;

    [ObservableProperty]
    private MixTrackItem? _selectedQueueTrack;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    // ─── Mix Settings ───
    [ObservableProperty]
    private double _crossfadeDuration = 2.0; // seconds

    [ObservableProperty]
    private double _targetDurationMinutes = 60.0; // 1 hour target

    [ObservableProperty]
    private string _totalMixDurationDisplay = "0:00:00";

    [ObservableProperty]
    private double _totalMixDurationSeconds = 0;

    [ObservableProperty]
    private string _outputMixPath = "";

    [ObservableProperty]
    private bool _isMixRendering;

    [ObservableProperty]
    private int _mixRenderProgress;

    [ObservableProperty]
    private string _customBackgroundImagePath = "";

    public event Action<string>? RequestOpenFileDialog;
    public event Action<MixTrackItem>? RequestTrackImageDialog;

    [RelayCommand]
    private void SelectBackgroundImage()
    {
        RequestOpenFileDialog?.Invoke("Image Files (*.jpg;*.jpeg;*.png;*.gif)|*.jpg;*.jpeg;*.png;*.gif|All files (*.*)|*.*");
    }

    [RelayCommand]
    private void ClearBackgroundImage()
    {
        CustomBackgroundImagePath = "";
    }

    [RelayCommand]
    private void ChangeTrackImage(MixTrackItem? track)
    {
        if (track == null) return;
        RequestTrackImageDialog?.Invoke(track);
    }

    [RelayCommand]
    private void ClearTrackImage(MixTrackItem? track)
    {
        if (track == null) return;
        track.CustomImagePath = "";
        track.ThumbnailPath = track.OriginalThumbnailPath;
    }

    [RelayCommand]
    private void RemoveTrackEffect(MixTrackItem? track)
    {
        if (track == null) return;
        track.SelectedEffect = "none";
    }

    public MixViewModel(Services.ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    private async Task LoadAvailableTracks()
    {
        IsLoading = true;
        StatusMessage = "⏳ Render edilmiş projeler yükleniyor...";
        try
        {
            var projects = await _apiClient.GetProjectsAsync();
            AvailableTracks.Clear();

            foreach (var p in projects)
            {
                // Only show completed projects with a video file
                if (p.Status == ProjectStatus.Completed && !string.IsNullOrEmpty(p.OutputVideoPath))
                {
                    // Check if file actually exists
                    bool fileExists = System.IO.File.Exists(p.OutputVideoPath);
                    
                    // Get first scene image as thumbnail
                    string thumbnail = "";
                    if (!string.IsNullOrEmpty(p.TimelineJson))
                    {
                        try
                        {
                            var items = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(p.TimelineJson);
                            thumbnail = items?.FirstOrDefault()?.ImagePath ?? "";
                        }
                        catch { }
                    }

                    // Calculate file size
                    string fileSize = "?";
                    if (fileExists)
                    {
                        var info = new System.IO.FileInfo(p.OutputVideoPath);
                        fileSize = info.Length >= 1_073_741_824 ? $"{info.Length / 1_073_741_824.0:F1} GB" : $"{info.Length / 1_048_576.0:F0} MB";
                    }

                    AvailableTracks.Add(new MixTrackItem
                    {
                        ProjectId = p.Id,
                        Title = p.Title,
                        Duration = p.Duration ?? 0,
                        VideoPath = p.OutputVideoPath,
                        AudioPath = p.AudioPath,
                        ThumbnailPath = thumbnail,
                        FileSize = fileSize,
                        FileExists = fileExists
                    });
                }
            }

            StatusMessage = $"✅ {AvailableTracks.Count} render edilmiş proje bulundu.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Hata: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void AddToMix(MixTrackItem? track)
    {
        track ??= SelectedAvailableTrack;
        if (track == null) return;

        MixQueue.Add(new MixTrackItem
        {
            ProjectId = track.ProjectId,
            Title = track.Title,
            Duration = track.Duration,
            VideoPath = track.VideoPath,
            AudioPath = track.AudioPath,
            ThumbnailPath = track.ThumbnailPath,
            OriginalThumbnailPath = track.ThumbnailPath,
            FileSize = track.FileSize,
            FileExists = track.FileExists
        });

        UpdateMixDuration();
        StatusMessage = $"✅ '{track.Title}' mix sırasına eklendi.";
    }

    [RelayCommand]
    private void RemoveFromMix(MixTrackItem? track)
    {
        track ??= SelectedQueueTrack;
        if (track == null) return;

        MixQueue.Remove(track);
        UpdateMixDuration();
        StatusMessage = $"🗑️ '{track.Title}' mix sırasından çıkarıldı.";
    }

    [RelayCommand]
    private void MoveTrackUp()
    {
        if (SelectedQueueTrack == null) return;
        int index = MixQueue.IndexOf(SelectedQueueTrack);
        if (index <= 0) return;
        MixQueue.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveTrackDown()
    {
        if (SelectedQueueTrack == null) return;
        int index = MixQueue.IndexOf(SelectedQueueTrack);
        if (index < 0 || index >= MixQueue.Count - 1) return;
        MixQueue.Move(index, index + 1);
    }

    [RelayCommand]
    private void ClearMix()
    {
        MixQueue.Clear();
        UpdateMixDuration();
        StatusMessage = "🧹 Mix sırası temizlendi.";
    }

    [RelayCommand]
    private void AutoMix()
    {
        if (AvailableTracks.Count == 0)
        {
            StatusMessage = "⚠️ Kullanılabilir proje yok.";
            return;
        }

        MixQueue.Clear();
        double totalSeconds = 0;
        double targetSeconds = TargetDurationMinutes * 60;
        var shuffled = AvailableTracks.OrderBy(_ => Random.Shared.Next()).ToList();

        // Fill the mix queue until we reach the target duration, looping if needed
        int loopIndex = 0;
        while (totalSeconds < targetSeconds && shuffled.Count > 0)
        {
            var track = shuffled[loopIndex % shuffled.Count];
            MixQueue.Add(new MixTrackItem
            {
                ProjectId = track.ProjectId,
                Title = track.Title,
                Duration = track.Duration,
                VideoPath = track.VideoPath,
                AudioPath = track.AudioPath,
                ThumbnailPath = track.ThumbnailPath,
                OriginalThumbnailPath = track.ThumbnailPath,
                FileSize = track.FileSize,
                FileExists = track.FileExists
            });
            totalSeconds += track.Duration;
            loopIndex++;

            // Safety: prevent infinite loop if all durations are 0
            if (loopIndex > 1000) break;
        }

        UpdateMixDuration();
        StatusMessage = $"🎲 Otomatik mix oluşturuldu! {MixQueue.Count} parça, toplam {TotalMixDurationDisplay}";
    }

    private void UpdateMixDuration()
    {
        double total = MixQueue.Sum(t => t.Duration);
        // Subtract crossfade overlaps (N-1 crossfades)
        if (MixQueue.Count > 1)
        {
            total -= (MixQueue.Count - 1) * CrossfadeDuration;
            if (total < 0) total = 0;
        }

        TotalMixDurationSeconds = total;
        int hours = (int)(total / 3600);
        int mins = (int)((total % 3600) / 60);
        int secs = (int)(total % 60);
        TotalMixDurationDisplay = $"{hours}:{mins:D2}:{secs:D2}";
    }

    [RelayCommand]
    private async Task RenderMix()
    {
        if (MixQueue.Count == 0)
        {
            StatusMessage = "⚠️ Mix sırasına en az 1 parça ekleyin.";
            return;
        }

        // Validate all files exist
        var missing = MixQueue.Where(t => !t.FileExists).ToList();
        if (missing.Count > 0)
        {
            StatusMessage = $"❌ {missing.Count} dosya bulunamıyor: {missing.First().Title}";
            return;
        }

        var result = EchoForge.WPF.Views.EchoMessageBox.Show(
            $"Mix render başlatılsın mı?\n{MixQueue.Count} parça, toplam {TotalMixDurationDisplay}\nCrossfade: {CrossfadeDuration}s",
            "Mix Render", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Question);

        if (result != System.Windows.MessageBoxResult.OK) return;

        IsMixRendering = true;
        MixRenderProgress = 0;
        StatusMessage = "🎬 Mix render başlıyor...";

        try
        {
            // Determine output path
            string outputDir = System.IO.Path.GetDirectoryName(MixQueue.First().VideoPath) ?? "";
            string mixFileName = $"EchoForge_Mix_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
            OutputMixPath = System.IO.Path.Combine(outputDir, mixFileName);

            // Build FFmpeg concat command
            await Task.Run(() => RenderMixWithFFmpeg());

            if (System.IO.File.Exists(OutputMixPath))
            {
                StatusMessage = $"✅ Mix render tamamlandı! {OutputMixPath}";
                EchoForge.WPF.Views.EchoMessageBox.Show(
                    $"Mix başarıyla oluşturuldu!\n{OutputMixPath}",
                    "Mix Tamamlandı", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Success);
            }
            else
            {
                StatusMessage = "❌ Mix render başarısız oldu.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Mix render hatası: {ex.Message}";
        }
        finally
        {
            IsMixRendering = false;
            MixRenderProgress = 100;
        }
    }

    private void RenderMixWithFFmpeg()
    {
        // Create a concat file list for FFmpeg
        string tempDir = System.IO.Path.GetTempPath();
        string concatFile = System.IO.Path.Combine(tempDir, $"echoforge_mix_{DateTime.Now:yyyyMMddHHmmss}.txt");

        var lines = new System.Collections.Generic.List<string>();
        foreach (var track in MixQueue)
        {
            // FFmpeg concat format: file 'path'
            lines.Add($"file '{track.VideoPath.Replace("'", "'\\''").Replace("\\", "/")}'");
        }
        System.IO.File.WriteAllLines(concatFile, lines);

        try
        {
            // Find FFmpeg
            string ffmpegPath = "ffmpeg";
            string localFfmpeg = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            if (System.IO.File.Exists(localFfmpeg))
            {
                ffmpegPath = localFfmpeg;
            }

            // Simple concat (no crossfade for performance). Crossfade would require complex filter_complex.
            string args;
            if (!string.IsNullOrEmpty(CustomBackgroundImagePath) && System.IO.File.Exists(CustomBackgroundImagePath))
            {
                // Custom static image or looping GIF over concatenated audio tracks
                bool isGif = CustomBackgroundImagePath.EndsWith(".gif", StringComparison.OrdinalIgnoreCase);
                string loopArg = isGif ? "-ignore_loop 0" : "-loop 1";
                
                // Map the image stream to video and the concat stream to audio
                args = $"{loopArg} -i \"{CustomBackgroundImagePath}\" -f concat -safe 0 -i \"{concatFile}\" -map 0:v -map 1:a -c:v libx264 -crf 18 -preset fast -pix_fmt yuv420p -c:a aac -b:a 192k -shortest -y \"{OutputMixPath}\"";
            }
            else if (CrossfadeDuration > 0 && MixQueue.Count <= 10)
            {
                // For small mixes, use crossfade filter
                args = BuildCrossfadeArgs(concatFile);
            }
            else
            {
                // Simple concat for large/no-crossfade mixes
                args = $"-f concat -safe 0 -i \"{concatFile}\" -c copy -y \"{OutputMixPath}\"";
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process != null)
            {
                process.WaitForExit(600_000); // 10 minute timeout
            }
        }
        finally
        {
            try { System.IO.File.Delete(concatFile); } catch { }
        }
    }

    private string BuildCrossfadeArgs(string concatFile)
    {
        // For crossfade, we need to build a complex filter graph  
        // For simplicity, we use concat demuxer (no crossfade) when filter gets too complex
        // This provides a simple concat which is reliable
        return $"-f concat -safe 0 -i \"{concatFile}\" -c:v libx264 -crf 18 -preset fast -c:a aac -b:a 192k -y \"{OutputMixPath}\"";
    }

    [RelayCommand]
    private void OpenMixFile()
    {
        if (string.IsNullOrEmpty(OutputMixPath) || !System.IO.File.Exists(OutputMixPath))
        {
            StatusMessage = "❌ Mix dosyası bulunamadı.";
            return;
        }
        try
        {
            System.Diagnostics.Process.Start("explorer.exe", $"/select,\"{OutputMixPath}\"");
        }
        catch (Exception ex) { StatusMessage = $"Hata: {ex.Message}"; }
    }
}

/// <summary>
/// Represents a single track item in the mix queue
/// </summary>
public partial class MixTrackItem : ObservableObject
{
    public int ProjectId { get; set; }
    public string Title { get; set; } = "";
    public double Duration { get; set; }
    public string VideoPath { get; set; } = "";
    public string AudioPath { get; set; } = "";
    
    [ObservableProperty]
    private string _thumbnailPath = "";
    
    public string OriginalThumbnailPath { get; set; } = "";
    public string FileSize { get; set; } = "";
    public bool FileExists { get; set; }

    [ObservableProperty]
    private string _customImagePath = "";

    [ObservableProperty]
    private string _selectedEffect = "none";

    public static List<string> EffectOptions { get; } = new()
    {
        "none", "bw", "sepia", "vhs", "cinematic", "dreamy", "vintage", "vivid"
    };

    public string DurationDisplay
    {
        get
        {
            int mins = (int)(Duration / 60);
            int secs = (int)(Duration % 60);
            return $"{mins}:{secs:D2}";
        }
    }

    partial void OnCustomImagePathChanged(string value)
    {
        if (!string.IsNullOrEmpty(value) && System.IO.File.Exists(value))
        {
            ThumbnailPath = value;
        }
    }
}
