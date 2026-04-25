using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using EchoForge.Core.DTOs;
using EchoForge.WPF.ViewModels;
using LibVLCSharp.Shared;

namespace EchoForge.WPF.Views;

public partial class StandaloneEditorView : UserControl
{
    private string _lastTransition = "";

    public StandaloneEditorView()
    {
        InitializeComponent();
        Loaded += StandaloneEditorView_Loaded;
        SizeChanged += (s, e) => DrawTimeRuler();

        DataContextChanged += (s, e) =>
        {
            if (e.OldValue is StandaloneEditorViewModel oldVm)
            {
                oldVm.AudioPlaybackChanged -= Vm_AudioPlaybackChanged;
                oldVm.AudioSeeked -= Vm_AudioSeeked;
                oldVm.PropertyChanged -= Vm_PropertyChangedForTimeline;
                oldVm.RequestOpenFileDialog -= Vm_RequestOpenFileDialog;
            }

            if (e.NewValue is StandaloneEditorViewModel vm)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(StandaloneEditorViewModel.PlayheadPixelPosition))
                    {
                        // Auto-scroll timeline when playing
                        if (vm.IsPlaying && TimelineScrollViewer != null)
                        {
                            double x = vm.PlayheadPixelPosition;
                            double viewportWidth = TimelineScrollViewer.ViewportWidth;
                            double offset = TimelineScrollViewer.HorizontalOffset;
                            
                            // If playhead crosses 80% of viewport, pan smoothly keeping it at 20%
                            if (viewportWidth > 0 && x > offset + viewportWidth * 0.8)
                            {
                                TimelineScrollViewer.ScrollToHorizontalOffset(x - viewportWidth * 0.2);
                            }
                            // Or if user seeks out of view, center it
                            else if (viewportWidth > 0 && (x < offset || x > offset + viewportWidth))
                            {
                                TimelineScrollViewer.ScrollToHorizontalOffset(Math.Max(0, x - viewportWidth / 2));
                            }
                        }
                        return;
                    }

                    if (args.PropertyName == nameof(StandaloneEditorViewModel.IsPlaying) ||
                        args.PropertyName == nameof(StandaloneEditorViewModel.CurrentPlaybackTimeDisplay) ||
                        args.PropertyName == nameof(StandaloneEditorViewModel.PreviewOpacity))
                        return;

                    if (args.PropertyName == nameof(StandaloneEditorViewModel.ZoomScale))
                        DrawTimeRuler();

                    if (args.PropertyName == nameof(StandaloneEditorViewModel.SelectedScene))
                        WatchSceneTransition(vm);
                };

                vm.AudioPlaybackChanged += Vm_AudioPlaybackChanged;
                vm.AudioSeeked += Vm_AudioSeeked;
                vm.SpeedChanged += Vm_SpeedChanged;
                vm.PropertyChanged += Vm_PropertyChangedForTimeline;
                vm.RequestOpenFileDialog += Vm_RequestOpenFileDialog;

