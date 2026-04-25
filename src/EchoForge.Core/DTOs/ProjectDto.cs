using EchoForge.Core.Models;

namespace EchoForge.Core.DTOs;

public class ProjectDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string AudioPath { get; set; } = string.Empty;
    public double AudioFadeInDuration { get; set; } = 0;
    public double AudioFadeOutDuration { get; set; } = 0;
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
    public DateTime? ScheduledPublishAt { get; set; }
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

    private string _prompt = string.Empty;
    public string Prompt
    {
        get => _prompt;
        set { if (_prompt != value) { _prompt = value; OnPropertyChanged(); } }
    }

    private string _imagePath = string.Empty;
    public string ImagePath
    {
        get => _imagePath;
        set { if (_imagePath != value) { _imagePath = value; OnPropertyChanged(); } }
    }
    private string _transition = string.Empty;
    public string Transition
    {
        get => _transition;
        set { if (_transition != value) { _transition = value; OnPropertyChanged(); } }
    }
    
    private double? _transitionDuration;
    public double? TransitionDuration
    {
        get => _transitionDuration;
        set { if (Nullable.Compare(_transitionDuration, value) != 0) { _transitionDuration = value; OnPropertyChanged(); } }
    }

    private string? _transitionDirection;
    public string? TransitionDirection
    {
        get => _transitionDirection;
        set { if (_transitionDirection != value) { _transitionDirection = value; OnPropertyChanged(); } }
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

    private bool _isReversed = false;
    public bool IsReversed
    {
        get => _isReversed;
        set { if (_isReversed != value) { _isReversed = value; OnPropertyChanged(); } }
    }

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

    private string _textStyle = "";
    public string TextStyle
    {
        get => _textStyle;
        set { if (_textStyle != value) { _textStyle = value; OnPropertyChanged(); } }
    }

    // ═══ Multiple Text Overlays Collection ═══
    public System.Collections.ObjectModel.ObservableCollection<TextOverlayDto> TextOverlays { get; set; } = new();

    // ═══ Text Overlays ═══
    private string _overlayText = string.Empty;
    public string OverlayText
    {
        get => _overlayText;
        set { if (_overlayText != value) { _overlayText = value; OnPropertyChanged(); } }
    }
    
    private string _textFont = "Inter";
    public string TextFont
    {
        get => _textFont;
        set { if (_textFont != value) { _textFont = value; OnPropertyChanged(); } }
    }

    private double _textSize = 48;
    public double TextSize
    {
        get => _textSize;
        set { if (Math.Abs(_textSize - value) > 0.1) { _textSize = value; OnPropertyChanged(); } }
    }

    private string _textColor = "#FFFFFF";
    public string TextColor
    {
        get => _textColor;
        set { if (_textColor != value) { _textColor = value; OnPropertyChanged(); } }
    }

    private string _textAlignment = "Center";
    public string TextAlignment
    {
        get => _textAlignment;
        set { if (_textAlignment != value) { _textAlignment = value; OnPropertyChanged(); } }
    }

    private double _textPositionX = 0.5;
    public double TextPositionX
    {
        get => _textPositionX;
        set { if (Math.Abs(_textPositionX - value) > 0.01) { _textPositionX = value; OnPropertyChanged(); } }
    }

    private double _textPositionY = 0.5;
    public double TextPositionY
    {
        get => _textPositionY;
        set { if (Math.Abs(_textPositionY - value) > 0.01) { _textPositionY = value; OnPropertyChanged(); } }
    }

    private string _textAnimation = "Yok";
    public string TextAnimation
    {
        get => _textAnimation;
        set { if (_textAnimation != value) { _textAnimation = value; OnPropertyChanged(); } }
    }

    private double _textOutlineThickness = 0;
    public double TextOutlineThickness
    {
        get => _textOutlineThickness;
        set { if (Math.Abs(_textOutlineThickness - value) > 0.1) { _textOutlineThickness = value; OnPropertyChanged(); } }
    }

    private double _textShadowOpacity = 0;
    public double TextShadowOpacity
    {
        get => _textShadowOpacity;
        set { if (Math.Abs(_textShadowOpacity - value) > 0.05) { _textShadowOpacity = value; OnPropertyChanged(); } }
    }

    private double _textTransparency = 100;
    public double TextTransparency
    {
        get => _textTransparency;
        set { if (Math.Abs(_textTransparency - value) > 0.1) { _textTransparency = value; OnPropertyChanged(); } }
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

    public TimelineItemDto Clone()
    {
        var clone = new TimelineItemDto
        {
            SceneNumber = this.SceneNumber,
            Duration = this.Duration,
            Prompt = this.Prompt,
            ImagePath = this.ImagePath,
            Transition = this.Transition,
            TransitionDuration = this.TransitionDuration,
            TransitionDirection = this.TransitionDirection,
            FadeInDuration = this.FadeInDuration,
            FadeOutDuration = this.FadeOutDuration,
            Speed = this.Speed,
            Filter = this.Filter,
            TextStyle = this.TextStyle,
            OverlayText = this.OverlayText,
            TextFont = this.TextFont,
            TextSize = this.TextSize,
            TextColor = this.TextColor,
            TextAlignment = this.TextAlignment,
            TextPositionX = this.TextPositionX,
            TextPositionY = this.TextPositionY,
            TextAnimation = this.TextAnimation,
            TextOutlineThickness = this.TextOutlineThickness,
            TextShadowOpacity = this.TextShadowOpacity,
            TextTransparency = this.TextTransparency,
            Brightness = this.Brightness,
            Contrast = this.Contrast,
            Saturation = this.Saturation,
            Temperature = this.Temperature,
            Tint = this.Tint
        };
        foreach (var overlay in this.TextOverlays)
            clone.TextOverlays.Add(overlay.Clone());
        return clone;
    }
}
