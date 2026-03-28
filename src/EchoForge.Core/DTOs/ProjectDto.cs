using EchoForge.Core.Models;

namespace EchoForge.Core.DTOs;

public class ProjectDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public int TemplateId { get; set; }
    public string? TemplateName { get; set; }
    public double? BPM { get; set; }
    public double? Duration { get; set; }
    public int? SceneCount { get; set; }
    public double? SceneDuration { get; set; }
    public FormatType FormatType { get; set; }
    public bool ExtractAutoShorts { get; set; }
    public string ImageModel { get; set; } = "flux";
    public int UniqueImageCount { get; set; } = 8;
    public string ImageStyle { get; set; } = string.Empty;
    public double? ManualImageDurationSec { get; set; }
    public string? TransitionStyle { get; set; }
    public string? VisualEffect { get; set; }
    public ProjectStatus Status { get; set; }
    public int? PipelineProgress { get; set; }
    public int? UserId { get; set; }
    public string PrivacyStatus { get; set; } = "private";
    public string? OutputVideoPath { get; set; }
    public string? YouTubeVideoId { get; set; }
    public string? CustomInstructions { get; set; }
    public string? TargetPlatforms { get; set; }
    public int? TargetChannelId { get; set; }
    public string? SeoTitle { get; set; }
    public string? SeoDescription { get; set; }
    public string? SeoTags { get; set; }
    public string? SeoHashtags { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? TimelineJson { get; set; }
    
    // Virtual property for UI usage
    public System.Collections.ObjectModel.ObservableCollection<TimelineItemDto> Scenes { get; set; } = new();
}

public class TimelineItemDto : System.ComponentModel.INotifyPropertyChanged
{
    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string? name = null)
        => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

    public int SceneNumber { get; set; }

    private double _duration;
    public double Duration
    {
        get => _duration;
        set { if (Math.Abs(_duration - value) > 0.001) { _duration = value; OnPropertyChanged(); OnPropertyChanged(nameof(DurationStr)); } }
    }
    public string DurationStr => $"{Duration:F2}s";

    public string Prompt { get; set; } = string.Empty;
    public string ImagePath { get; set; } = string.Empty;
    private string _transition = string.Empty;
    public string Transition
    {
        get => _transition;
        set { if (_transition != value) { _transition = value; OnPropertyChanged(); } }
    }
    
    // Visual effects per scene
    private double _fadeInDuration = 0;
    public double FadeInDuration
    {
        get => _fadeInDuration;
        set { if (Math.Abs(_fadeInDuration - value) > 0.001) { _fadeInDuration = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFadeIn)); } }
    }

    private double _fadeOutDuration = 0;
    public double FadeOutDuration
    {
        get => _fadeOutDuration;
        set { if (Math.Abs(_fadeOutDuration - value) > 0.001) { _fadeOutDuration = value; OnPropertyChanged(); OnPropertyChanged(nameof(HasFadeOut)); } }
    }

    // Helper booleans for UI triggers
    public bool HasFadeIn => FadeInDuration > 0.05;
    public bool HasFadeOut => FadeOutDuration > 0.05;

    private double _speed = 1.0;
    public double Speed
    {
        get => _speed;
        set { if (Math.Abs(_speed - value) > 0.001) { _speed = value; OnPropertyChanged(); } }
    }
    
    private string _filter = "none";
    public string Filter
    {
        get => _filter;
        set { if (_filter != value) { _filter = value; OnPropertyChanged(); } }
    }

    // ═══ Color Adjustments (per scene) ═══
    private double _brightness = 0;
    public double Brightness
    {
        get => _brightness;
        set { if (Math.Abs(_brightness - value) > 0.001) { _brightness = value; OnPropertyChanged(); } }
    }

    private double _contrast = 1.0;
    public double Contrast
    {
        get => _contrast;
        set { if (Math.Abs(_contrast - value) > 0.001) { _contrast = value; OnPropertyChanged(); } }
    }

    private double _saturation = 1.0;
    public double Saturation
    {
        get => _saturation;
        set { if (Math.Abs(_saturation - value) > 0.001) { _saturation = value; OnPropertyChanged(); } }
    }

    private double _temperature = 6500;
    public double Temperature
    {
        get => _temperature;
        set { if (Math.Abs(_temperature - value) > 1) { _temperature = value; OnPropertyChanged(); } }
    }

    private double _tint = 0;
    public double Tint
    {
        get => _tint;
        set { if (Math.Abs(_tint - value) > 0.001) { _tint = value; OnPropertyChanged(); } }
    }

    // UI-only property, not serialized to JSON
    private bool _isSelected;
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsSelected
    {
        get => _isSelected;
        set { if (_isSelected != value) { _isSelected = value; OnPropertyChanged(); } }
    }
}
