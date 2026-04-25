using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.DTOs;
using Microsoft.Win32;

namespace EchoForge.WPF.ViewModels;

public partial class StandaloneEditorViewModel : ObservableObject
{
    // ═══════════════════════════════════
    // Core State
    // ═══════════════════════════════════
    private readonly Services.ApiClient? _apiClient;
    private readonly Services.ClientJobOrchestrator? _orchestrator;
    private readonly ProjectDto? _project;
    private readonly Action? _goBackAction;

    [ObservableProperty] private string _projectTitle = "Untitled Video";
    [ObservableProperty] private ObservableCollection<TimelineItemDto> _scenes = new();
    [ObservableProperty] private TimelineItemDto? _selectedScene;
    [ObservableProperty] private TextOverlayDto? _selectedTextOverlay;
    [ObservableProperty] private TimelineItemDto? _activeScene;
    [ObservableProperty] private string _previewImagePath = "";
    [ObservableProperty] private double _previewOpacity = 1.0;
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private string _statusMessage = "Ready — Import a video to begin editing.";
    [ObservableProperty] private bool _hasVideo;
    [ObservableProperty] private string _outputVideoPath = "";

    // YouTube Channel Selection
    [ObservableProperty] private ObservableCollection<EchoForge.Core.Entities.YouTubeChannel> _youTubeChannels = new();
    [ObservableProperty] private EchoForge.Core.Entities.YouTubeChannel? _selectedYouTubeChannel;

    // ═══════════════════════════════════
    // Video / Media
    // ═══════════════════════════════════
    [ObservableProperty] private string _videoFilePath = "";
    [ObservableProperty] private Uri? _videoSource;

    // ═══════════════════════════════════
    // Playback System
    // ═══════════════════════════════════
    [ObservableProperty] private bool _isPlaying;
    [ObservableProperty] private string _currentPlaybackTimeDisplay = "0:00 / 0:00";
    [ObservableProperty] private double _playheadPixelPosition = 0;
    [ObservableProperty] private double _zoomScale = 1.0;
    [ObservableProperty] private string _totalDurationDisplay = "";
    [ObservableProperty] private string _selectedSceneDurationDisplay = "";

    private System.Windows.Threading.DispatcherTimer _playbackTimer;
    private double _currentPlaybackTime = 0;
    private double _totalPlaybackTime = 0;
    private double _pixelsPerSecond = 1;
    private double _scrollOffset = 0;

    public double TotalPlaybackTime => _totalPlaybackTime;
    public double CurrentPlaybackTimeRaw => _currentPlaybackTime;

    // Audio events (consumed by code-behind for NAudio)
    public event EventHandler<string>? AudioPlaybackChanged;
    public event EventHandler<double>? AudioSeeked;
    public event EventHandler? RequestOpenFileDialog;

    // Callbacks for MediaElement
    public Action? RequestPlay { get; set; }
    public Action? RequestPause { get; set; }
    public Action<double>? RequestSeek { get; set; }
    public event EventHandler<double>? SpeedChanged;

    // ═══════════════════════════════════
    // Audio Properties
    // ═══════════════════════════════════
    [ObservableProperty] private double _audioVolume = 0.5;
    [ObservableProperty] private double _audioDuration = 60.0;
    [ObservableProperty] private double _pitch = 0;
    [ObservableProperty] private string _audioEqualizerPreset = "Normal";
    [ObservableProperty] private bool _isNoiseReductionEnabled = false;

    public string AudioFilePath => _project?.AudioPath ?? string.Empty;

    public double ProjectFadeIn
    {
        get => _project?.AudioFadeInDuration ?? 0;
        set { if (_project != null) { _project.AudioFadeInDuration = value; OnPropertyChanged(); SaveHistoryState(); } }
    }

    public double ProjectFadeOut
    {
        get => _project?.AudioFadeOutDuration ?? 0;
        set { if (_project != null) { _project.AudioFadeOutDuration = value; OnPropertyChanged(); SaveHistoryState(); } }
    }

    // ═══════════════════════════════════
    // Ribbon & Asset Library
    // ═══════════════════════════════════
    [ObservableProperty] private bool _isAssetLibraryOpen;
    [ObservableProperty] private string _assetLibraryMode = "Media";

    private ObservableCollection<ImportedMediaItem> _allImportedMedia = new();
    [ObservableProperty] private ObservableCollection<ImportedMediaItem> _importedMedia = new();
    [ObservableProperty] private string _mediaFilter = "All";

    partial void OnMediaFilterChanged(string value) => FilterMediaCollection();

    // ═══════════════════════════════════
    // Properties Panel
    // ═══════════════════════════════════
    [ObservableProperty] private int _selectedPropertiesTab;

    // Speed
    [ObservableProperty] private double _speed = 1.0;
    [ObservableProperty] private bool _isLoopEnabled;

    partial void OnSpeedChanged(double value)
    {
        // Push speed to the selected scene
        if (SelectedScene != null)
        {
            SelectedScene.Speed = value;
            SaveHistoryState();
        }
        SpeedChanged?.Invoke(this, value);
    }

    // Fade
    [ObservableProperty] private double _fadeInDuration;
    [ObservableProperty] private double _fadeOutDuration;

    // Effects & Filters
    [ObservableProperty] private string _selectedEffect = "none";
    [ObservableProperty] private double _effectIntensity = 50;
    [ObservableProperty] private string _selectedFilter = "none";
    [ObservableProperty] private double _filterIntensity = 50;

    // Color adjustments
    [ObservableProperty] private double _exposure;
    [ObservableProperty] private double _contrast;
    [ObservableProperty] private double _saturation;
    [ObservableProperty] private double _temperature;

    // Text
    [ObservableProperty] private string _overlayText = string.Empty;
    [ObservableProperty] private string _textFont = "Inter";
    [ObservableProperty] private double _textSize = 48;
    [ObservableProperty] private string _textColor = "#FFFFFF";
    [ObservableProperty] private string _textAlignment = "Center";
    [ObservableProperty] private double _textLineHeight = 1.0;
    [ObservableProperty] private double _textLetterSpacing = 0;
    [ObservableProperty] private string _textOutlineColor = "#000000";
    [ObservableProperty] private double _textOutlineThickness = 0;
    [ObservableProperty] private double _textShadowOpacity = 0.5;
    [ObservableProperty] private double _textTransparency = 100;
    [ObservableProperty] private double _textPositionX = 0.5;
    [ObservableProperty] private double _textPositionY = 0.5;
    [ObservableProperty] private string _textAnimation = "none";

    // Playback modifiers
    [ObservableProperty] private bool _isReversed = false;

