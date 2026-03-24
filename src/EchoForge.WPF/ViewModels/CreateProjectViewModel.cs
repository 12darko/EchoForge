using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.Models;
using Microsoft.Win32;

namespace EchoForge.WPF.ViewModels;

public partial class CreateProjectViewModel : ObservableObject
{
    private readonly Services.ApiClient _apiClient;
    private readonly Services.ClientJobOrchestrator _orchestrator;

    public event Action? ProjectCreated;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _audioFilePath = string.Empty;

    [ObservableProperty]
    private int _selectedTemplateId;

    [ObservableProperty]
    private FormatType _selectedFormat = FormatType.Vertical_9x16;

    [ObservableProperty]
    private ObservableCollection<Template> _templates = new();

    [ObservableProperty]
    private Template? _selectedTemplate;

    [ObservableProperty]
    private bool _isCreating;

    [ObservableProperty]
    private string _statusText = string.Empty;

    [ObservableProperty]
    private string _privacyStatus = "private";

    [ObservableProperty]
    private string _uploadType = "Short"; // Default to Shorts (Vertical)

    [ObservableProperty]
    private bool _extractAutoShorts;

    // --- Pollinations Image Settings ---
    [ObservableProperty]
    private string _imageModel = "flux";

    [ObservableProperty]
    private int _uniqueImageCount = 8;

    [ObservableProperty]
    private string _imageStyle = string.Empty;

    [ObservableProperty]
    private string _customInstructions = string.Empty;

    [ObservableProperty]
    private bool _isYouTubeSelected = true;

    [ObservableProperty]
    private bool _isTikTokSelected = false;

    [ObservableProperty]
    private bool _isInstagramSelected = false;

    [ObservableProperty]
    private ObservableCollection<EchoForge.Core.Entities.YouTubeChannel> _availableChannels = new();

    [ObservableProperty]
    private int? _selectedChannelId;

    [ObservableProperty]
    private string _selectedTransition = DefaultTransition;
    public ObservableCollection<string> TransitionOptions { get; } = new()
    {
        "Template Default", "none", "fade", "zoompan", "smoothleft", "smoothright", "circlecrop", "rectcrop", "distance", "wipeleft", "wiperight"
    };
    private const string DefaultTransition = "Template Default";

    [ObservableProperty]
    private VisualEffectOption? _selectedVisualEffect;
    
    public List<VisualEffectOption> VisualEffectOptions { get; } = new()
    {
        new(null, "🚫 No VFX (Clean)"),
        new("bw", "🎞️ Siyah & Beyaz (B&W)"),
        new("sepia", "🕰️ Vintage (Sepya + Noise)"),
        new("vhs", "📼 VHS / Glitch (RGB Shift)"),
        new("cinematic", "🎥 Sinematik (Kontrast + Vignette)"),
        new("dreamy", "✨ Dreamy (Glow/Soft Focus)")
    };

    [ObservableProperty]
    private ImageDurationOption? _selectedImageDuration;

    public List<ImageDurationOption> ImageDurationOptions { get; } = new()
    {
        new(null, "🔄 Auto (Match Beats)"),
        new(10, "⏱️ 10 seconds per image"),
        new(30, "⏳ 30 seconds per image"),
        new(60, "⏲️ 1 minute per image"),
        new(300, "🖼️ Single image (5 mins)")
    };

    public List<string> PrivacyOptions { get; } = new() { "private", "unlisted", "public" };

    public List<ImageModelOption> ImageModelOptions { get; } = new()
    {
        new("flux", "⚡ Flux Schnell", "Hızlı, iyi kalite"),
        new("turbo", "🎨 Turbo", "Yüksek kalite, yavaş"),
        new("flux-realism", "📸 Flux Realism", "Gerçekçi görseller")
    };

    public List<int> UniqueImageCountOptions { get; } = new() { 1, 2, 3, 4, 5, 6, 8, 10, 12, 15, 20 };

    partial void OnUploadTypeChanged(string value)
    {
        if (value == "Short")
            SelectedFormat = FormatType.Vertical_9x16;
        else
            SelectedFormat = FormatType.Standard_16x9;
    }

    public CreateProjectViewModel(Services.ApiClient apiClient, Services.ClientJobOrchestrator orchestrator)
    {
        _apiClient = apiClient;
        _orchestrator = orchestrator;
    }

