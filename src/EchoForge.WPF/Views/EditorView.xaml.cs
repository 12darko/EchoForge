using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using EchoForge.WPF.ViewModels;

namespace EchoForge.WPF.Views;

public partial class EditorView : UserControl
{
    private string _lastTransition = "";

    public EditorView()
    {
        InitializeComponent();
        Loaded += EditorView_Loaded;
        SizeChanged += (s, e) => DrawTimeRuler();

        DataContextChanged += (s, e) =>
        {
            if (e.OldValue is EditorViewModel oldVm)
            {
                oldVm.AudioPlaybackChanged -= Vm_AudioPlaybackChanged;
                oldVm.AudioSeeked -= Vm_AudioSeeked;
                oldVm.PropertyChanged -= Vm_PropertyChangedForAudio;
            }

            if (e.NewValue is EditorViewModel vm)
            {
                vm.PropertyChanged += (sender, args) =>
                {
                    if (args.PropertyName == nameof(EditorViewModel.IsPlaying) ||
                        args.PropertyName == nameof(EditorViewModel.PlayheadPixelPosition) ||
                        args.PropertyName == nameof(EditorViewModel.CurrentPlaybackTimeDisplay) ||
                        args.PropertyName == nameof(EditorViewModel.PreviewOpacity))
                        return;
                        
                    if (args.PropertyName == nameof(EditorViewModel.ZoomScale))
                    {
                        DrawTimeRuler();
                    }

                    // Watch for SelectedScene changes to monitor Transition property
                    if (args.PropertyName == nameof(EditorViewModel.SelectedScene))
                    {
                        WatchSceneTransition(vm);
                    }
                };

                vm.AudioPlaybackChanged += Vm_AudioPlaybackChanged;
                vm.AudioSeeked += Vm_AudioSeeked;
                vm.PropertyChanged += Vm_PropertyChangedForAudio;

                TryLoadAudio(vm);
                WatchSceneTransition(vm);
            }
        };
    }

    private System.Windows.Threading.DispatcherTimer? _transitionWatchTimer;

    private void WatchSceneTransition(EditorViewModel vm)
    {
        _transitionWatchTimer?.Stop();
        
        if (vm.SelectedScene != null)
        {
            _lastTransition = vm.SelectedScene.Transition ?? "none";
            _transitionWatchTimer = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _transitionWatchTimer.Tick += (s, e) =>
            {
                if (vm.SelectedScene == null) return;
                var currentTransition = vm.SelectedScene.Transition ?? "none";
                if (currentTransition != _lastTransition)
                {
                    _lastTransition = currentTransition;
                    PlayTransitionPreview(currentTransition);
                }
            };
            _transitionWatchTimer.Start();
        }
    }