                TryLoadAudio(vm);
                TryLoadVideo(vm);
                WatchSceneTransition(vm);
            }
        };

        // Keyboard shortcuts
        PreviewKeyDown += (s, ke) =>
        {
            if (DataContext is not StandaloneEditorViewModel vm) return;
            switch (ke.Key)
            {
                case System.Windows.Input.Key.Space:
                    vm.TogglePlaybackCommand.Execute(null);
                    ke.Handled = true;
                    break;
                case System.Windows.Input.Key.Home:
                    vm.SeekToStartCommand.Execute(null);
                    ke.Handled = true;
                    break;
                case System.Windows.Input.Key.Left:
                    vm.StepBackwardCommand.Execute(null);
                    ke.Handled = true;
                    break;
                case System.Windows.Input.Key.Right:
                    vm.StepForwardCommand.Execute(null);
                    ke.Handled = true;
                    break;
                case System.Windows.Input.Key.End:
                    vm.SeekToEndCommand.Execute(null);
                    ke.Handled = true;
                    break;
                case System.Windows.Input.Key.S:
                    vm.SplitSceneCommand.Execute(null);
                    ke.Handled = true;
                    break;
                case System.Windows.Input.Key.Delete:
                    if (vm.SelectedTextOverlay != null)
                        vm.DeleteTextOverlayCommand.Execute(null);
                    else
                        vm.DeleteSceneCommand.Execute(null);
                    ke.Handled = true;
                    break;
                case System.Windows.Input.Key.F:
                    if (vm.ToggleFullscreenPreviewCommand.CanExecute(null))
                    {
                        vm.ToggleFullscreenPreviewCommand.Execute(null);
                        ke.Handled = true;
                    }
                    break;
                case System.Windows.Input.Key.L:
                    vm.IsLoopEnabled = !vm.IsLoopEnabled;
                    ke.Handled = true;
                    break;
            }
        };
    }

    // ═══════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════
    private void StandaloneEditorView_Loaded(object sender, RoutedEventArgs e)
    {
        DrawTimeRuler();
        if (DataContext is StandaloneEditorViewModel vm)
        {
            TryLoadAudio(vm);
            TryLoadVideo(vm);
            // Native MultiBinding now handles automatic scaling mappings!
        }
    }

    // ═══════════════════════════════════════════════════
    // LIBVLC VIDEO ENGINE
    // ═══════════════════════════════════════════════════
    private LibVLC? _libVLC;
    private LibVLCSharp.Shared.MediaPlayer? _vlcPlayer;
    private bool _vlcInitialized = false;

    private void EnsureVlcInitialized()
    {
        if (_vlcInitialized) return;
        LibVLCSharp.Shared.Core.Initialize();
        _libVLC = new LibVLC("--no-audio"); // audio handled by NAudio
        _vlcPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);
        
        _vlcPlayer.LengthChanged += (s, e) =>
        {
            Dispatcher.InvokeAsync(() =>
            {
                if (DataContext is StandaloneEditorViewModel vm)
                {
                    vm.SetVideoDuration(e.Length / 1000.0);
                    DrawTimeRuler();
                }
            });
        };
        
        VideoPlayer.MediaPlayer = _vlcPlayer;
        _vlcInitialized = true;
    }

    private void TryLoadVideo(StandaloneEditorViewModel vm)
    {
        try
        {
            string? videoPath = null;
            if (vm.VideoSource != null)
                videoPath = vm.VideoSource.LocalPath;
            else if (!string.IsNullOrEmpty(vm.OutputVideoPath) && System.IO.File.Exists(vm.OutputVideoPath))
                videoPath = vm.OutputVideoPath;

            if (!string.IsNullOrEmpty(videoPath) && System.IO.File.Exists(videoPath))
                LoadVideoIntoPlayer(videoPath);
        }
        catch { }
    }

    private async void LoadVideoIntoPlayer(string videoPath)
    {
        try
        {
            EnsureVlcInitialized();
            if (_libVLC == null || _vlcPlayer == null) return;

            var media = new Media(_libVLC, videoPath, FromType.FromPath);

            // Yield execution to allow WPF to process the Visibility=Visible layout pass and acquire an HWND.
            // Without this delay, LibVLC will attempt to play on a missing HWND, which spawns an external Direct3D window.
            await System.Threading.Tasks.Task.Delay(250);

            // Play securely on UI Thread to prevent LibVLC window detachment
            await Dispatcher.InvokeAsync(() => 
            {
                if (_vlcPlayer.IsPlaying) _vlcPlayer.Stop();
                _vlcPlayer.Play(media);
            });

            // Pause after a brief moment to show first frame
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(500);
                await Dispatcher.InvokeAsync(() =>
                {
                    try { _vlcPlayer?.SetPause(true); } catch { }
                });
            });
        }
        catch (Exception ex)
        {
            if (DataContext is StandaloneEditorViewModel vm)
                vm.StatusMessage = $"⚠️ Video load failed: {ex.Message}";
        }
    }

    // ═══════════════════════════════════════════════════
    // NAUDIO ENGINE
    // ═══════════════════════════════════════════════════
    private NAudio.Wave.WaveOutEvent? _waveOut;
    private NAudio.Wave.AudioFileReader? _audioFile;
    private bool _audioLoaded = false;

    private void TryLoadAudio(StandaloneEditorViewModel vm)
    {
        if (_audioLoaded) return;
        if (string.IsNullOrEmpty(vm.AudioFilePath)) return;

        var fullPath = System.IO.Path.GetFullPath(vm.AudioFilePath);
        if (!System.IO.File.Exists(fullPath))
        {
            vm.StatusMessage = "🎵 Audio file not found.";
            return;
        }

        try
        {
            DisposeAudio();
            _audioFile = new NAudio.Wave.AudioFileReader(fullPath);
            _audioFile.Volume = (float)vm.AudioVolume;
            _waveOut = new NAudio.Wave.WaveOutEvent();
            _waveOut.Init(_audioFile);
            _audioLoaded = true;
            vm.ShowTemporaryStatus($"🎵 Audio loaded ({_audioFile.TotalTime.TotalSeconds:F1}s)");
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"🎵 Audio error: {ex.Message}";
            _audioLoaded = false;
        }

        Unloaded += (s, e) => DisposeAudio();
    }

    private void DisposeAudio()
    {
        _waveOut?.Stop();
        _waveOut?.Dispose();
        _waveOut = null;
        _audioFile?.Dispose();
        _audioFile = null;
        _audioLoaded = false;
    }

    private void Vm_SpeedChanged(object? sender, double speed)
    {
        try
        {
            _vlcPlayer?.SetRate((float)speed);
        }
        catch { }
    }

    private void Vm_AudioPlaybackChanged(object? sender, string state)
    {
        // VLC video
        try
        {
            if (state == "play")
                _vlcPlayer?.SetPause(false);
            else
                _vlcPlayer?.SetPause(true);
        }
        catch { }

        // NAudio
        if (_waveOut != null && _audioFile != null)
        {
            if (state == "play")
            {
                if (sender is StandaloneEditorViewModel vm)
                {
                    var targetPos = TimeSpan.FromSeconds(vm.CurrentPlaybackTimeRaw);
                    if (targetPos <= _audioFile.TotalTime)
                        _audioFile.CurrentTime = targetPos;
                }
                _waveOut.Play();
            }
            else
                _waveOut.Pause();
        }
    }

    private void Vm_AudioSeeked(object? sender, double timeInSeconds)
    {
        if (_audioFile != null)
        {
            var newPosition = TimeSpan.FromSeconds(timeInSeconds);
            if (newPosition <= _audioFile.TotalTime)
                _audioFile.CurrentTime = newPosition;
        }

        // Seek video too
        try
        {
            if (_vlcPlayer != null && _vlcPlayer.Length > 0)
            {
                long seekMs = (long)(timeInSeconds * 1000);
                if (seekMs >= 0 && seekMs <= _vlcPlayer.Length)
                    _vlcPlayer.Time = seekMs;
            }
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════
    // TRIM HANDLES (Clip edge dragging)
    // ═══════════════════════════════════════════════════
    private TimelineItemDto? _trimScene;
    private bool _trimFromLeft;
    private double _trimStartX;
    private double _trimOriginalDuration;

    private void TrimHandle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe)
        {
            // Find the parent Border to get the scene
            var parent = fe.Parent as FrameworkElement;
            while (parent != null && !(parent.DataContext is TimelineItemDto))
                parent = parent.Parent as FrameworkElement;

            if (parent?.DataContext is TimelineItemDto scene)
            {
                _trimScene = scene;
                _trimFromLeft = fe.Tag?.ToString() == "Left";
                _trimStartX = e.GetPosition(null).X;
                _trimOriginalDuration = scene.Duration;
                fe.CaptureMouse();
                e.Handled = true;
            }
        }
    }

    private void TrimHandle_MouseMove(object sender, MouseEventArgs e)
    {
        if (_trimScene != null && e.LeftButton == MouseButtonState.Pressed && sender is FrameworkElement fe)
        {
            double deltaX = e.GetPosition(null).X - _trimStartX;
            var vm = DataContext as StandaloneEditorViewModel;
            double pixelsPerSec = 50 * (vm?.ZoomScale ?? 1.0);

            double deltaSec = deltaX / pixelsPerSec;
            double newDuration;

            if (_trimFromLeft)
                newDuration = _trimOriginalDuration - deltaSec; // Dragging left handle right = shorter
            else
                newDuration = _trimOriginalDuration + deltaSec; // Dragging right handle right = longer

            // Clamp to minimum 0.5s
            _trimScene.Duration = Math.Max(0.5, newDuration);
            vm?.UpdateTotalDuration();
        }
    }

    private void TrimHandle_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_trimScene != null && sender is FrameworkElement fe)
        {
            fe.ReleaseMouseCapture();
            _trimScene = null;

            // Redraw timeline
            DrawTimeRuler();
        }
    }

    private void Vm_PropertyChangedForTimeline(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(StandaloneEditorViewModel.AudioVolume) && sender is StandaloneEditorViewModel vm)
        {
            if (_audioFile != null)
                _audioFile.Volume = (float)vm.AudioVolume;
        }
        // Redraw timeline when TotalPlaybackTime changes
        if (e.PropertyName == nameof(StandaloneEditorViewModel.TotalPlaybackTime) ||
            e.PropertyName == nameof(StandaloneEditorViewModel.TotalDurationDisplay))
        {
            DrawTimeRuler();
        }
        // When VideoSource changes, load video into VLC
        if (e.PropertyName == nameof(StandaloneEditorViewModel.VideoSource) && sender is StandaloneEditorViewModel vm2)
        {
            if (vm2.VideoSource != null)
                LoadVideoIntoPlayer(vm2.VideoSource.LocalPath);
        }
        // When HasVideo changes (e.g. project loaded from dashboard), try to load output video
        if (e.PropertyName == nameof(StandaloneEditorViewModel.HasVideo) && sender is StandaloneEditorViewModel vm3)
        {
            if (vm3.HasVideo && vm3.VideoSource == null && !string.IsNullOrEmpty(vm3.OutputVideoPath))
            {
                if (System.IO.File.Exists(vm3.OutputVideoPath))
                    LoadVideoIntoPlayer(vm3.OutputVideoPath);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // VLC CLEANUP
    // ═══════════════════════════════════════════════════
    private void DisposeVlc()
    {
        try
        {
            _vlcPlayer?.Stop();
            _vlcPlayer?.Dispose();
            _vlcPlayer = null;
            _libVLC?.Dispose();
            _libVLC = null;
            _vlcInitialized = false;
        }
        catch { }
    }

    // ═══════════════════════════════════════════════════
    // TIME RULER
    // ═══════════════════════════════════════════════════
    private void DrawTimeRuler()
    {
        if (TimeRulerCanvas == null) return;
        TimeRulerCanvas.Children.Clear();

        var vm = DataContext as StandaloneEditorViewModel;
        if (vm == null || vm.Scenes.Count == 0) return;

        double totalDuration = vm.TotalPlaybackTime;
        if (vm.AudioDuration > totalDuration) totalDuration = vm.AudioDuration;
        if (totalDuration <= 0) return;

        double canvasWidth = TimeRulerCanvas.ActualWidth;
        if (canvasWidth <= 0) canvasWidth = 800;

        double zoom = vm.ZoomScale > 0 ? vm.ZoomScale : 1.0;
        double totalTrackWidth = totalDuration * 50 * zoom + 100;
        double scrollOffset = TimelineScrollViewer?.HorizontalOffset ?? 0;
        double pixelsPerSecond = (totalTrackWidth - 100) / totalDuration;

        vm.UpdateTimelineLayoutMetrics(totalTrackWidth, pixelsPerSecond, scrollOffset);

        // Pick good tick interval
        double maxTicks = Math.Max(10, canvasWidth / 70);
        double idealInterval = (canvasWidth / pixelsPerSecond) / maxTicks;
        double[] niceIntervals = { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
        double tickInterval = 5;
        foreach (var interval in niceIntervals)
        {
            if (interval >= idealInterval) { tickInterval = interval; break; }
        }

        double visibleStart = scrollOffset / pixelsPerSecond;
        double visibleEnd = (scrollOffset + canvasWidth) / pixelsPerSecond;
        double startTick = Math.Floor(visibleStart / tickInterval) * tickInterval;

        for (double t = startTick; t <= visibleEnd + tickInterval; t += tickInterval)
        {
            if (t < 0) continue;
            double x = (t * pixelsPerSecond) - scrollOffset;
            if (x < -50 || x > canvasWidth + 50) continue;

            // Major tick line
            var line = new Line
            {
                X1 = x, Y1 = 18, X2 = x, Y2 = 24,
                Stroke = new SolidColorBrush(Color.FromRgb(0x4A, 0x55, 0x68)), StrokeThickness = 1
            };
            TimeRulerCanvas.Children.Add(line);

            // Label
            int m = (int)(t / 60), s = (int)(t % 60);
            string label = tickInterval < 1 ? $"{m}:{s:D2}.{(int)((t % 1) * 10)}" : $"{m}:{s:D2}";
            var tb = new TextBlock
            {
                Text = label, FontSize = 10,
                Foreground = new SolidColorBrush(Color.FromRgb(0x64, 0x74, 0x8B))
            };
            Canvas.SetLeft(tb, x + 2);
            Canvas.SetTop(tb, 3);
            TimeRulerCanvas.Children.Add(tb);

            // Sub-ticks
            int subCount = tickInterval >= 1 ? 4 : 2;
            double subInterval = tickInterval / subCount;
            for (int si = 1; si < subCount; si++)
            {
                double subT = t + (si * subInterval);
                double subX = (subT * pixelsPerSecond) - scrollOffset;
                if (subX < 0 || subX > canvasWidth) continue;
                var subLine = new Line
                {
                    X1 = subX, Y1 = 20, X2 = subX, Y2 = 24,
                    Stroke = new SolidColorBrush(Color.FromRgb(0x33, 0x3B, 0x4D)), StrokeThickness = 0.5
                };
                TimeRulerCanvas.Children.Add(subLine);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // TIMELINE CLICK → SEEK
    // ═══════════════════════════════════════════════════
    private void TimeRulerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(TimeRulerCanvas);
        HandleSeek(pos.X);
        _isDraggingPlayhead = true;
        TimeRulerCanvas.CaptureMouse();
    }

    private bool _isDraggingPlayhead = false;

    private void TimeRulerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPlayhead && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(TimeRulerCanvas);
            HandleSeek(pos.X);
        }
    }

    private void TimeRulerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = false;
        TimeRulerCanvas.ReleaseMouseCapture();
    }

    // --- New Handlers for Tracks / Playhead Scrubbing ---

    private void TracksGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var pos = e.GetPosition(TimeRulerCanvas);
        HandleSeek(pos.X);
    }

    private void Playhead_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = true;
        if (sender is FrameworkElement fe) fe.CaptureMouse();
        e.Handled = true;
    }

    private void Playhead_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPlayhead && e.LeftButton == MouseButtonState.Pressed)
        {
            var pos = e.GetPosition(TimeRulerCanvas);
            HandleSeek(pos.X);
            e.Handled = true;
        }
    }

    private void Playhead_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = false;
        if (sender is FrameworkElement fe) fe.ReleaseMouseCapture();
        e.Handled = true;
    }

    private void HandleSeek(double pixelX)
    {
        var vm = DataContext as StandaloneEditorViewModel;
        if (vm == null) return;

        double scrollOffset = TimelineScrollViewer?.HorizontalOffset ?? 0;
        double absolutePixel = pixelX + scrollOffset;
        vm.SeekToPixelPosition(absolutePixel);
    }

    // ═══════════════════════════════════════════════════
    // TIMELINE SCROLL SYNC
    // ═══════════════════════════════════════════════════
    private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        DrawTimeRuler();
    }

    // ═══════════════════════════════════════════════════
    // DRAG & DROP
    // ═══════════════════════════════════════════════════
    private void Border_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (DataContext is StandaloneEditorViewModel vm && files != null)
                vm.HandleDroppedFilesCommand.Execute(files);
        }
    }

    private void AudioTrack_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (files != null && files.Length > 0)
            {
                var audioFile = files[0];
                var vm = DataContext as StandaloneEditorViewModel;
                if (vm != null && (audioFile.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                                   audioFile.EndsWith(".wav", StringComparison.OrdinalIgnoreCase)))
                    vm.SetAudioFile(audioFile);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // FILE DIALOG
    // ═══════════════════════════════════════════════════
    private void Vm_RequestOpenFileDialog(object? sender, EventArgs e)
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Sahne Görseli Seç",
            Filter = "Görsel Dosyaları|*.png;*.jpg;*.jpeg;*.gif;*.webp;*.bmp|Tüm Dosyalar|*.*",
            Multiselect = false
        };
        if (dialog.ShowDialog() == true)
        {
            var vm = DataContext as StandaloneEditorViewModel;
            vm?.ApplyReplacementImage(dialog.FileName);
        }
    }

    // ═══════════════════════════════════════════════════
    // SCENE SELECTION ON CLICK
    // ═══════════════════════════════════════════════════
    private Point _dragStartPoint;

    private void SceneBlock_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        if (sender is FrameworkElement fe && fe.DataContext is TimelineItemDto scene)
        {
            var vm = DataContext as StandaloneEditorViewModel;
            vm?.SelectSceneCommand.Execute(scene);
        }
    }

    private void SceneBlock_MouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed)
        {
            Vector diff = _dragStartPoint - e.GetPosition(null);
            if (Math.Abs(diff.X) > SystemParameters.MinimumHorizontalDragDistance ||
                Math.Abs(diff.Y) > SystemParameters.MinimumVerticalDragDistance)
            {
                if (sender is Border border && border.DataContext is TimelineItemDto scene)
                {
                    DragDrop.DoDragDrop(border, scene, DragDropEffects.Move);
                }
            }
        }
    }

    private void SceneBlock_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TimelineItemDto)))
        {
            e.Effects = DragDropEffects.None;
        }
    }

    private void SceneBlock_Drop(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(TimelineItemDto)) && sender is Border border && border.DataContext is TimelineItemDto targetScene)
        {
            var sourceScene = e.Data.GetData(typeof(TimelineItemDto)) as TimelineItemDto;
            if (sourceScene != null && sourceScene != targetScene)
            {
                var vm = DataContext as StandaloneEditorViewModel;
                vm?.ReorderScene(sourceScene, targetScene);
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // FILTER/EFFECT CARD CLICK
    // ═══════════════════════════════════════════════════
    private void FilterCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string filterName)
        {
            var vm = DataContext as StandaloneEditorViewModel;
            vm?.ApplyFilterCommand.Execute(filterName);
        }
    }

    private void EffectCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string effectName)
        {
            var vm = DataContext as StandaloneEditorViewModel;
            vm?.ApplyEffectCommand.Execute(effectName);
        }
    }

    private void TransitionCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string transitionName)
        {
            var vm = DataContext as StandaloneEditorViewModel;
            vm?.ApplyTransitionCommand.Execute(transitionName);
        }
    }

    private void TextStyleCard_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is string styleName)
        {
            var vm = DataContext as StandaloneEditorViewModel;
            vm?.ApplyTextStyleCommand.Execute(styleName);
        }
    }

    // ═══════════════════════════════════════════════════
    // TRANSITION PREVIEW ANIMATION
    // ═══════════════════════════════════════════════════
    private System.Windows.Threading.DispatcherTimer? _transitionWatchTimer;

    private void WatchSceneTransition(StandaloneEditorViewModel vm)
    {
        _transitionWatchTimer?.Stop();
        if (vm.SelectedScene != null)
        {
            _lastTransition = vm.SelectedScene.Transition ?? "none";
            _transitionWatchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _transitionWatchTimer.Tick += (s, e) =>
            {
                if (vm.SelectedScene == null) return;
                var current = vm.SelectedScene.Transition ?? "none";
                if (current != _lastTransition)
                {
                    _lastTransition = current;
                    PlayTransitionPreview(current);
                }
            };
            _transitionWatchTimer.Start();
        }
    }

    private void PlayTransitionPreview(string transition)
    {
        var previewImage = FindPreviewImage(this);
        if (previewImage == null) return;

        previewImage.RenderTransform = new TranslateTransform(0, 0);
        previewImage.RenderTransformOrigin = new Point(0.5, 0.5);

        var sb = new Storyboard();

        switch (transition.ToLowerInvariant())
        {
            case "fade":
            case "dissolve":
            case "cross fade":
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300));
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(300) };
                Storyboard.SetTarget(fadeOut, previewImage); Storyboard.SetTarget(fadeIn, previewImage);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(fadeOut); sb.Children.Add(fadeIn);
                break;

            case "smoothleft":
            case "wipe left":
                previewImage.RenderTransform = new TranslateTransform(0, 0);
                var slideLeft = new DoubleAnimation(0, -200, TimeSpan.FromMilliseconds(300));
                var slideBack = new DoubleAnimation(-200, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(300) };
                Storyboard.SetTarget(slideLeft, previewImage); Storyboard.SetTarget(slideBack, previewImage);
                Storyboard.SetTargetProperty(slideLeft, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                Storyboard.SetTargetProperty(slideBack, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                sb.Children.Add(slideLeft); sb.Children.Add(slideBack);
                break;

            case "smoothright":
            case "wipe right":
                previewImage.RenderTransform = new TranslateTransform(0, 0);
                var slideRight = new DoubleAnimation(0, 200, TimeSpan.FromMilliseconds(300));
                var slideBack2 = new DoubleAnimation(200, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(300) };
                Storyboard.SetTarget(slideRight, previewImage); Storyboard.SetTarget(slideBack2, previewImage);
                Storyboard.SetTargetProperty(slideRight, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                Storyboard.SetTargetProperty(slideBack2, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                sb.Children.Add(slideRight); sb.Children.Add(slideBack2);
                break;

            case "zoom in":
            case "zoompan":
                previewImage.RenderTransform = new ScaleTransform(1, 1);
                var zIn = new DoubleAnimation(1.0, 1.3, TimeSpan.FromMilliseconds(400));
                var zOut = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(400) };
                var zInY = new DoubleAnimation(1.0, 1.3, TimeSpan.FromMilliseconds(400));
                var zOutY = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(400) };
                foreach (var a in new[] { zIn, zOut }) { Storyboard.SetTarget(a, previewImage); Storyboard.SetTargetProperty(a, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)")); sb.Children.Add(a); }
                foreach (var a in new[] { zInY, zOutY }) { Storyboard.SetTarget(a, previewImage); Storyboard.SetTargetProperty(a, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)")); sb.Children.Add(a); }
                break;

            case "shake":
            case "glitch":
                previewImage.RenderTransform = new TranslateTransform(0, 0);
                var offsets = new[] { (-10, 50), (10, 50), (-8, 50), (6, 50), (0, 50) };
                int accMs = 0;
                double prev = 0;
                foreach (var (target, dur) in offsets)
                {
                    var a = new DoubleAnimation(prev, target, TimeSpan.FromMilliseconds(dur)) { BeginTime = TimeSpan.FromMilliseconds(accMs) };
                    Storyboard.SetTarget(a, previewImage);
                    Storyboard.SetTargetProperty(a, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                    sb.Children.Add(a);
                    accMs += dur; prev = target;
                }
                break;

            case "flash":
                var fO = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(100));
                var fI = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(100) };
                Storyboard.SetTarget(fO, previewImage); Storyboard.SetTarget(fI, previewImage);
                Storyboard.SetTargetProperty(fO, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTargetProperty(fI, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(fO); sb.Children.Add(fI);
                break;

            case "spin":
                previewImage.RenderTransform = new RotateTransform(0);
                var spin = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(500));
                Storyboard.SetTarget(spin, previewImage);
                Storyboard.SetTargetProperty(spin, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
                sb.Children.Add(spin);
                break;

            case "pulse":
                previewImage.RenderTransform = new ScaleTransform(1, 1);
                var pU = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(200));
                var pD = new DoubleAnimation(1.1, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(200) };
                foreach (var prop in new[] { "ScaleX", "ScaleY" })
                {
                    var a1 = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(200));
                    var a2 = new DoubleAnimation(1.1, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(200) };
                    Storyboard.SetTarget(a1, previewImage); Storyboard.SetTarget(a2, previewImage);
                    Storyboard.SetTargetProperty(a1, new PropertyPath($"(UIElement.RenderTransform).(ScaleTransform.{prop})"));
                    Storyboard.SetTargetProperty(a2, new PropertyPath($"(UIElement.RenderTransform).(ScaleTransform.{prop})"));
                    sb.Children.Add(a1); sb.Children.Add(a2);
                }
                break;

            default:
                return;
        }
        sb.Begin();
    }

    private Image? FindPreviewImage(DependencyObject parent)
    {
        for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Image img && img.Source != null && img.Stretch == Stretch.Uniform)
                return img;
            var result = FindPreviewImage(child);
            if (result != null) return result;
        }
        return null;
    }

    // ═══════════════════════════════════════════════════
    // TEXT DRAG & DROP LOGIC
    // ═══════════════════════════════════════════════════
    private bool _isDraggingText;
    private Point _clickPosition;
    private double _initialTranslateX;
    private double _initialTranslateY;
    private TextOverlayDto? _draggedOverlay;

    private void TextOverlay_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element && element.DataContext is TextOverlayDto overlay && DataContext is StandaloneEditorViewModel vm)
        {
            _isDraggingText = true;
            _draggedOverlay = overlay;
            element.CaptureMouse();
            _clickPosition = e.GetPosition(VideoPreviewGrid);
            
            // Select this specific text element visually in the properties pane
            vm.SelectTextOverlayCommand?.Execute(overlay);
            
            _initialTranslateX = (overlay.PositionX - 0.5) * VideoPreviewGrid.ActualWidth;
            _initialTranslateY = (overlay.PositionY - 0.5) * VideoPreviewGrid.ActualHeight;
        }
    }

    private void TextOverlay_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_isDraggingText && _draggedOverlay != null && sender is FrameworkElement element)
        {
            var currentPosition = e.GetPosition(VideoPreviewGrid);
            double deltaX = currentPosition.X - _clickPosition.X;
            double deltaY = currentPosition.Y - _clickPosition.Y;
            
            double newTranslateX = _initialTranslateX + deltaX;
            double newTranslateY = _initialTranslateY + deltaY;
            
            // Map pixels back to 0.0 - 1.0 normalized coordinates
            double newPctX = (newTranslateX / VideoPreviewGrid.ActualWidth) + 0.5;
            double newPctY = (newTranslateY / VideoPreviewGrid.ActualHeight) + 0.5;
            
            // Native MultiValue bindings handle frame-sync mapping natively
            _draggedOverlay.PositionX = Math.Max(0.01, Math.Min(0.99, newPctX));
            _draggedOverlay.PositionY = Math.Max(0.01, Math.Min(0.99, newPctY));
        }
    }

    private void TextOverlay_MouseLeftButtonUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (_isDraggingText && sender is FrameworkElement element && DataContext is StandaloneEditorViewModel vm)
        {
            _isDraggingText = false;
            _draggedOverlay = null;
            element.ReleaseMouseCapture();
            
            // Commit drag modifications to history stack
            vm.SaveHistoryState();
        }
    }
}