    partial void OnOverlayTextChanged(string value) { if (SelectedScene != null) { SelectedScene.OverlayText = value; SaveHistoryState(); } }
    partial void OnTextFontChanged(string value) { if (SelectedScene != null) { SelectedScene.TextFont = value; SaveHistoryState(); } }
    partial void OnTextSizeChanged(double value) { if (SelectedScene != null) { SelectedScene.TextSize = value; SaveHistoryState(); } }
    partial void OnTextColorChanged(string value) { if (SelectedScene != null) { SelectedScene.TextColor = value; SaveHistoryState(); } }
    partial void OnTextAlignmentChanged(string value) { if (SelectedScene != null) { SelectedScene.TextAlignment = value; SaveHistoryState(); } }
    partial void OnTextOutlineThicknessChanged(double value) { if (SelectedScene != null) { SelectedScene.TextOutlineThickness = value; SaveHistoryState(); } }
    partial void OnTextShadowOpacityChanged(double value) { if (SelectedScene != null) { SelectedScene.TextShadowOpacity = value; SaveHistoryState(); } }
    partial void OnTextTransparencyChanged(double value) { if (SelectedScene != null) { SelectedScene.TextTransparency = value; SaveHistoryState(); } }
    partial void OnTextPositionXChanged(double value) { if (SelectedScene != null) { SelectedScene.TextPositionX = value; SaveHistoryState(); } }
    partial void OnTextPositionYChanged(double value) { if (SelectedScene != null) { SelectedScene.TextPositionY = value; SaveHistoryState(); } }
    partial void OnTextAnimationChanged(string value) { if (SelectedScene != null) { SelectedScene.TextAnimation = value; SaveHistoryState(); } }
    partial void OnIsReversedChanged(bool value) { if (SelectedScene != null) { SelectedScene.IsReversed = value; SaveHistoryState(); } }

    partial void OnSelectedSceneChanged(TimelineItemDto? value)
    {
        if (value != null)
        {
            // Sync Text UI
            _overlayText = value.OverlayText ?? "";
            OnPropertyChanged(nameof(OverlayText));
            _textFont = value.TextFont ?? "Inter";
            OnPropertyChanged(nameof(TextFont));
            _textSize = value.TextSize > 0 ? value.TextSize : 48;
            OnPropertyChanged(nameof(TextSize));
            _textColor = value.TextColor ?? "#FFFFFF";
            OnPropertyChanged(nameof(TextColor));
            _textAlignment = value.TextAlignment ?? "Center";
            OnPropertyChanged(nameof(TextAlignment));
            _textOutlineThickness = value.TextOutlineThickness;
            OnPropertyChanged(nameof(TextOutlineThickness));
            _textShadowOpacity = value.TextShadowOpacity;
            OnPropertyChanged(nameof(TextShadowOpacity));
            _textTransparency = value.TextTransparency;
            OnPropertyChanged(nameof(TextTransparency));
            _textPositionX = value.TextPositionX;
            OnPropertyChanged(nameof(TextPositionX));
            _textPositionY = value.TextPositionY;
            OnPropertyChanged(nameof(TextPositionY));
            _textAnimation = value.TextAnimation ?? "none";
            OnPropertyChanged(nameof(TextAnimation));
            
            _speed = value.Speed;
            OnPropertyChanged(nameof(Speed));
            _isReversed = value.IsReversed;
            OnPropertyChanged(nameof(IsReversed));

            // Sync Transition UI
            _transitionDirection = string.IsNullOrEmpty(value.TransitionDirection) ? "Left" : value.TransitionDirection;
            OnPropertyChanged(nameof(TransitionDirection));
            _transitionDuration = value.TransitionDuration ?? 1.0;
            OnPropertyChanged(nameof(TransitionDuration));
        }
    }

    // Transitions
    [ObservableProperty] private double _transitionDuration = 1.0;
    [ObservableProperty] private string _transitionDirection = "Left to Right";

    // Layout
    [ObservableProperty] private string _aspectRatio = "16:9";

    // Range marking
    [ObservableProperty] private double _rangeInPoint = -1;
    [ObservableProperty] private double _rangeOutPoint = -1;
    [ObservableProperty] private string _rangeDisplay = "No range set";
    [ObservableProperty] private bool _hasRange;

    // AI Image Models
    [ObservableProperty] private string _imageUrlInput = "";
    [ObservableProperty] private int _projectWidth = 1920;
    [ObservableProperty] private int _projectHeight = 1080;

    public class SimpleImageModelOption
    {
        public string Value { get; set; } = "";
        public string Label { get; set; } = "";
        public override string ToString() => Label;
    }

    // ═══════════════════════════════════════════════════
    // TRANSITION PROPERTIES (TAB 3)
    // ═══════════════════════════════════════════════════

    partial void OnTransitionDirectionChanged(string value)
    {
        if (SelectedScene != null)
        {
            SelectedScene.TransitionDirection = value.ToLowerInvariant();
            SaveHistoryState();
        }
    }

    partial void OnTransitionDurationChanged(double value)
    {
        if (SelectedScene != null)
        {
            SelectedScene.TransitionDuration = value;
            SaveHistoryState();
        }
    }

    [ObservableProperty]
    private List<SimpleImageModelOption> _editorRegenerateImageModels = new()
    {
        new() { Value = "comfyui:local", Label = "🖥️ Local (ComfyUI)" },
        new() { Value = "pollinations:flux", Label = "Pollinations - Flux" },
        new() { Value = "pollinations:turbo", Label = "Pollinations - SD Turbo" },
        new() { Value = "pollinations:flux-realism", Label = "Pollinations - Realism" },
        new() { Value = "gemini:gemini-2.5-flash", Label = "Gemini 2.5 Flash" },
        new() { Value = "huggingface:flux", Label = "HuggingFace - Flux" }
    };

    [ObservableProperty] private SimpleImageModelOption? _selectedEditorImageModel;

    // ═══════════════════════════════════════════════════
    // CONSTRUCTORS
    // ═══════════════════════════════════════════════════
    public StandaloneEditorViewModel()
    {
        _playbackTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _playbackTimer.Tick += PlaybackTimer_Tick;
    }

    public StandaloneEditorViewModel(ProjectDto project, Services.ApiClient apiClient, Services.ClientJobOrchestrator orchestrator, Action goBackAction)
    {
        _apiClient = apiClient;
        _orchestrator = orchestrator;
        _project = project;
        _goBackAction = goBackAction;
        _projectTitle = project.Title ?? "Untitled Project";
        _outputVideoPath = project.OutputVideoPath ?? "";

        _playbackTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        // Load scenes
        foreach (var scene in project.Scenes)
            Scenes.Add(scene);

        UpdateTotalDuration();

        if (Scenes.Count > 0)
        {
            SelectedScene = Scenes[0];
            ActiveScene = Scenes[0];
            if (!string.IsNullOrEmpty(Scenes[0].ImagePath))
                PreviewImagePath = Scenes[0].ImagePath;
        }

        HasVideo = true;
        StatusMessage = $"Loaded project: {_projectTitle}";

        // History
        SaveHistoryState();

        // Image model
        var mappedModel = string.IsNullOrWhiteSpace(_project.ImageModel) ? "comfyui:local" : _project.ImageModel;
        SelectedEditorImageModel = EditorRegenerateImageModels.FirstOrDefault(x => x.Value == mappedModel)
            ?? EditorRegenerateImageModels.First(x => x.Value == "comfyui:local");

        // Load YouTube Channels
        _ = LoadYouTubeChannelsAsync();
    }

