using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.DTOs;
using GongSolutions.Wpf.DragDrop;

namespace EchoForge.WPF.ViewModels;

public partial class EditorViewModel : ObservableObject, IDropTarget
{
    private readonly Services.ApiClient _apiClient;
    private readonly Services.ClientJobOrchestrator _orchestrator;
    private readonly ProjectDto _project;

    [ObservableProperty]
    private string _projectTitle = "";

    [ObservableProperty]
    private ObservableCollection<TimelineItemDto> _scenes = new();

    [ObservableProperty]
    private TimelineItemDto? _selectedScene;

    [ObservableProperty]
    private string _previewImagePath = "";

    [ObservableProperty]
    private double _previewOpacity = 1.0;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "";

    [ObservableProperty]
    private string _totalDurationDisplay = "";

    [ObservableProperty]
    private string _selectedSceneDurationDisplay = "";

    [ObservableProperty]
    private TimelineItemDto? _activeScene;

    [ObservableProperty]
    private bool _isPlaying;

    [ObservableProperty]
    private string _currentPlaybackTimeDisplay = "0:00 / 0:00";

    [ObservableProperty]
    private double _playheadPixelPosition = 60;

    [ObservableProperty]
    private double _zoomScale = 1.0;

    [ObservableProperty]
    private double _rangeInPoint = -1;

    [ObservableProperty]
    private double _rangeOutPoint = -1;

    [ObservableProperty]
    private string _rangeDisplay = "No range set";

    [ObservableProperty]
    private bool _hasRange;

    [ObservableProperty]
    private double _audioVolume = 0.5;

    public double ProjectFadeIn
    {
        get => Scenes.FirstOrDefault()?.FadeInDuration ?? 0;
        set
        {
            if (Scenes.FirstOrDefault() is { } s)
            {
                s.FadeInDuration = value;
                OnPropertyChanged();
            }
        }
    }

    public double ProjectFadeOut
    {
        get => Scenes.LastOrDefault()?.FadeOutDuration ?? 0;
        set
        {
            if (Scenes.LastOrDefault() is { } s)
            {
                s.FadeOutDuration = value;
                OnPropertyChanged();
            }
        }
    }

    private System.Windows.Threading.DispatcherTimer _playbackTimer;
    private double _currentPlaybackTime = 0;
    private double _totalPlaybackTime = 0;
    private double _pixelsPerSecond = 1;
    private double _scrollOffset = 0;
    private Action _goBackAction;

    public int ProjectId => _project.Id;
    public string AudioFilePath => _project.AudioPath ?? string.Empty;

    public EditorViewModel(ProjectDto project, Services.ApiClient apiClient, Services.ClientJobOrchestrator orchestrator, Action goBackAction)
    {
        _apiClient = apiClient;
        _orchestrator = orchestrator;
        _project = project;
        _projectTitle = project.Title ?? "Untitled Project";
        _goBackAction = goBackAction;

        _playbackTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        _playbackTimer.Tick += PlaybackTimer_Tick;

        // Load scenes from project
        foreach (var scene in project.Scenes)
        {
            Scenes.Add(scene);
        }

        UpdateTotalDuration();

        // Select first scene by default
        if (Scenes.Count > 0)
        {
            SelectedScene = Scenes[0];
            ActiveScene = Scenes[0];
        }

        // Initialize history with initial state
        SaveHistoryState();
    }