    [RelayCommand]
    private async Task LoadTemplates()
    {
        try
        {
            var templates = await _apiClient.GetTemplatesAsync();
            Templates.Clear();
            foreach (var t in templates)
                Templates.Add(t);
            if (Templates.Count > 0)
                SelectedTemplate = Templates[0];

            var channels = await _apiClient.GetChannelsAsync();
            AvailableChannels.Clear();
            foreach (var c in channels)
                AvailableChannels.Add(c);
            SelectedChannelId = AvailableChannels.FirstOrDefault()?.Id;
        }
        catch (Exception ex)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Failed to load data: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
    }

    [RelayCommand]
    private void BrowseAudio()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "Audio Files|*.mp3;*.wav;*.flac;*.ogg;*.m4a|All Files|*.*",
            Title = "Select Audio File"
        };

        if (dialog.ShowDialog() == true)
        {
            AudioFilePath = dialog.FileName;
            if (string.IsNullOrEmpty(Title))
            {
                Title = System.IO.Path.GetFileNameWithoutExtension(dialog.FileName);
            }
        }
    }

    [RelayCommand]
    private async Task CreateProject()
    {
        if (string.IsNullOrWhiteSpace(Title))
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Please enter a project title", "Warning", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(AudioFilePath) || !System.IO.File.Exists(AudioFilePath))
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Please select a valid audio file", "Warning", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);
            return;
        }

        if (SelectedTemplate == null)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Please select a template", "Warning", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);
            return;
        }

        IsCreating = true;
        StatusText = "Creating project and starting pipeline...";

        try
        {
            var transitionValue = SelectedTransition == DefaultTransition ? "" : SelectedTransition;

            var platformList = new List<string>();
            if (IsYouTubeSelected) platformList.Add("YouTube Shorts");
            if (IsTikTokSelected) platformList.Add("TikTok");
            if (IsInstagramSelected) platformList.Add("Instagram Reels");
            var platforms = string.Join(", ", platformList);

            var visualEffectValue = SelectedVisualEffect?.Value;

            var result = await _apiClient.CreateProjectAsync(
                Title, SelectedTemplate.Id, SelectedFormat, AudioFilePath, ExtractAutoShorts, transitionValue, PrivacyStatus,
                ImageModel, UniqueImageCount, SelectedImageDuration?.Value, ImageStyle, CustomInstructions, platforms, SelectedChannelId, visualEffectValue);

            if (result != null)
            {
                StatusText = "Project created successfully! Pipeline started.";
                System.Windows.MessageBox.Show(
                    $"Project '{Title}' created successfully!\n\nModel: {ImageModel} | Unique Images: {UniqueImageCount}\nThe processing pipeline has been started.\nYou can monitor progress in the Dashboard.",
                    "Success");
                ProjectCreated?.Invoke();

                // Start local processing pipeline without awaiting (fire and forget)
                _ = Task.Run(async () =>
                {
                    await _orchestrator.StartPipelineAsync(result.Id, System.Threading.CancellationToken.None);
                });
            }
            else
            {
                StatusText = "Failed to create project";
                EchoForge.WPF.Views.EchoMessageBox.Show("Failed to create project. Check API connection.", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
            }
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
            EchoForge.WPF.Views.EchoMessageBox.Show($"Error creating project: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
        finally
        {
            IsCreating = false;
        }
    }
}

public class VisualEffectOption
{
    public string? Value { get; }
    public string Label { get; }

    public VisualEffectOption(string? value, string label)
    {
        Value = value;
        Label = label;
    }
}

/// <summary>Represents a Pollinations AI model option for the UI</summary>
public class ImageModelOption
{
    public string Value { get; }
    public string Label { get; }
    public string Description { get; }

    public ImageModelOption(string value, string label, string description)
    {
        Value = value;
        Label = label;
        Description = description;
    }

    public override string ToString() => $"{Label} — {Description}";
}

/// <summary>Represents manual image duration options</summary>
public class ImageDurationOption
{
    public double? Value { get; }
    public string Label { get; }

    public ImageDurationOption(double? value, string label)
    {
        Value = value;
        Label = label;
    }

    public override string ToString() => Label;
}