    private async Task LoadYouTubeChannelsAsync()
    {
        if (_apiClient == null) return;
        try
        {
            var channels = await _apiClient.GetYouTubeChannelsAsync();
            App.Current.Dispatcher.Invoke(() =>
            {
                YouTubeChannels.Clear();
                foreach (var c in channels) YouTubeChannels.Add(c);
                SelectedYouTubeChannel = YouTubeChannels.FirstOrDefault(c => c.Id == _project?.TargetChannelId) ?? YouTubeChannels.FirstOrDefault();
            });
        }
        catch { /* ignore network error */ }
    }

    // ═══════════════════════════════════════════════════
    // PLAYBACK ENGINE
    // ═══════════════════════════════════════════════════
    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        _currentPlaybackTime += 0.05 * Speed;
        if (_currentPlaybackTime >= _totalPlaybackTime)
        {
            if (IsLoopEnabled)
            {
                _currentPlaybackTime = 0;
                AudioSeeked?.Invoke(this, 0);
            }
            else
            {
                _currentPlaybackTime = _totalPlaybackTime;
                IsPlaying = false;
                _playbackTimer.Stop();
                AudioPlaybackChanged?.Invoke(this, "pause");
            }
        }

        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (IsPlaying)
        {
            IsPlaying = false;
            _playbackTimer.Stop();
            AudioPlaybackChanged?.Invoke(this, "pause");
            RequestPause?.Invoke();
        }
        else
        {
            if (_currentPlaybackTime >= _totalPlaybackTime)
                _currentPlaybackTime = 0;
            IsPlaying = true;
            _playbackTimer.Start();
            AudioPlaybackChanged?.Invoke(this, "play");
            RequestPlay?.Invoke();
        }
    }

    [RelayCommand]
    private void StepForward()
    {
        if (IsPlaying) return;
        _currentPlaybackTime = Math.Min(_currentPlaybackTime + (1.0 / 30.0), _totalPlaybackTime); // ~1 frame at 30fps
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
        AudioSeeked?.Invoke(this, _currentPlaybackTime);
    }

    [RelayCommand]
    private void StepBackward()
    {
        if (IsPlaying) return;
        _currentPlaybackTime = Math.Max(_currentPlaybackTime - (1.0 / 30.0), 0);
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
        AudioSeeked?.Invoke(this, _currentPlaybackTime);
    }

    [RelayCommand]
    private void SeekToStart()
    {
        _currentPlaybackTime = 0;
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
        AudioSeeked?.Invoke(this, _currentPlaybackTime);
    }

    [RelayCommand]
    private void SeekToEnd()
    {
        _currentPlaybackTime = _totalPlaybackTime;
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
        AudioSeeked?.Invoke(this, _currentPlaybackTime);
    }

    public void SeekToTime(double timeInSeconds)
    {
        if (timeInSeconds < 0) timeInSeconds = 0;
        if (timeInSeconds > _totalPlaybackTime) timeInSeconds = _totalPlaybackTime;
        _currentPlaybackTime = timeInSeconds;
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
        AudioSeeked?.Invoke(this, _currentPlaybackTime);
    }

    public void SeekToPixelPosition(double pixelX)
    {
        if (_pixelsPerSecond <= 0) return;
        double sec = pixelX / _pixelsPerSecond;
        if (sec < 0) sec = 0;
        if (sec > _totalPlaybackTime) sec = _totalPlaybackTime;
        _currentPlaybackTime = sec;
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
        AudioSeeked?.Invoke(this, _currentPlaybackTime);
    }

    public void UpdateTotalDuration()
    {
        double oldTotal = _totalPlaybackTime;
        _totalPlaybackTime = Scenes.Sum(s => s.Duration);
        OnPropertyChanged(nameof(TotalPlaybackTime));

        if (Math.Abs(AudioDuration - oldTotal) < 0.1 || Math.Abs(AudioDuration - 60.0) < 0.1)
            AudioDuration = _totalPlaybackTime > 0 ? _totalPlaybackTime : 60.0;

        int mins = (int)(_totalPlaybackTime / 60);
        int secs = (int)(_totalPlaybackTime % 60);
        TotalDurationDisplay = $"Duration: {mins}:{secs:D2}";
        UpdateTimeDisplay();
    }

    private void UpdateTimeDisplay()
    {
        int cmins = (int)(_currentPlaybackTime / 60);
        int csecs = (int)(_currentPlaybackTime % 60);
        int tmins = (int)(_totalPlaybackTime / 60);
        int tsecs = (int)(_totalPlaybackTime % 60);
        CurrentPlaybackTimeDisplay = $"{cmins}:{csecs:D2} / {tmins}:{tsecs:D2}";
    }

    public void UpdateTimelineLayoutMetrics(double trackWidth, double pixelsPerSecond, double scrollOffset)
    {
        _pixelsPerSecond = pixelsPerSecond;
        _scrollOffset = scrollOffset;
        UpdatePlayheadPosition();
    }

    private void UpdatePlayheadPosition()
    {
        if (_pixelsPerSecond <= 0) return;
        PlayheadPixelPosition = (_currentPlaybackTime * _pixelsPerSecond) - _scrollOffset;
    }

    private void UpdateActiveSceneBasedOnTime()
    {
        double accum = 0;
        foreach (var s in Scenes)
        {
            double sceneStart = accum;
            double sceneEnd = accum + s.Duration;

            if (_currentPlaybackTime >= sceneStart && _currentPlaybackTime <= sceneEnd)
            {
                if (ActiveScene != s) ActiveScene = s;
                if (!string.IsNullOrEmpty(s.ImagePath)) PreviewImagePath = s.ImagePath;

                double timeInScene = _currentPlaybackTime - sceneStart;
                double opacity = 1.0;
                if (s.FadeInDuration > 0 && timeInScene < s.FadeInDuration)
                    opacity = timeInScene / s.FadeInDuration;
                else if (s.FadeOutDuration > 0 && timeInScene > s.Duration - s.FadeOutDuration)
                {
                    double timeInFadeOut = timeInScene - (s.Duration - s.FadeOutDuration);
                    opacity = 1.0 - (timeInFadeOut / s.FadeOutDuration);
                }
                PreviewOpacity = Math.Clamp(opacity, 0.0, 1.0);
                break;
            }
            accum += s.Duration;
        }
    }

    // ═══════════════════════════════════════════════════
    // SCENE MANAGEMENT
    // ═══════════════════════════════════════════════════
    public void ReorderScene(TimelineItemDto sourceScene, TimelineItemDto targetScene)
    {
        if (sourceScene == null || targetScene == null || sourceScene == targetScene) return;

        int oldIndex = Scenes.IndexOf(sourceScene);
        int newIndex = Scenes.IndexOf(targetScene);
        
        if (oldIndex == -1 || newIndex == -1 || oldIndex == newIndex) return;

        SaveHistoryState();
        
        Scenes.Move(oldIndex, newIndex);
        
        // Re-number scenes starting from 1
        for (int i = 0; i < Scenes.Count; i++)
        {
            Scenes[i].SceneNumber = i + 1;
        }
        
        UpdateTotalDuration();
        UpdateActiveSceneBasedOnTime();
    }

    partial void OnSelectedSceneChanged(TimelineItemDto? oldValue, TimelineItemDto? newValue)
    {
        if (oldValue != null) oldValue.IsSelected = false;
        if (newValue != null && !string.IsNullOrEmpty(newValue.ImagePath))
        {
            newValue.IsSelected = true;
            PreviewImagePath = newValue.ImagePath;

            // Sync properties from selected scene
            Speed = newValue.Speed > 0 ? newValue.Speed : 1.0;
            FadeInDuration = newValue.FadeInDuration;
            FadeOutDuration = newValue.FadeOutDuration;

            double startTime = 0;
            foreach (var s in Scenes)
            {
                if (s.SceneNumber == newValue.SceneNumber) break;
                startTime += s.Duration;
            }
            double endTime = startTime + newValue.Duration;
            SelectedSceneDurationDisplay = $"Starts: {FormatTime(startTime)} — Ends: {FormatTime(endTime)}";
        }
        else
        {
            PreviewImagePath = "";
            SelectedSceneDurationDisplay = "";
        }
    }

    [RelayCommand]
    private void SelectScene(TimelineItemDto? scene)
    {
        SelectedScene = scene;
        if (scene != null && !string.IsNullOrEmpty(scene.ImagePath))
            PreviewImagePath = scene.ImagePath;
    }

    [RelayCommand]
    private void DeleteScene()
    {
        if (SelectedScene == null) { StatusMessage = "⚠️ Select a scene first."; return; }
        if (Scenes.Count <= 1) { StatusMessage = "⚠️ Cannot delete the last scene."; return; }
        Scenes.Remove(SelectedScene);
        SelectedScene = null;
        RenumberScenes();
        UpdateTotalDuration();
        UpdateActiveSceneBasedOnTime();
        SaveHistoryState();
        StatusMessage = "🗑️ Scene deleted.";
    }

    [RelayCommand]
    private void DuplicateScene()
    {
        if (SelectedScene == null) return;
        var newScene = SelectedScene.Clone();
        newScene.SceneNumber = Scenes.Count > 0 ? Scenes.Max(s => s.SceneNumber) + 1 : 1;
        var index = Scenes.IndexOf(SelectedScene);
        if (index >= 0) Scenes.Insert(index + 1, newScene);
        else Scenes.Add(newScene);
        UpdateTotalDuration();
        SaveHistoryState();
        StatusMessage = "⧉ Scene duplicated.";
    }

    // ═══════════════════════════════════════════════════
    // AUDIO EDITING (DETACH & VOICEOVER)
    // ═══════════════════════════════════════════════════
    [RelayCommand]
    private async Task DetachAudioAsync()
    {
        if (_project != null && !string.IsNullOrEmpty(_project.AudioPath))
        {
            _project.AudioPath = string.Empty;
            // Force refresh of the bindings that check HasAudio
            OnPropertyChanged(nameof(ProjectFadeIn)); 
            StatusMessage = "🔇 Ses başarıyla projeden söküldü.";
            SaveHistoryState();
        }
        else
        {
            StatusMessage = "⚠️ Ayırmak için projede aktif bir ses izi bulunamadı.";
        }
        await Task.CompletedTask;
    }

    [RelayCommand]
    private async Task RecordVoiceoverAsync()
    {
        // STUB: Real implementaton will use NAudio WaveInEvent to write to path, then assign to ProjectDto.AudioPath / SecondaryAudioPath
        StatusMessage = "🎙️ Mikrofon başlatılıyor... (Kayıt modülü beklemede)";
        await Task.Delay(1500);
        StatusMessage = "🛑 Voiceover geçici olarak eklendi.";
    }

    [RelayCommand]
    private void SplitScene()
    {
        if (_pixelsPerSecond <= 0) return;
        double accum = 0;
        for (int i = 0; i < Scenes.Count; i++)
        {
            var s = Scenes[i];
            if (_currentPlaybackTime > accum && _currentPlaybackTime < accum + s.Duration)
            {
                double splitPoint = _currentPlaybackTime - accum;
                if (splitPoint < 0.5 || s.Duration - splitPoint < 0.5)
                {
                    StatusMessage = "⚠️ Too close to clip edge.";
                    return;
                }
                bool wasPlaying = IsPlaying;
                if (IsPlaying) TogglePlayback();

                var firstHalf = new TimelineItemDto
                {
                    SceneNumber = s.SceneNumber, Duration = splitPoint, Prompt = s.Prompt,
                    ImagePath = s.ImagePath, Transition = "none", FadeInDuration = s.FadeInDuration,
                    FadeOutDuration = 0, Speed = s.Speed, Filter = s.Filter
                };
                var secondHalf = new TimelineItemDto
                {
                    SceneNumber = s.SceneNumber + 1, Duration = s.Duration - splitPoint, Prompt = s.Prompt,
                    ImagePath = s.ImagePath, Transition = s.Transition, FadeInDuration = 0,
                    FadeOutDuration = s.FadeOutDuration, Speed = s.Speed, Filter = s.Filter
                };

                Scenes.RemoveAt(i);
                Scenes.Insert(i, firstHalf);
                Scenes.Insert(i + 1, secondHalf);
                RenumberScenes();
                UpdateTotalDuration();
                SaveHistoryState();
                SeekToTime(_currentPlaybackTime);
                StatusMessage = "✂️ Scene split!";
                return;
            }
            accum += s.Duration;
        }
    }

    private void RenumberScenes()
    {
        for (int i = 0; i < Scenes.Count; i++)
            Scenes[i].SceneNumber = i + 1;
    }

    private string FormatTime(double seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return $"{m}:{s:D2}";
    }

    // ═══════════════════════════════════════════════════
    // UNDO / REDO
    // ═══════════════════════════════════════════════════
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _isRestoringHistory = false;

    public bool CanUndo => _historyIndex > 0;
    public bool CanRedo => _historyIndex < _history.Count - 1;

    public void SaveHistoryState()
    {
        if (_isRestoringHistory) return;
        if (_history.Count > 0 && _historyIndex < _history.Count - 1)
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);

        var json = System.Text.Json.JsonSerializer.Serialize(Scenes.ToList());
        if (_history.Count > 0 && _historyIndex >= 0 && _history[_historyIndex] == json) return;

        _history.Add(json);
        _historyIndex++;
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo()
    {
        if (!CanUndo) return;
        _historyIndex--;
        RestoreHistoryState();
    }

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo()
    {
        if (!CanRedo) return;
        _historyIndex++;
        RestoreHistoryState();
    }

    private void RestoreHistoryState()
    {
        _isRestoringHistory = true;
        try
        {
            var json = _history[_historyIndex];
            var items = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(json);
            if (items != null)
            {
                Scenes.Clear();
                foreach (var item in items) Scenes.Add(item);
                UpdateTotalDuration();
                UpdateActiveSceneBasedOnTime();
                if (SelectedScene != null)
                    SelectedScene = Scenes.FirstOrDefault(s => s.SceneNumber == SelectedScene.SceneNumber);
            }
        }
        finally
        {
            _isRestoringHistory = false;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void CommitEdit() => SaveHistoryState();

    // ═══════════════════════════════════════════════════
    // RENDER & API
    // ═══════════════════════════════════════════════════
    [RelayCommand]
    private async Task RenderVideo()
    {
        if (_project == null || _apiClient == null || _orchestrator == null)
        {
            StatusMessage = "No project loaded.";
            return;
        }

        var result = Views.EchoMessageBox.Show("Finalize all edits and start rendering?", "Confirm Render", Views.EchoMessageBox.EchoMessageType.Question);
        if (result != System.Windows.MessageBoxResult.OK) return;

        IsLoading = true;
        StatusMessage = "💾 Saving scenes...";
        try
        {
            var saveSuccess = await _apiClient.UpdateProjectScenesAsync(_project.Id, Scenes.ToList());
            if (!saveSuccess) { StatusMessage = "❌ Save failed."; IsLoading = false; return; }

            StatusMessage = "🎬 Starting render...";
            _ = Task.Run(async () => await _orchestrator.ResumePipelineAsync(_project.Id, CancellationToken.None));

            StatusMessage = "🎬 Render started!";
            Views.EchoMessageBox.Show("Rendering started in the background!", "Rendering", Views.EchoMessageBox.EchoMessageType.Success);
            _goBackAction?.Invoke();
        }
        catch (Exception ex) { StatusMessage = $"❌ Error: {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task ApplySceneEdits()
    {
        if (_project == null || _apiClient == null) return;
        IsLoading = true;
        StatusMessage = "Saving scene changes...";
        try
        {
            var success = await _apiClient.UpdateProjectScenesAsync(_project.Id, Scenes.ToList());
            StatusMessage = success ? "✅ Saved!" : "❌ Save failed.";
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsLoading = false; }
    }

    [RelayCommand]
    private async Task RegenerateSceneAsync()
    {
        if (SelectedScene == null) { StatusMessage = "⚠️ Select a scene first."; return; }
        if (_orchestrator == null) { StatusMessage = "⚠️ No orchestrator."; return; }

        IsLoading = true;
        StatusMessage = "⏳ Regenerating scene image...";
        try
        {
            string prompt = SelectedScene.Prompt;
            if (string.IsNullOrWhiteSpace(prompt))
                prompt = $"{ProjectTitle}, cinematic lighting, high quality";

            Action<int, string> progressCallback = (percent, message) =>
                System.Windows.Application.Current.Dispatcher.Invoke(() => StatusMessage = $"⏳ {message}");

            string newImagePath = await Task.Run(() =>
                _orchestrator.GenerateSingleImageAsync(_project!.Id, prompt, SelectedEditorImageModel?.Value,
                    ProjectWidth, ProjectHeight, progressCallback, CancellationToken.None));

            if (!string.IsNullOrEmpty(newImagePath) && File.Exists(newImagePath))
            {
                SelectedScene.ImagePath = newImagePath;
                PreviewImagePath = newImagePath;
                int index = Scenes.IndexOf(SelectedScene);
                if (index >= 0) Scenes[index] = SelectedScene;
                OnPropertyChanged(nameof(SelectedScene));
                SaveHistoryState();
                StatusMessage = "✅ Image regenerated!";
            }
            else StatusMessage = "❌ Empty result.";
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
        finally { IsLoading = false; }
    }

    // ═══════════════════════════════════════════════════
    // SCENE IMAGE REPLACEMENT
    // ═══════════════════════════════════════════════════
    [RelayCommand]
    private void ReplaceSceneImageFromFile()
    {
        if (SelectedScene == null) { StatusMessage = "⚠️ Select a scene."; return; }
        RequestOpenFileDialog?.Invoke(this, EventArgs.Empty);
    }

    public void ApplyReplacementImage(string sourceFilePath)
    {
        if (SelectedScene == null || string.IsNullOrEmpty(sourceFilePath)) return;
        try
        {
            string projectDir = Path.GetDirectoryName(_project?.AudioPath ?? "") ?? "";
            string imagesDir = Path.Combine(projectDir, "images");
            if (!Directory.Exists(imagesDir)) Directory.CreateDirectory(imagesDir);

            string ext = Path.GetExtension(sourceFilePath);
            string newFileName = $"custom_scene{SelectedScene.SceneNumber}_{DateTime.Now:yyyyMMddHHmmss}{ext}";
            string destPath = Path.Combine(imagesDir, newFileName);
            File.Copy(sourceFilePath, destPath, true);

            SelectedScene.ImagePath = destPath;
            PreviewImagePath = destPath;
            int index = Scenes.IndexOf(SelectedScene);
            if (index >= 0) Scenes[index] = SelectedScene;
            OnPropertyChanged(nameof(SelectedScene));
            SaveHistoryState();
            StatusMessage = "✅ Image replaced!";
        }
        catch (Exception ex) { StatusMessage = $"❌ {ex.Message}"; }
    }

    // ═══════════════════════════════════════════════════
    // AUDIO FILE
    // ═══════════════════════════════════════════════════
    public void SetAudioFile(string path)
    {
        if (_project != null) _project.AudioPath = path;
        OnPropertyChanged(nameof(AudioFilePath));
        AudioPlaybackChanged?.Invoke(this, "pause");
        SaveHistoryState();
    }

    // ═══════════════════════════════════════════════════
    // NAVIGATION & UI COMMANDS
    // ═══════════════════════════════════════════════════
    [RelayCommand]
    private void GoBack()
    {
        IsPlaying = false;
        _playbackTimer?.Stop();
        _goBackAction?.Invoke();
    }

    [RelayCommand]
    private void SelectPropertiesTab(string index)
    {
        if (int.TryParse(index, out var tab))
            SelectedPropertiesTab = tab;
    }

    [RelayCommand]
    private void ToggleAssetLibrary(string mode)
    {
        if (IsAssetLibraryOpen && AssetLibraryMode == mode)
            IsAssetLibraryOpen = false;
        else { AssetLibraryMode = mode; IsAssetLibraryOpen = true; }
    }

    [RelayCommand]
    private void ResetEffects()
    {
        SelectedEffect = "none"; EffectIntensity = 50;
        SelectedFilter = "none"; FilterIntensity = 50;
        Exposure = 0; Contrast = 0; Saturation = 0; Temperature = 0;
        TextFont = "Inter"; TextSize = 48; TextColor = "#FFFFFF";
        TextAlignment = "Center"; TextLineHeight = 1.0; TextLetterSpacing = 0;
        TextOutlineThickness = 0; TextShadowOpacity = 0.5; TextTransparency = 100;
        Pitch = 0; AudioEqualizerPreset = "Normal"; IsNoiseReductionEnabled = false;
        StatusMessage = "All effects reset.";
    }

    // ═══════════════════════════════════════════════════
    // FILTER/EFFECT APPLY (from card clicks)
    // ═══════════════════════════════════════════════════
    [RelayCommand]
    private void ApplyFilter(string filterName)
    {
        if (SelectedScene != null)
        {
            SelectedScene.Filter = filterName;
            SelectedFilter = filterName;
            SaveHistoryState();
            StatusMessage = $"🎨 Filter applied: {filterName}";
        }
    }

    // (Deleted duplicate ApplyEffect, ApplyTransition, ApplyTextStyle, now managed via 1100+ overloads)
        // ═══════════════════════════════════════════════════
    // MEDIA IMPORT
    // ═══════════════════════════════════════════════════
    [RelayCommand]
    private void OpenVideo()
    {
        var dlg = new OpenFileDialog { Title = "Import Video", Filter = "Video Files|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm|All|*.*" };
        if (dlg.ShowDialog() == true) LoadVideo(dlg.FileName);
    }

    [RelayCommand]
    private void ImportMedia()
    {
        var dlg = new OpenFileDialog { Title = "Import Media", Filter = "Media|*.mp4;*.avi;*.mkv;*.mov;*.wmv;*.webm;*.mp3;*.wav;*.ogg;*.jpg;*.jpeg;*.png;*.gif;*.bmp|All|*.*", Multiselect = true };
        if (dlg.ShowDialog() == true) AddMediaFiles(dlg.FileNames);
    }

    [RelayCommand]
    private void HandleDroppedFiles(string[] files) => AddMediaFiles(files);

    [RelayCommand] private void RecordScreenAndCamera() => StatusMessage = "Screen recording pending.";
    [RelayCommand] private void RecordCamera() => StatusMessage = "Camera recording pending.";
    [RelayCommand] private void RecordAudio() => StatusMessage = "Audio recording pending.";
    [RelayCommand] private void TextToSpeech() => StatusMessage = "TTS pending.";

    private void AddMediaFiles(string[] files)
    {
        foreach (var file in files)
        {
            var ext = Path.GetExtension(file).ToLowerInvariant();
            var type = ext switch
            {
                ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm" => "Video",
                ".mp3" or ".wav" or ".ogg" => "Audio",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp" => "Image",
                _ => "Other"
            };
            if (!_allImportedMedia.Any(m => m.FilePath == file))
            {
                _allImportedMedia.Add(new ImportedMediaItem
                {
                    FileName = Path.GetFileName(file), FilePath = file, MediaType = type,
                    IsVideo = type == "Video", IsAudio = type == "Audio", IsImage = type == "Image"
                });
            }
        }
        FilterMediaCollection();
        StatusMessage = $"{files.Length} file(s) imported.";
        if (!HasVideo)
        {
            var firstVideo = files.FirstOrDefault(f => Path.GetExtension(f).ToLowerInvariant() is ".mp4" or ".avi" or ".mkv" or ".mov" or ".wmv" or ".webm");
            if (firstVideo != null) LoadVideo(firstVideo);
        }
    }

    private void FilterMediaCollection()
    {
        ImportedMedia.Clear();
        foreach (var item in _allImportedMedia)
            if (MediaFilter == "All" || item.MediaType == MediaFilter)
                ImportedMedia.Add(item);
    }

    private void LoadVideo(string path)
    {
        VideoFilePath = path;
        VideoSource = new Uri(path, UriKind.Absolute);
        HasVideo = true;
        StatusMessage = $"Loaded: {Path.GetFileName(path)}";

        // Create a default scene for the video if no scenes exist
        if (Scenes.Count == 0)
        {
            Scenes.Add(new TimelineItemDto
            {
                SceneNumber = 1,
                Duration = 10, // Default — will be updated by MediaOpened
                Prompt = Path.GetFileNameWithoutExtension(path),
                ImagePath = ""
            });
            SelectedScene = Scenes[0];
        }

        UpdateTotalDuration();
        UpdateTimeDisplay();
        AddMediaFiles(new[] { path });
    }

    /// <summary>Called by code-behind when MediaElement reports the actual video duration.</summary>
    public void SetVideoDuration(double durationSeconds)
    {
        if (durationSeconds <= 0) return;

        // Update the first scene's duration to match the video
        if (Scenes.Count == 1 && Scenes[0].SceneNumber == 1)
        {
            Scenes[0].Duration = durationSeconds;
        }

        _totalPlaybackTime = durationSeconds;
        OnPropertyChanged(nameof(TotalPlaybackTime));
        UpdateTimeDisplay();
        UpdatePlayheadPosition();

        int mins = (int)(durationSeconds / 60);
        int secs = (int)(durationSeconds % 60);
        TotalDurationDisplay = $"Duration: {mins}:{secs:D2}";
    }

    // ═══════════════════════════════════
    // VIDEO EXPORT / RENDER
    // ═══════════════════════════════════
    [ObservableProperty] private string _exportResolution = "1920x1080";
    [ObservableProperty] private string _exportFormat = "mp4";
    [ObservableProperty] private string _exportCodec = "H.264";
    [ObservableProperty] private int _exportFps = 30;
    [ObservableProperty] private bool _isExporting;
    [ObservableProperty] private double _exportProgress;
    [ObservableProperty] private string _exportStatusText = "";
    [ObservableProperty] private bool _isExportPanelOpen;

    [RelayCommand]
    private void ApplyExportPreset(string preset)
    {
        switch (preset)
        {
            case "youtube_shorts":
            case "instagram_reels":
            case "tiktok":
                ExportResolution = "1080x1920";
                ExportFps = 30;
                ExportFormat = "mp4";
                ExportCodec = "H.264";
                break;
            case "youtube":
                ExportResolution = "1920x1080";
                ExportFps = 30;
                ExportFormat = "mp4";
                ExportCodec = "H.264";
                break;
        }
        StatusMessage = $"🎯 Preset uygulandı: {preset.Replace('_', ' ')}";
    }

    // Fullscreen Preview
    [ObservableProperty] private bool _isFullscreenPreview;

    [RelayCommand]
    private void ApplyTransition(string transitionName)
    {
        var target = SelectedScene ?? ActiveScene;
        if (target != null)
        {
            target.Transition = transitionName;
            if (transitionName.Contains("fade", StringComparison.OrdinalIgnoreCase) || 
                transitionName.Contains("blur", StringComparison.OrdinalIgnoreCase))
            {
                target.FadeInDuration = 0.5;
                target.FadeOutDuration = 0.5;
            }
            SaveHistoryState();
            StatusMessage = $"Geçiş uygulandı: {transitionName}";
            
            // Re-trigger property change explicitly to notify UI
            OnPropertyChanged(nameof(SelectedScene));
        }
    }

    [RelayCommand]
    private void ApplyEffect(string effectName)
    {
        var target = SelectedScene ?? ActiveScene;
        if (target != null)
        {
            target.Filter = effectName;
            SaveHistoryState();
            StatusMessage = $"Efekt uygulandı: {effectName}";
        }
    }

    [RelayCommand]
    private void SelectTextOverlay(TextOverlayDto? dto)
    {
        SelectedTextOverlay = dto;
        if (dto != null) SelectedPropertiesTab = 2;
    }

    [RelayCommand]
    private void DeleteTextOverlay()
    {
        if (SelectedTextOverlay != null && SelectedScene != null)
        {
            SelectedScene.TextOverlays.Remove(SelectedTextOverlay);
            SelectedTextOverlay = null;
            SaveHistoryState();
            StatusMessage = "🗑️ Metin Silindi.";
        }
    }

    [RelayCommand]
    private void ApplyTextStyle(string styleName)
    {
        var target = SelectedScene ?? ActiveScene;
        
        // Auto-select first scene if none is selected
        if (target == null && Scenes.Count > 0)
        {
            target = Scenes[0];
            SelectedScene = target;
        }
        
        if (target == null)
        {
            StatusMessage = "⚠️ Önce bir sahne ekleyin veya seçin.";
            return;
        }
        
        {
            SelectedPropertiesTab = 2;

            // ALWAYS spawn a new text overlay on each call
            var txt = new TextOverlayDto() { Text = "Yeni Metin" };

            switch (styleName.ToLowerInvariant())
            {
                case "plain":
                    txt.FontFamily = "Inter"; txt.FontSize = 36; txt.Color = "#FFFFFF";
                    break;
                case "neon":
                    txt.FontFamily = "Inter"; txt.FontSize = 48; txt.Color = "#EC4899";
                    txt.OutlineThickness = 2;
                    break;
                case "glitch":
                    txt.FontFamily = "Courier New"; txt.FontSize = 42; txt.Color = "#10B981";
                    break;
                case "groovy":
                    txt.FontFamily = "Georgia"; txt.FontSize = 52; txt.Color = "#F59E0B";
                    break;
                case "funky":
                    txt.FontFamily = "Impact"; txt.FontSize = 56; txt.Color = "#8B5CF6";
                    break;
                case "subscribe":
                    txt.FontFamily = "Inter"; txt.FontSize = 32; txt.Color = "#EF4444";
                    break;
                case "lower_third":
                    txt.FontFamily = "Inter"; txt.FontSize = 24; txt.Color = "#F8FAFC";
                    txt.Alignment = "Left";
                    txt.PositionX = 0.5;
                    txt.PositionY = 0.9;
                    break;
                default:
                    txt.FontFamily = "Inter"; txt.FontSize = 36; txt.Color = "#FFFFFF";
                    break;
            }

            target.TextOverlays.Add(txt);
            SelectedTextOverlay = txt;

            SaveHistoryState();
            StatusMessage = $"✅ Metin eklendi ({target.TextOverlays.Count} adet): {styleName}";
        }
    }

    [RelayCommand]
    private async Task ImportSrt()
    {
        var target = SelectedScene ?? ActiveScene;
        if (target == null)
        {
            StatusMessage = "⚠️ Altyazı eklemek için önce bir sahne seçin.";
            return;
        }

        var dlg = new OpenFileDialog { Filter = "SubRip Subtitle (*.srt)|*.srt" };
        if (dlg.ShowDialog() == true)
        {
            try
            {
                var lines = await File.ReadAllLinesAsync(dlg.FileName);
                int count = 0;
                TextOverlayDto? currentSubtitle = null;
                
                foreach(var line in lines)
                {
                    var trimmed = line.Trim();
                    if (string.IsNullOrEmpty(trimmed)) continue;
                    
                    if (int.TryParse(trimmed, out _))
                    {
                        // New block
                        currentSubtitle = new TextOverlayDto 
                        {
                            Text = "",
                            FontFamily = "Inter",
                            FontSize = 36,
                            Color = "#FFFFFF",
                            OutlineThickness = 2,
                            Alignment = "Center",
                            PositionX = 0.5,
                            PositionY = 0.9,
                            Animation = "none"
                        };
                        target.TextOverlays.Add(currentSubtitle);
                        count++;
                    }
                    else if (trimmed.Contains("-->"))
                    {
                        if (currentSubtitle != null)
                        {
                            var parts = trimmed.Split(new[] { "-->" }, StringSplitOptions.None);
                            if (parts.Length == 2 && TimeSpan.TryParse(parts[0].Trim().Replace(',', '.'), out var start) && TimeSpan.TryParse(parts[1].Trim().Replace(',', '.'), out var end))
                            {
                                currentSubtitle.StartTime = start.TotalSeconds;
                                currentSubtitle.EndTime = end.TotalSeconds;
                            }
                        }
                    }
                    else
                    {
                        if (currentSubtitle != null)
                        {
                            if (!string.IsNullOrEmpty(currentSubtitle.Text)) currentSubtitle.Text += "\n";
                            currentSubtitle.Text += trimmed;
                        }
                    }
                }
                SaveHistoryState();
                StatusMessage = $"✅ {count} altyazı eklendi.";
            }
            catch (Exception ex)
            {
                StatusMessage = $"⚠️ SRT yüklenemedi: {ex.Message}";
            }
        }
    }

    [RelayCommand]
    private void ToggleFullscreenPreview() => IsFullscreenPreview = !IsFullscreenPreview;

    [RelayCommand]
    private void ToggleExportPanel() => IsExportPanelOpen = !IsExportPanelOpen;

    [RelayCommand]
    private async Task ExportVideoToFile()
    {
        if (string.IsNullOrEmpty(VideoFilePath) && string.IsNullOrEmpty(OutputVideoPath))
        {
            StatusMessage = "⚠️ No video to export. Import a video first.";
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Title = "Export Video",
            Filter = "MP4 Video|*.mp4|MKV Video|*.mkv|AVI Video|*.avi|WebM Video|*.webm",
            DefaultExt = ".mp4",
            FileName = $"{ProjectTitle ?? "EchoForge_Export"}_{DateTime.Now:yyyyMMdd_HHmmss}"
        };

        if (dlg.ShowDialog() != true) return;

        string inputPath = !string.IsNullOrEmpty(VideoFilePath) ? VideoFilePath : OutputVideoPath;
        string outputPath = dlg.FileName;

        if (!System.IO.File.Exists(inputPath))
        {
            StatusMessage = "⚠️ Source video not found.";
            return;
        }

        IsExporting = true;
        ExportProgress = 0;
        ExportStatusText = "Preparing export...";
        StatusMessage = "🎬 Exporting video...";

        try
        {
            // Build FFmpeg args
            var res = ExportResolution.Split('x');
            int w = int.TryParse(res[0], out var rw) ? rw : 1920;
            int h = res.Length > 1 && int.TryParse(res[1], out var rh) ? rh : 1080;

            // Trim to scene timeline (start to total duration)
            double trimStart = 0;
            double trimEnd = _totalPlaybackTime;

            string speedFilter = Math.Abs(Speed - 1.0) > 0.01
                ? $"-filter_complex \"[0:v]setpts={1.0 / Speed}*PTS[v];[0:a]atempo={Speed}[a]\" -map \"[v]\" -map \"[a]\""
                : "";

            // Determine codec
            string codecArgs = ExportCodec switch
            {
                "H.265" => "-c:v libx265 -preset medium -crf 28",
                "VP9" => "-c:v libvpx-vp9 -crf 30 -b:v 0",
                _ => "-c:v libx264 -preset medium -crf 23" // H.264 default
            };

            string ffmpegArgs = $"-y -i \"{inputPath}\" -ss {trimStart:F3} -t {trimEnd:F3} " +
                               $"-vf \"scale={w}:{h}:force_original_aspect_ratio=decrease,pad={w}:{h}:(ow-iw)/2:(oh-ih)/2\" " +
                               (string.IsNullOrEmpty(speedFilter) ? "" : speedFilter + " ") +
                               $"-r {ExportFps} {codecArgs} -c:a aac -b:a 192k \"{outputPath}\"";

            ExportStatusText = "Encoding...";
            ExportProgress = 10;

            var ffmpegPath = FindFFmpeg();
            if (string.IsNullOrEmpty(ffmpegPath))
            {
                StatusMessage = "⚠️ FFmpeg not found. Install FFmpeg and add to PATH.";
                IsExporting = false;
                return;
            }

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = ffmpegPath,
                Arguments = ffmpegArgs,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            var process = new System.Diagnostics.Process { StartInfo = psi };
            process.Start();

            ExportProgress = 30;
            ExportStatusText = "Encoding video...";

            // Read stderr async for progress
            _ = Task.Run(async () =>
            {
                using var reader = process.StandardError;
                while (!reader.EndOfStream)
                {
                    var line = await reader.ReadLineAsync();
                    if (line != null && line.Contains("time="))
                    {
                        // Parse progress from FFmpeg output
                        var timeMatch = System.Text.RegularExpressions.Regex.Match(line, @"time=(\d+):(\d+):(\d+)\.(\d+)");
                        if (timeMatch.Success)
                        {
                            double hrs = double.Parse(timeMatch.Groups[1].Value);
                            double min = double.Parse(timeMatch.Groups[2].Value);
                            double sec = double.Parse(timeMatch.Groups[3].Value);
                            double encoded = hrs * 3600 + min * 60 + sec;
                            double pct = Math.Min(90, 30 + (encoded / Math.Max(1, _totalPlaybackTime)) * 60);
                            System.Windows.Application.Current?.Dispatcher.Invoke(() =>
                            {
                                ExportProgress = pct;
                                ExportStatusText = $"Encoding... {pct:F0}%";
                            });
                        }
                    }
                }
            });

            await process.WaitForExitAsync();

            if (process.ExitCode == 0)
            {
                ExportProgress = 100;
                ExportStatusText = "Export complete!";
                StatusMessage = $"✅ Exported: {Path.GetFileName(outputPath)}";
                OutputVideoPath = outputPath;
            }
            else
            {
                StatusMessage = $"⚠️ Export failed (exit code {process.ExitCode}).";
                ExportStatusText = "Export failed.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"⚠️ Export error: {ex.Message}";
            ExportStatusText = "Export error.";
        }
        finally
        {
            IsExporting = false;
        }
    }

    private string? FindFFmpeg()
    {
        // Check common locations
        string[] candidates = {
            "ffmpeg.exe",
            @"C:\ffmpeg\bin\ffmpeg.exe",
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "ffmpeg", "bin", "ffmpeg.exe")
        };
        foreach (var c in candidates)
            if (System.IO.File.Exists(c)) return c;

        // Try PATH
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "where",
                Arguments = "ffmpeg",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true
            };
            var p = System.Diagnostics.Process.Start(psi);
            var path = p?.StandardOutput.ReadLine()?.Trim();
            p?.WaitForExit();
            if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path)) return path;
        }
        catch { }
        return null;
    }

    // ═══════════════════════════════════
    // SCENE TRIM (drag edge)
    // ═══════════════════════════════════
    public void TrimSceneStart(TimelineItemDto scene, double newDuration)
    {
        if (newDuration < 0.5) newDuration = 0.5;
        scene.Duration = newDuration;
        UpdateTotalDuration();
        SaveHistoryState();
        StatusMessage = $"✂️ Trimmed scene {scene.SceneNumber} to {newDuration:F1}s";
    }

    public void TrimSceneEnd(TimelineItemDto scene, double newDuration)
    {
        if (newDuration < 0.5) newDuration = 0.5;
        scene.Duration = newDuration;
        UpdateTotalDuration();
        SaveHistoryState();
        StatusMessage = $"✂️ Trimmed scene {scene.SceneNumber} to {newDuration:F1}s";
    }

    // ═══════════════════════════════════
    // TEMPORARY STATUS
    // ═══════════════════════════════════
    public async void ShowTemporaryStatus(string message, int durationMs = 3000)
    {
        StatusMessage = message;
        await Task.Delay(durationMs);
        if (StatusMessage == message) StatusMessage = string.Empty;
    }


    [RelayCommand]
    private async Task UploadToYouTubeAsync()
    {
        IsLoading = true;
        StatusMessage = "⏳ YouTube'a yükleme başlatılıyor...";
        try
        {
            if (string.IsNullOrEmpty(OutputVideoPath) || !System.IO.File.Exists(OutputVideoPath))
            {
                ShowTemporaryStatus("❌ Video dosyası bulunamadı. Lütfen önce 'Export' işlemini tamamlayın.");
                return;
            }

            var success = await _apiClient.UploadToYouTubeAsync(
                _project.Id,
                OutputVideoPath,
                _project.SeoTitle ?? _project.Title,
                _project.SeoDescription ?? "",
                _project.SeoTags ?? "",
                _project.PrivacyStatus ?? "private",
                SelectedYouTubeChannel?.Id,
                _project.ScheduledPublishAt);

            if (success)
            {
                var updated = await _apiClient.GetProjectAsync(_project.Id);
                if (updated != null && !string.IsNullOrEmpty(updated.YouTubeVideoId))
                {
                    _project.YouTubeVideoId = updated.YouTubeVideoId;
                }
                await _apiClient.UpdateProjectStatusAsync(_project.Id, EchoForge.Core.Models.ProjectStatus.Completed);
                ShowTemporaryStatus("✅ Video başarıyla YouTube'a yüklendi!");
            }
            else
            {
                StatusMessage = "❌ YouTube yükleme başarısız oldu.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApproveProjectAsync()
    {
        IsLoading = true;
        StatusMessage = "⏳ Proje onaylanıyor...";
        try
        {
            await _apiClient.UpdateProjectStatusAsync(_project.Id, EchoForge.Core.Models.ProjectStatus.Completed);
            ShowTemporaryStatus("✅ Proje başarıyla onaylandı!");
        }
        catch (Exception ex)
        {
            StatusMessage = $"Hata: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public class ImportedMediaItem
{
    public string FileName { get; set; } = "";
    public string FilePath { get; set; } = "";
    public string MediaType { get; set; } = "";
    public bool IsVideo { get; set; }
    public bool IsAudio { get; set; }
    public bool IsImage { get; set; }
    public string Icon => MediaType switch { "Video" => "🎬", "Audio" => "🎵", "Image" => "🖼️", _ => "📄" };
    public string ThumbnailPath { get; set; } = "";
}