    public void UpdateTotalDuration()
    {
        _totalPlaybackTime = Scenes.Sum(s => s.Duration);
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
        PlayheadPixelPosition = (_currentPlaybackTime * _pixelsPerSecond) - _scrollOffset + 60; // 60 is track left padding
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

    private void PlaybackTimer_Tick(object? sender, EventArgs e)
    {
        _currentPlaybackTime += 0.05; // 50ms
        if (_currentPlaybackTime >= _totalPlaybackTime)
        {
            _currentPlaybackTime = _totalPlaybackTime;
            IsPlaying = false;
            _playbackTimer.Stop();
            AudioPlaybackChanged?.Invoke(this, "pause");
        }
        
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
    }

    private void UpdateActiveSceneBasedOnTime()
    {
        double accum = 0;
        foreach (var s in Scenes)
        {
            double sceneStart = accum;
            double sceneEnd = accum + s.Duration;
            
            // Check if current time falls within this scene (inclusive of start, exclusive of end mostly except for last scene)
            if (_currentPlaybackTime >= sceneStart && _currentPlaybackTime <= sceneEnd)
            {
                if (ActiveScene != s)
                {
                    ActiveScene = s;
                }
                
                if (!string.IsNullOrEmpty(s.ImagePath))
                {
                    PreviewImagePath = s.ImagePath;
                }

                // Calculate Opacity based on Fade In / Fade Out
                double timeInScene = _currentPlaybackTime - sceneStart;
                double opacity = 1.0;

                if (s.FadeInDuration > 0 && timeInScene < s.FadeInDuration)
                {
                    opacity = timeInScene / s.FadeInDuration;
                }
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

    partial void OnSelectedSceneChanged(TimelineItemDto? value)
    {
        if (value != null && !string.IsNullOrEmpty(value.ImagePath))
        {
            PreviewImagePath = value.ImagePath;
            
            // Calculate start and end time of the selected scene
            double startTime = 0;
            foreach (var s in Scenes)
            {
                if (s.SceneNumber == value.SceneNumber) break;
                startTime += s.Duration;
            }
            double endTime = startTime + value.Duration;
            
            SelectedSceneDurationDisplay = $"Starts at: {(int)(startTime/60)}:{(int)(startTime%60):D2}  —  Ends at: {(int)(endTime/60)}:{(int)(endTime%60):D2}";
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
        {
            PreviewImagePath = scene.ImagePath;
        }
    }

    [RelayCommand]
    private void TogglePlayback()
    {
        if (IsPlaying)
        {
            IsPlaying = false;
            _playbackTimer.Stop();
            AudioPlaybackChanged?.Invoke(this, "pause");
        }
        else
        {
            if (_currentPlaybackTime >= _totalPlaybackTime)
            {
                _currentPlaybackTime = 0; // Restart if at end
            }
            IsPlaying = true;
            _playbackTimer.Start();
            AudioPlaybackChanged?.Invoke(this, "play");
        }
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

    // --- History Control (Undo / Redo) ---
    private readonly List<string> _history = new();
    private int _historyIndex = -1;
    private bool _isRestoringHistory = false;

    public bool CanUndo => _historyIndex > 0;
    public bool CanRedo => _historyIndex < _history.Count - 1;

    public void SaveHistoryState()
    {
        if (_isRestoringHistory) return;

        // If we undo'd and then make a new edit, clear future history
        if (_history.Count > 0 && _historyIndex < _history.Count - 1)
        {
            _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
        }

        var json = System.Text.Json.JsonSerializer.Serialize(Scenes.ToList());
        
        // Prevent stacking identical states
        if (_history.Count > 0 && _historyIndex >= 0 && _history[_historyIndex] == json) return;

        _history.Add(json);
        _historyIndex++;
        
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CommitEdit()
    {
        SaveHistoryState();
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
                foreach(var item in items) Scenes.Add(item);
                UpdateTotalDuration();
                UpdateActiveSceneBasedOnTime();

                // Keep selection valid if it was deleted/moved
                if (SelectedScene != null)
                {
                    SelectedScene = Scenes.FirstOrDefault(s => s.SceneNumber == SelectedScene.SceneNumber);
                }
            }
        }
        finally
        {
            _isRestoringHistory = false;
            UndoCommand.NotifyCanExecuteChanged();
            RedoCommand.NotifyCanExecuteChanged();
        }
    }
    // -------------------------------------

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

    // Events for audio sync
    public event EventHandler<string>? AudioPlaybackChanged;
    public event EventHandler<double>? AudioSeeked;

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
                // We found the scene to split!
                double splitPointInScene = _currentPlaybackTime - accum;
                
                // Don't split if too close to edges (e.g., < 0.5s)
                if (splitPointInScene < 0.5 || s.Duration - splitPointInScene < 0.5)
                {
                    StatusMessage = "⚠️ Too close to clip edge to split.";
                    return;
                }

                // Stop playback during split to prevent weird states
                bool wasPlaying = IsPlaying;
                if (IsPlaying)
                {
                    TogglePlayback();
                }

                // Create the two new halves
                var firstHalf = new TimelineItemDto
                {
                    SceneNumber = s.SceneNumber,
                    Duration = splitPointInScene,
                    Prompt = s.Prompt,
                    ImagePath = s.ImagePath,
                    Transition = "none", // Cut
                    FadeInDuration = s.FadeInDuration,
                    FadeOutDuration = 0,
                    Speed = s.Speed,
                    Filter = s.Filter
                };

                var secondHalf = new TimelineItemDto
                {
                    SceneNumber = s.SceneNumber + 1,
                    Duration = s.Duration - splitPointInScene,
                    Prompt = s.Prompt,
                    ImagePath = s.ImagePath,
                    Transition = s.Transition, // Keep original transition for the second half
                    FadeInDuration = 0,
                    FadeOutDuration = s.FadeOutDuration,
                    Speed = s.Speed,
                    Filter = s.Filter
                };

                // Remove the old scene and insert the new ones
                // This forces the ObservableCollection to update the UI (since TimelineItemDto lacks INotifyPropertyChanged)
                Scenes.RemoveAt(i);
                Scenes.Insert(i, firstHalf);
                Scenes.Insert(i + 1, secondHalf);
                
                RenumberScenes();
                UpdateTotalDuration();
                SaveHistoryState();
                
                // Seek exactly to the split point to resync active scene and audio
                SeekToTime(_currentPlaybackTime);
                
                StatusMessage = "✂️ Scene split successfully!";
                return;
            }
            accum += s.Duration;
        }
    }

    [RelayCommand]
    private void DeleteScene()
    {
        if (SelectedScene == null)
        {
            StatusMessage = "⚠️ Select a scene to delete first.";
            return;
        }

        if (Scenes.Count <= 1)
        {
            StatusMessage = "⚠️ Cannot delete the last scene.";
            return;
        }

        Scenes.Remove(SelectedScene);
        SelectedScene = null;
        RenumberScenes();
        UpdateTotalDuration();
        UpdateActiveSceneBasedOnTime();
        SaveHistoryState();
        StatusMessage = "🗑️ Scene deleted.";
    }

    private void RenumberScenes()
    {
        for (int i = 0; i < Scenes.Count; i++)
        {
            Scenes[i].SceneNumber = i + 1;
        }
    }

    private string FormatTime(double seconds)
    {
        int m = (int)(seconds / 60);
        int s = (int)(seconds % 60);
        return $"{m}:{s:D2}";
    }

    [RelayCommand]
    private void MarkIn()
    {
        RangeInPoint = _currentPlaybackTime;
        UpdateRangeDisplay();
        ShowTemporaryStatus($"📍 In point set at {FormatTime(_currentPlaybackTime)}");
    }

    public async void ShowTemporaryStatus(string message, int durationMs = 3000)
    {
        StatusMessage = message;
        await Task.Delay(durationMs);
        if (StatusMessage == message)
        {
            StatusMessage = string.Empty;
        }
    }

    [RelayCommand]
    private void MarkOut()
    {
        RangeOutPoint = _currentPlaybackTime;
        UpdateRangeDisplay();
        StatusMessage = $"📍 Out point set at {FormatTime(_currentPlaybackTime)}";
    }

    [RelayCommand]
    private void ClearRange()
    {
        RangeInPoint = -1;
        RangeOutPoint = -1;
        HasRange = false;
        RangeDisplay = "No range set";
        StatusMessage = "🧹 Range cleared";
    }

    private void UpdateRangeDisplay()
    {
        if (RangeInPoint >= 0 && RangeOutPoint >= 0 && RangeOutPoint > RangeInPoint)
        {
            HasRange = true;
            double dur = RangeOutPoint - RangeInPoint;
            RangeDisplay = $"Range: {FormatTime(RangeInPoint)} → {FormatTime(RangeOutPoint)} ({dur:F1}s)";
        }
        else if (RangeInPoint >= 0)
        {
            HasRange = false;
            RangeDisplay = $"In: {FormatTime(RangeInPoint)} — Set Out point";
        }
        else
        {
            HasRange = false;
            RangeDisplay = "No range set";
        }
    }

    [RelayCommand]
    private void TrimToRange()
    {
        if (RangeInPoint < 0 || RangeOutPoint < 0 || RangeOutPoint <= RangeInPoint)
        {
            StatusMessage = "⚠️ Set both In and Out points first (Out must be after In).";
            return;
        }

        // Find the scenes that fall within the range and trim them
        double rangeStart = RangeInPoint;
        double rangeEnd = RangeOutPoint;
        var newScenes = new System.Collections.Generic.List<TimelineItemDto>();

        double accum = 0;
        foreach (var s in Scenes)
        {
            double sceneStart = accum;
            double sceneEnd = accum + s.Duration;

            // Does this scene overlap with the range?
            if (sceneEnd > rangeStart && sceneStart < rangeEnd)
            {
                double overlapStart = Math.Max(sceneStart, rangeStart);
                double overlapEnd = Math.Min(sceneEnd, rangeEnd);
                double overlapDuration = overlapEnd - overlapStart;

                if (overlapDuration > 0.1) // minimum 0.1s
                {
                    newScenes.Add(new TimelineItemDto
                    {
                        Duration = overlapDuration,
                        Prompt = s.Prompt,
                        ImagePath = s.ImagePath,
                        Transition = s.Transition,
                        FadeInDuration = s.FadeInDuration,
                        FadeOutDuration = s.FadeOutDuration,
                        Speed = s.Speed,
                        Filter = s.Filter
                    });
                }
            }

            accum += s.Duration;
        }

        if (newScenes.Count == 0)
        {
            StatusMessage = "⚠️ No scenes found in the selected range.";
            return;
        }

        Scenes.Clear();
        foreach (var ns in newScenes) Scenes.Add(ns);
        RenumberScenes();
        UpdateTotalDuration();
        _currentPlaybackTime = 0;
        UpdateActiveSceneBasedOnTime();
        UpdateTimeDisplay();
        UpdatePlayheadPosition();
        ClearRange();
        SaveHistoryState();
        StatusMessage = $"✂️ Trimmed to range! {newScenes.Count} scene(s) kept.";
    }

    [RelayCommand]
    private void GoBack()
    {
        IsPlaying = false;
        _playbackTimer?.Stop();
        _goBackAction?.Invoke();
    }

    [RelayCommand]
    private async Task RegenerateScene()
    {
        if (SelectedScene == null) return;

        IsLoading = true;
        StatusMessage = "Regenerating scene image...";
        try
        {
            var updatedProject = await _apiClient.RegenerateSceneAsync(_project.Id, SelectedScene.SceneNumber, SelectedScene.Prompt);
            if (updatedProject != null)
            {
                var selectedNum = SelectedScene.SceneNumber;
                Scenes.Clear();
                if (!string.IsNullOrEmpty(updatedProject.TimelineJson))
                {
                    var items = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(updatedProject.TimelineJson);
                    if (items != null)
                    {
                        foreach (var item in items)
                        {
                            Scenes.Add(item);
                            if (item.SceneNumber == selectedNum)
                            {
                                SelectedScene = item;
                            }
                        }
                    }
                }
                SaveHistoryState();
                StatusMessage = "✅ Scene regenerated successfully!";
            }
            else
            {
                StatusMessage = "❌ Failed to regenerate scene.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task ApplySceneEdits()
    {
        IsLoading = true;
        StatusMessage = "Saving scene changes...";
        try
        {
            var success = await _apiClient.UpdateProjectScenesAsync(_project.Id, Scenes.ToList());
            StatusMessage = success ? "✅ Scene updates saved!" : "❌ Failed to save scene updates.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task RenderVideo()
    {
        var result = EchoForge.WPF.Views.EchoMessageBox.Show(
            "Finalize all edits and start rendering video?",
            "Confirm Render", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Question);

        if (result != System.Windows.MessageBoxResult.OK) return;

        IsLoading = true;
        StatusMessage = "Starting video render...";
        try
        {
            // Save scenes first
            await _apiClient.UpdateProjectScenesAsync(_project.Id, Scenes.ToList());

            _ = Task.Run(async () =>
            {
                await _orchestrator.ResumePipelineAsync(_project.Id, System.Threading.CancellationToken.None);
            });

            StatusMessage = "🎬 Video rendering started! You can close this editor.";
            EchoForge.WPF.Views.EchoMessageBox.Show("Video rendering has been started locally! The process will run in the background.", "Rendering Started", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Success);
            
            // Navigate back to dashboard instead of closing a window wrapper
            _goBackAction?.Invoke();
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    // --- Drag & Drop Implementation ---
    public void DragOver(IDropInfo dropInfo)
    {
        if (dropInfo.Data is TimelineItemDto && dropInfo.TargetItem is TimelineItemDto)
        {
            dropInfo.DropTargetAdorner = DropTargetAdorners.Insert;
            dropInfo.Effects = System.Windows.DragDropEffects.Move;
        }
    }

    public void Drop(IDropInfo dropInfo)
    {
        if (dropInfo.Data is TimelineItemDto sourceItem && dropInfo.TargetItem is TimelineItemDto targetItem)
        {
            int sourceIndex = Scenes.IndexOf(sourceItem);
            
            // Getting index BEFORE insertion
            int targetIndex = dropInfo.InsertIndex;
            
            if (sourceIndex != targetIndex)
            {
                // Simple ObservableCollection Move
                Scenes.Move(sourceIndex, sourceIndex < targetIndex ? targetIndex - 1 : targetIndex);
                
                RenumberScenes();
                UpdateTotalDuration();
                UpdateActiveSceneBasedOnTime();
                SaveHistoryState();
                StatusMessage = "🔁 Scene order updated";
            }
        }
    }
}