    private void PlayTransitionPreview(string transition)
    {
        // Find the preview Image element
        var previewImage = FindPreviewImage(this);
        if (previewImage == null) return;

        // Reset any existing transform
        previewImage.RenderTransform = new TranslateTransform(0, 0);
        previewImage.RenderTransformOrigin = new Point(0.5, 0.5);

        var sb = new Storyboard();

        switch (transition.ToLowerInvariant())
        {
            case "fade":
            case "dissolve":
                // Opacity fade out then back in
                var fadeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(300));
                var fadeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(300) };
                Storyboard.SetTarget(fadeOut, previewImage);
                Storyboard.SetTarget(fadeIn, previewImage);
                Storyboard.SetTargetProperty(fadeOut, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTargetProperty(fadeIn, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(fadeOut);
                sb.Children.Add(fadeIn);
                break;

            case "smoothleft":
                previewImage.RenderTransform = new TranslateTransform(0, 0);
                var slideLeft = new DoubleAnimation(0, -200, TimeSpan.FromMilliseconds(300));
                var slideBack = new DoubleAnimation(-200, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(300) };
                Storyboard.SetTarget(slideLeft, previewImage);
                Storyboard.SetTarget(slideBack, previewImage);
                Storyboard.SetTargetProperty(slideLeft, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                Storyboard.SetTargetProperty(slideBack, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                sb.Children.Add(slideLeft);
                sb.Children.Add(slideBack);
                break;

            case "smoothright":
                previewImage.RenderTransform = new TranslateTransform(0, 0);
                var slideRight = new DoubleAnimation(0, 200, TimeSpan.FromMilliseconds(300));
                var slideRightBack = new DoubleAnimation(200, 0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(300) };
                Storyboard.SetTarget(slideRight, previewImage);
                Storyboard.SetTarget(slideRightBack, previewImage);
                Storyboard.SetTargetProperty(slideRight, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                Storyboard.SetTargetProperty(slideRightBack, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                sb.Children.Add(slideRight);
                sb.Children.Add(slideRightBack);
                break;

            case "zoompan":
                previewImage.RenderTransform = new ScaleTransform(1, 1);
                var zoomIn = new DoubleAnimation(1.0, 1.3, TimeSpan.FromMilliseconds(400));
                var zoomOut = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(400) };
                var zoomInY = new DoubleAnimation(1.0, 1.3, TimeSpan.FromMilliseconds(400));
                var zoomOutY = new DoubleAnimation(1.3, 1.0, TimeSpan.FromMilliseconds(400)) { BeginTime = TimeSpan.FromMilliseconds(400) };
                Storyboard.SetTarget(zoomIn, previewImage);
                Storyboard.SetTarget(zoomOut, previewImage);
                Storyboard.SetTarget(zoomInY, previewImage);
                Storyboard.SetTarget(zoomOutY, previewImage);
                Storyboard.SetTargetProperty(zoomIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
                Storyboard.SetTargetProperty(zoomOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
                Storyboard.SetTargetProperty(zoomInY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
                Storyboard.SetTargetProperty(zoomOutY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
                sb.Children.Add(zoomIn);
                sb.Children.Add(zoomOut);
                sb.Children.Add(zoomInY);
                sb.Children.Add(zoomOutY);
                break;

            case "shake":
            case "glitch":
                previewImage.RenderTransform = new TranslateTransform(0, 0);
                var shake1 = new DoubleAnimation(0, -10, TimeSpan.FromMilliseconds(50));
                var shake2 = new DoubleAnimation(-10, 10, TimeSpan.FromMilliseconds(50)) { BeginTime = TimeSpan.FromMilliseconds(50) };
                var shake3 = new DoubleAnimation(10, -8, TimeSpan.FromMilliseconds(50)) { BeginTime = TimeSpan.FromMilliseconds(100) };
                var shake4 = new DoubleAnimation(-8, 6, TimeSpan.FromMilliseconds(50)) { BeginTime = TimeSpan.FromMilliseconds(150) };
                var shake5 = new DoubleAnimation(6, 0, TimeSpan.FromMilliseconds(50)) { BeginTime = TimeSpan.FromMilliseconds(200) };
                foreach (var anim in new[] { shake1, shake2, shake3, shake4, shake5 })
                {
                    Storyboard.SetTarget(anim, previewImage);
                    Storyboard.SetTargetProperty(anim, new PropertyPath("(UIElement.RenderTransform).(TranslateTransform.X)"));
                    sb.Children.Add(anim);
                }
                break;

            case "flash":
                var flashOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(100));
                var flashIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(100) };
                Storyboard.SetTarget(flashOut, previewImage);
                Storyboard.SetTarget(flashIn, previewImage);
                Storyboard.SetTargetProperty(flashOut, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTargetProperty(flashIn, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(flashOut);
                sb.Children.Add(flashIn);
                break;

            case "pulse":
                previewImage.RenderTransform = new ScaleTransform(1, 1);
                var pulseUp = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(200));
                var pulseDown = new DoubleAnimation(1.1, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(200) };
                var pulseUpY = new DoubleAnimation(1.0, 1.1, TimeSpan.FromMilliseconds(200));
                var pulseDownY = new DoubleAnimation(1.1, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(200) };
                Storyboard.SetTarget(pulseUp, previewImage);
                Storyboard.SetTarget(pulseDown, previewImage);
                Storyboard.SetTarget(pulseUpY, previewImage);
                Storyboard.SetTarget(pulseDownY, previewImage);
                Storyboard.SetTargetProperty(pulseUp, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
                Storyboard.SetTargetProperty(pulseDown, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
                Storyboard.SetTargetProperty(pulseUpY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
                Storyboard.SetTargetProperty(pulseDownY, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
                sb.Children.Add(pulseUp);
                sb.Children.Add(pulseDown);
                sb.Children.Add(pulseUpY);
                sb.Children.Add(pulseDownY);
                break;

            case "circlecrop":
            case "pixelize":
                // Simulated: quick opacity bounce
                var pixFade = new DoubleAnimation(1.0, 0.3, TimeSpan.FromMilliseconds(200));
                var pixBack = new DoubleAnimation(0.3, 1.0, TimeSpan.FromMilliseconds(300)) { BeginTime = TimeSpan.FromMilliseconds(200) };
                Storyboard.SetTarget(pixFade, previewImage);
                Storyboard.SetTarget(pixBack, previewImage);
                Storyboard.SetTargetProperty(pixFade, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTargetProperty(pixBack, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(pixFade);
                sb.Children.Add(pixBack);
                break;

            case "wipe":
                previewImage.RenderTransformOrigin = new Point(0, 0.5);
                previewImage.RenderTransform = new ScaleTransform(1, 1);
                var wipeOut = new DoubleAnimation(1.0, 0.0, TimeSpan.FromMilliseconds(250));
                var wipeIn = new DoubleAnimation(0.0, 1.0, TimeSpan.FromMilliseconds(250)) { BeginTime = TimeSpan.FromMilliseconds(250) };
                Storyboard.SetTarget(wipeOut, previewImage);
                Storyboard.SetTarget(wipeIn, previewImage);
                Storyboard.SetTargetProperty(wipeOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
                Storyboard.SetTargetProperty(wipeIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleX)"));
                sb.Children.Add(wipeOut);
                sb.Children.Add(wipeIn);
                break;

            case "blinds":
                previewImage.RenderTransform = new ScaleTransform(1, 1);
                var blindOut = new DoubleAnimation(1.0, 0.1, TimeSpan.FromMilliseconds(200)) { RepeatBehavior = new RepeatBehavior(2) };
                var blindIn = new DoubleAnimation(0.1, 1.0, TimeSpan.FromMilliseconds(200)) { BeginTime = TimeSpan.FromMilliseconds(400) };
                Storyboard.SetTarget(blindOut, previewImage);
                Storyboard.SetTarget(blindIn, previewImage);
                Storyboard.SetTargetProperty(blindOut, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
                Storyboard.SetTargetProperty(blindIn, new PropertyPath("(UIElement.RenderTransform).(ScaleTransform.ScaleY)"));
                sb.Children.Add(blindOut);
                sb.Children.Add(blindIn);
                break;
                
            case "spin":
                previewImage.RenderTransform = new RotateTransform(0);
                var spinAnim = new DoubleAnimation(0, 360, TimeSpan.FromMilliseconds(500));
                var spinOutFade = new DoubleAnimation(1.0, 0.2, TimeSpan.FromMilliseconds(250));
                var spinInFade = new DoubleAnimation(0.2, 1.0, TimeSpan.FromMilliseconds(250)) { BeginTime = TimeSpan.FromMilliseconds(250) };
                Storyboard.SetTarget(spinAnim, previewImage);
                Storyboard.SetTarget(spinOutFade, previewImage);
                Storyboard.SetTarget(spinInFade, previewImage);
                Storyboard.SetTargetProperty(spinAnim, new PropertyPath("(UIElement.RenderTransform).(RotateTransform.Angle)"));
                Storyboard.SetTargetProperty(spinOutFade, new PropertyPath(UIElement.OpacityProperty));
                Storyboard.SetTargetProperty(spinInFade, new PropertyPath(UIElement.OpacityProperty));
                sb.Children.Add(spinAnim);
                sb.Children.Add(spinOutFade);
                sb.Children.Add(spinInFade);
                break;

            default: // "none"
                return;
        }

        sb.Begin();
    }

    private Image? FindPreviewImage(DependencyObject parent)
    {
        // Find the first Image inside the preview area that has a Source (the scene preview)
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

    private void EditorView_Loaded(object sender, RoutedEventArgs e)
    {
        DrawTimeRuler();
        // Audio might not have loaded during DataContextChanged if the view wasn't in the visual tree yet
        if (DataContext is EditorViewModel vm)
        {
            TryLoadAudio(vm);
        }
    }

    private NAudio.Wave.WaveOutEvent? _waveOut;
    private NAudio.Wave.AudioFileReader? _audioFile;
    private bool _audioLoaded = false;

    private void TryLoadAudio(EditorViewModel vm)
    {
        if (_audioLoaded) return;
        if (string.IsNullOrEmpty(vm.AudioFilePath)) return;
        
        var fullPath = System.IO.Path.GetFullPath(vm.AudioFilePath);
        if (!System.IO.File.Exists(fullPath))
        {
            vm.StatusMessage = "🎵 Audio file not found on disk.";
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
            vm.ShowTemporaryStatus($"🎵 NAudio Engine loaded (Duration: {_audioFile.TotalTime.TotalSeconds:F1}s)");
        }
        catch (Exception ex)
        {
            vm.StatusMessage = $"🎵 NAudio Load Error: {ex.Message}";
            _audioLoaded = false;
        }

        // Cleanup when the view is closed
        this.Unloaded += (s, e) => DisposeAudio();
    }

    private void DisposeAudio()
    {
        if (_waveOut != null)
        {
            _waveOut.Stop();
            _waveOut.Dispose();
            _waveOut = null;
        }
        if (_audioFile != null)
        {
            _audioFile.Dispose();
            _audioFile = null;
        }
        _audioLoaded = false;
    }

    private void Vm_PropertyChangedForAudio(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(EditorViewModel.AudioVolume) && sender is EditorViewModel vm)
        {
            if (_audioFile != null)
                _audioFile.Volume = (float)vm.AudioVolume;
        }
    }

    private void Vm_AudioPlaybackChanged(object? sender, string state)
    {
        if (_waveOut == null || _audioFile == null) return;
        
        if (state == "play")
        {
            if (sender is EditorViewModel vm)
                _audioFile.Volume = (float)vm.AudioVolume;
            _waveOut.Play();
        }
        else
        {
            _waveOut.Pause();
        }
    }

    private void Vm_AudioSeeked(object? sender, double timeInSeconds)
    {
        if (_audioFile == null) return;
        
        var newPosition = TimeSpan.FromSeconds(timeInSeconds);
        if (newPosition <= _audioFile.TotalTime)
        {
            _audioFile.CurrentTime = newPosition;
        }
    }

    private void TimelineScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        // Sync audio track scroll
        if (AudioScrollViewer != null)
        {
            AudioScrollViewer.ScrollToHorizontalOffset(e.HorizontalOffset);
        }
        
        DrawTimeRuler();
    }

    private void DrawTimeRuler()
    {
        // Keep PlayheadGroup if it exists, otherwise clear everything
        UIElement? playhead = null;
        foreach (UIElement child in TimeRulerCanvas.Children)
        {
            // We know the playhead isn't in TimeRulerCanvas anymore (moved to TrackGrid), but just in case
            if (child is Canvas c && c.Name == "PlayheadGroup") playhead = child;
        }

        TimeRulerCanvas.Children.Clear();
        if (playhead != null) TimeRulerCanvas.Children.Add(playhead);

        var vm = DataContext as EditorViewModel;
        if (vm == null || vm.Scenes.Count == 0) return;

        double totalDuration = 0;
        foreach (var scene in vm.Scenes)
            totalDuration += scene.Duration;

        if (totalDuration <= 0) return;

        double canvasWidth = TimeRulerCanvas.ActualWidth;
        if (canvasWidth <= 0) canvasWidth = 800;

        double totalTrackWidth = 0;
        double zoom = vm.ZoomScale;
        if (zoom <= 0) zoom = 1.0;
        foreach (var scene in vm.Scenes)
        {
            double w = scene.Duration * 8 * zoom; // matches DurationAndZoomMultiConverter
            if (w < 60) w = 60;
            if (w > 1000) w = 1000;
            totalTrackWidth += w + 26; // +26 for transition arrow & margin
        }

        double scrollOffset = TimelineScrollViewer?.HorizontalOffset ?? 0;
        double pixelsPerSecond = totalTrackWidth / totalDuration;

        // Dynamic intervals based on pixels available
        double maxTicksOnScreen = Math.Max(10, canvasWidth / 70); // spaced ~70px apart
        double idealTickInterval = (canvasWidth / pixelsPerSecond) / maxTicksOnScreen;
        
        double[] niceIntervals = { 0.1, 0.25, 0.5, 1, 2, 5, 10, 15, 30, 60, 120, 300, 600 };
        double tickIntervalSec = 5; // default
        foreach (var interval in niceIntervals)
        {
            if (interval >= idealTickInterval)
            {
                tickIntervalSec = interval;
                break;
            }
        }

        // Pass info to ViewModel for playhead positioning
        vm.UpdateTimelineLayoutMetrics(totalTrackWidth, pixelsPerSecond, scrollOffset);

        // Update Audio Track Width
        if (AudioTrackBackground != null && AudioClipBlock != null)
        {
            AudioTrackBackground.Width = Math.Max(0, totalTrackWidth);
            AudioClipBlock.Width = Math.Max(0, totalTrackWidth);
        }

        for (double t = 0; t <= totalDuration; t += tickIntervalSec)
        {
            // Floating point precision fix to avoid 0.3000000004
            t = Math.Round(t, 2);

            double x = (t * pixelsPerSecond) - scrollOffset + 60; // +60 offset for left labels
            if (x < 0 || x > canvasWidth + 40) continue;

            // Tick line
            var line = new System.Windows.Shapes.Line
            {
                X1 = x, X2 = x,
                Y1 = 18, Y2 = 28,
                Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#374151")),
                StrokeThickness = 1.5
            };
            TimeRulerCanvas.Children.Add(line);

            // Time label
            int mins = (int)(t / 60);
            int secs = (int)(t % 60);
            int ms = (int)(Math.Round((t - Math.Floor(t)) * 100)); // up to 2 decimal places

            string timeText = ms > 0 ? $"{mins}:{secs:D2}.{ms:D2}" : $"{mins}:{secs:D2}";
            
            var label = new TextBlock
            {
                Text = timeText,
                FontSize = 9,
                FontWeight = FontWeights.SemiBold,
                Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B")),
            };
            
            // Adjust label pos
            Canvas.SetLeft(label, x - 12);
            Canvas.SetTop(label, 2);
            TimeRulerCanvas.Children.Add(label);

            // Add smaller sub-ticks
            double subTickInterval = tickIntervalSec / 5.0; // 5 sub-sections
            for (double st = t + subTickInterval; st < t + tickIntervalSec - (subTickInterval/2) && st <= totalDuration; st += subTickInterval)
            {
                double sx = (st * pixelsPerSecond) - scrollOffset + 60;
                if (sx < 0 || sx > canvasWidth + 40) continue;
                var subLine = new System.Windows.Shapes.Line
                {
                    X1 = sx, X2 = sx,
                    Y1 = 22, Y2 = 28,
                    Stroke = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1E293B")),
                    StrokeThickness = 1
                };
                TimeRulerCanvas.Children.Add(subLine);
            }
        }
    }

    private bool _isDraggingPlayhead;

    private void TimeRulerCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = true;
        TimeRulerCanvas.CaptureMouse();
        HandleSeek(e.GetPosition(TimeRulerCanvas).X);
    }

    private void TimeRulerCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPlayhead && e.LeftButton == MouseButtonState.Pressed)
        {
            HandleSeek(e.GetPosition(TimeRulerCanvas).X);
        }
    }

    private void TimeRulerCanvas_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = false;
        TimeRulerCanvas.ReleaseMouseCapture();
    }

    private void TrackGrid_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        // Don't seek if they clicked exactly on a scene block (let the block handle selection)
        // If they click empty space, we seek.
        if (e.OriginalSource is FrameworkElement fe && fe.DataContext is EchoForge.Core.DTOs.TimelineItemDto)
            return;

        _isDraggingPlayhead = true;
        TrackGrid.CaptureMouse();
        // Since scroll viewer shifts content, we need the position relative to the grid itself, but adjusting for 60px padding
        HandleSeek(e.GetPosition(TrackGrid).X - (TimelineScrollViewer?.HorizontalOffset ?? 0));
    }

    private void TrackGrid_MouseMove(object sender, MouseEventArgs e)
    {
        if (_isDraggingPlayhead && e.LeftButton == MouseButtonState.Pressed)
        {
            HandleSeek(e.GetPosition(TrackGrid).X - (TimelineScrollViewer?.HorizontalOffset ?? 0));
        }
    }

    private void TrackGrid_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        _isDraggingPlayhead = false;
        TrackGrid.ReleaseMouseCapture();
    }

    private void HandleSeek(double clientX)
    {
        if (DataContext is EditorViewModel vm)
        {
            // Remove the 60px left padding offset from the visual calculation
            double localX = clientX - 60 + (TimelineScrollViewer?.HorizontalOffset ?? 0);
            if (localX < 0) localX = 0;
            vm.SeekToPixelPosition(localX);
        }
    }

    // --- TIMELINE SCENE EDGE DRAGGING (TRIM) ---
    private bool _isResizingScene = false;
    private Point _resizeStartPoint;
    private double _resizeStartDuration;
    private EchoForge.Core.DTOs.TimelineItemDto? _resizingScene;

    private void SceneRightEdge_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.Tag is EchoForge.Core.DTOs.TimelineItemDto scene)
        {
            _isResizingScene = true;
            _resizingScene = scene;
            _resizeStartDuration = scene.Duration;
            _resizeStartPoint = e.GetPosition(TrackGrid);
            fe.CaptureMouse();
            e.Handled = true;
        }
    }

    private void SceneRightEdge_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_isResizingScene || _resizingScene == null) return;
        
        var vm = DataContext as EditorViewModel;
        if (vm == null) return;

        Point currentPos = e.GetPosition(TrackGrid);
        double deltaX = currentPos.X - _resizeStartPoint.X;
        
        // Zoom formula: pixelWidth = duration * 8 * zoomScale
        // deltaDuration = deltaX / (8 * zoomScale)
        double zoom = vm.ZoomScale > 0 ? vm.ZoomScale : 1.0;
        double deltaDuration = deltaX / (8.0 * zoom);
        
        double newDuration = _resizeStartDuration + deltaDuration;
        
        // Boundaries
        if (newDuration < 0.5) newDuration = 0.5; // absolute minimum duration
        if (newDuration > 120.0) newDuration = 120.0; // absolute maximum per scene
        
        // Snap to 0.1s increments for neatness visually
        newDuration = Math.Round(newDuration * 10) / 10.0;
        
        // Only update if changed
        if (Math.Abs(_resizingScene.Duration - newDuration) > 0.05)
        {
            _resizingScene.Duration = newDuration;
            
            // The Duration setter in TimelineItemDto fires PropertyChanged, 
            // so the binding in the UI auto-updates (Width + InfoBar).
            // Just refresh the ruler.
            DrawTimeRuler();
        }
    }

    private void SceneRightEdge_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isResizingScene && sender is FrameworkElement fe)
        {
            _isResizingScene = false;
            _resizingScene = null;
            fe.ReleaseMouseCapture();
            e.Handled = true;
            
            // After release, recalculate the entire timeline duration for the UI
            var vm = DataContext as EditorViewModel;
            if (vm != null)
            {
                vm.UpdateTotalDuration(); 
            }
        }
    }
}
