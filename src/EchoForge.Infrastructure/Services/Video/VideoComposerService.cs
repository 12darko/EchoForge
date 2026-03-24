using System.Diagnostics;
using System.Globalization;
using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;

namespace EchoForge.Infrastructure.Services.Video;

public class VideoComposerService : IVideoComposerService
{
    private readonly ILogger<VideoComposerService> _logger;
    private readonly string _ffmpegPath;
    private readonly string _outputDir;

    public VideoComposerService(ILogger<VideoComposerService> logger, string? ffmpegPath = null, string? outputDir = null)
    {
        _logger = logger;
        _ffmpegPath = !string.IsNullOrWhiteSpace(ffmpegPath) ? ffmpegPath : "ffmpeg";
        
        if (_ffmpegPath == "ffmpeg")
        {
             var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe");
             if (File.Exists(toolsPath)) _ffmpegPath = toolsPath;
        }

        _outputDir = !string.IsNullOrWhiteSpace(outputDir) ? outputDir : Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
        Directory.CreateDirectory(_outputDir);
    }

    public async Task<VideoCompositionResult> ComposeVideoAsync(
        List<string> imagePaths,
        string audioPath,
        VideoRenderSettings settings,
        string transition,
        string? visualEffect = null,
        string? overlayText = null,
        string? outputDirectory = null,
        string? introVideoPath = null,
        string? outroVideoPath = null,
        Action<int>? progressCallback = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting video composition: {ImageCount} images, {Width}x{Height}",
            imagePaths.Count, settings.Width, settings.Height);

        var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var targetDir = !string.IsNullOrWhiteSpace(outputDirectory) ? outputDirectory : Path.Combine(myDocs, "EchoForge", "Publishing");
        Directory.CreateDirectory(targetDir);

        var outputPath = Path.Combine(targetDir, $"echoforge_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        
        var tempMainDir = Path.Combine(myDocs, "EchoForge", "Rendering");
        var tempDir = Path.Combine(tempMainDir, "temp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var audioDuration = await GetAudioDurationAsync(audioPath, cancellationToken);
            var effectiveDuration = Math.Min(audioDuration, settings.MaxDurationSeconds);
            var sceneDuration = effectiveDuration / imagePaths.Count;

            _logger.LogInformation("Audio: {Duration}s, {SceneCount} scenes × {SceneDuration}s each",
                effectiveDuration, imagePaths.Count, sceneDuration.ToString("F2", CultureInfo.InvariantCulture));

            // Generate TimelineJson
            var timelineItems = new List<TimelineItemDto>();
            for (int i = 0; i < imagePaths.Count; i++)
            {
                timelineItems.Add(new TimelineItemDto
                {
                    SceneNumber = i + 1,
                    Duration = sceneDuration,
                    ImagePath = imagePaths[i],
                    Transition = transition,
                    Prompt = "Auto-generated scene details" // can't easily fetch prompt here but we keep the visual
                });
            }
            var timelineJson = System.Text.Json.JsonSerializer.Serialize(timelineItems);

            bool hasVisualEffect = !string.IsNullOrWhiteSpace(visualEffect) && visualEffect != "none";

            if (!hasVisualEffect && (string.IsNullOrWhiteSpace(transition) || transition == "none" || imagePaths.Count == 1))
            {
                // Basic Concat approach (faster, simple cuts)
                var concatFilePath = Path.Combine(tempDir, "concat.txt");
                var concatLines = new List<string>();
                foreach (var imgPath in imagePaths)
                {
                    concatLines.Add($"file '{imgPath.Replace("\\", "/").Replace("'", "'\\''")}'");
                    concatLines.Add($"duration {sceneDuration.ToString("F4", CultureInfo.InvariantCulture)}");
                }
                if (imagePaths.Count > 0)
                {
                    concatLines.Add($"file '{imagePaths.Last().Replace("\\", "/").Replace("'", "'\\''")}'");
                }
                await File.WriteAllLinesAsync(concatFilePath, concatLines, cancellationToken);

                var durStr = effectiveDuration.ToString("F2", CultureInfo.InvariantCulture);
                var filter = $"scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=decrease,pad={settings.Width}:{settings.Height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={settings.FPS}";

                var args = $"-hwaccel auto -f concat -safe 0 -i \"{concatFilePath}\" -i \"{audioPath}\" -vf \"{filter}\" -c:v {settings.Codec} -preset fast -pix_fmt yuv420p -c:a aac -b:a 192k -t {durStr} -shortest -movflags +faststart -y \"{outputPath}\"";

                _logger.LogInformation("Building simple video: {Width}x{Height} @ {FPS}fps", settings.Width, settings.Height, settings.FPS);
                await RunFfmpegAsync(args, progressCallback, effectiveDuration, cancellationToken);
            }
            else
            {
                // Complex Filtergraph approach (XFade, Zoompan, VFX)
                outputPath = await ComposeVideoWithTransitionsAsync(imagePaths, audioPath, settings, transition, visualEffect, targetDir, tempDir, effectiveDuration, sceneDuration, progressCallback, cancellationToken);
            }

            if (!File.Exists(outputPath) || new FileInfo(outputPath).Length < 1000)
                throw new InvalidOperationException("FFmpeg produced no valid output file");

            outputPath = await AppendBrandingAsync(outputPath, introVideoPath, outroVideoPath, settings, targetDir, tempDir, cancellationToken);

            _logger.LogInformation("Video composition complete: {Path} ({Size} KB)", outputPath, new FileInfo(outputPath).Length / 1024);
            
            return new VideoCompositionResult
            {
                VideoFilePath = outputPath,
                TimelineJson = timelineJson
            };
        }
        finally
        {
            try { Directory.Delete(tempDir, true); } catch { }
        }
    }

    private async Task<string> AppendBrandingAsync(string mainVideoPath, string? introPath, string? outroPath, VideoRenderSettings settings, string targetDir, string tempDir, CancellationToken cancellationToken)
    {
        bool hasIntro = !string.IsNullOrWhiteSpace(introPath) && File.Exists(introPath);
        bool hasOutro = !string.IsNullOrWhiteSpace(outroPath) && File.Exists(outroPath);

        if (!hasIntro && !hasOutro)
            return mainVideoPath;

        var finalPath = Path.Combine(targetDir, $"echoforge_branded_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        
        var inputs = new System.Text.StringBuilder();
        var filter = new System.Text.StringBuilder();
        int inputIndex = 0;
        int concatCount = 0;

        void AddInput(string path)
        {
            inputs.Append($"-i \"{path}\" ");
            
            // Normalize video for concat: scale/pad to target width/height, set framerate, set format
            filter.Append($"[{inputIndex}:v]scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=decrease,pad={settings.Width}:{settings.Height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={settings.FPS},format=yuv420p[v{inputIndex}]; ");
            
            // Normalize audio for concat: 44.1kHz, stereo
            filter.Append($"[{inputIndex}:a]aformat=sample_fmts=fltp:sample_rates=44100:channel_layouts=stereo[a{inputIndex}]; ");
            
            inputIndex++;
            concatCount++;
        }

        if (hasIntro) AddInput(introPath!);
        AddInput(mainVideoPath);
        if (hasOutro) AddInput(outroPath!);

        for (int i = 0; i < concatCount; i++)
        {
            filter.Append($"[v{i}][a{i}]");
        }
        filter.Append($"concat=n={concatCount}:v=1:a=1[vout][aout]");

        var args = $"-hwaccel auto {inputs} -filter_complex \"{filter}\" -map \"[vout]\" -map \"[aout]\" -c:v {settings.Codec} -preset fast -pix_fmt yuv420p -c:a aac -b:a 192k -movflags +faststart -y \"{finalPath}\"";

        _logger.LogInformation("Applying Intro/Outro branding...");
        await RunFfmpegAsync(args, null, 0, cancellationToken);

        if (File.Exists(finalPath) && new FileInfo(finalPath).Length > 1000)
        {
            // Optionally delete old main file to save space
            try { File.Delete(mainVideoPath); } catch { }
            return finalPath;
        }

        _logger.LogWarning("Branding failed to create a valid file, returning unbranded video.");
        return mainVideoPath;
    }

    private async Task<string> ComposeVideoWithTransitionsAsync(List<string> imagePaths, string audioPath, VideoRenderSettings settings, string transition, string? visualEffect, string targetDir, string tempDir, double effectiveDuration, double sceneDuration, Action<int>? progressCallback, CancellationToken cancellationToken)
    {
        var outputPath = Path.Combine(targetDir, $"echoforge_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        
        // Transition settings
        double transitionDuration = sceneDuration <= 2.0 ? 0.5 : 1.0;
        double imageDuration = sceneDuration + transitionDuration;

        var sbInputs = new System.Text.StringBuilder();
        var sbFilter = new System.Text.StringBuilder();

        // 1. Inputs (Copy to temp with short names to prevent CLI max length limits)
        for (int i = 0; i < imagePaths.Count; i++)
        {
            var tempImgPath = Path.Combine(tempDir, $"{i}.jpg");
            File.Copy(imagePaths[i], tempImgPath, true);
            sbInputs.Append($"-loop 1 -t {imageDuration.ToString("F4", CultureInfo.InvariantCulture)} -i \"{tempImgPath}\" ");
        }

        // 2. Filtergraph: Scale + Zoompan + VFX (if needed)
        
        string vfxFilter = "";
        if (!string.IsNullOrWhiteSpace(visualEffect))
        {
            vfxFilter = visualEffect.ToLowerInvariant() switch
            {
                "bw" => ",colorchannelmixer=.3:.4:.3:0:.3:.4:.3:0:.3:.4:.3",
                "sepia" => ",colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131",
                "vhs" => ",rgbashift=rh=-3:bh=3,noise=c0s=11:c0f=t+u",
                "cinematic" => ",eq=contrast=1.2:saturation=1.1,vignette=PI/4",
                "dreamy" => ",gblur=sigma=3:steps=1:planes=1",
                _ => ""
            };
        }

        for (int i = 0; i < imagePaths.Count; i++)
        {
            sbFilter.Append($"[{i}:v]scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=increase,crop={settings.Width}:{settings.Height},setsar=1,fps={settings.FPS}");
            if (transition == "zoompan")
            {
                 sbFilter.Append($",zoompan=z='min(zoom+0.0015,1.5)':d={((int)(settings.FPS * imageDuration))}:s={settings.Width}x{settings.Height}");
            }
            sbFilter.Append($"{vfxFilter}[v{i}];\n");
        }

        // 3. Filtergraph: XFade
        string lastNode = "[v0]";
        string xfadeEffect = transition == "zoompan" ? "fade" : transition; // Map zoompan transition to standard fade
        
        for (int i = 1; i < imagePaths.Count; i++)
        {
            double offset = i * sceneDuration;
            sbFilter.Append($"{lastNode}[v{i}]xfade=transition={xfadeEffect}:duration={transitionDuration.ToString("F2", CultureInfo.InvariantCulture)}:offset={offset.ToString("F4", CultureInfo.InvariantCulture)}[f{i}];\n");
            lastNode = $"[f{i}]";
        }

        var filterScriptPath = Path.Combine(tempDir, "filter.txt");
        await File.WriteAllTextAsync(filterScriptPath, sbFilter.ToString(), cancellationToken);

        var argsBuilder = new System.Text.StringBuilder();
        argsBuilder.Append("-hwaccel auto ");
        argsBuilder.Append(sbInputs.ToString());
        argsBuilder.Append($"-i \"{audioPath}\" ");
        argsBuilder.Append($"-filter_complex_script \"{filterScriptPath}\" ");
        argsBuilder.Append($"-map \"{lastNode}\" -map {imagePaths.Count}:a "); // Final video node and original audio
        argsBuilder.Append($"-c:v {settings.Codec} -preset fast -pix_fmt yuv420p ");
        argsBuilder.Append($"-c:a aac -b:a 192k ");
        argsBuilder.Append($"-t {effectiveDuration.ToString("F2", CultureInfo.InvariantCulture)} -shortest ");
        argsBuilder.Append($"-movflags +faststart ");
        argsBuilder.Append($"-y \"{outputPath}\"");

        _logger.LogInformation("Building transition video ({Transition}): {Width}x{Height} @ {FPS}fps", transition, settings.Width, settings.Height, settings.FPS);
        await RunFfmpegAsync(argsBuilder.ToString(), progressCallback, effectiveDuration, cancellationToken);
        
        return outputPath;
    }

    private async Task<double> GetAudioDurationAsync(string audioPath, CancellationToken cancellationToken)
    {
        var ffprobePath = "ffprobe";
        if (_ffmpegPath.EndsWith("ffmpeg.exe", StringComparison.OrdinalIgnoreCase))
        {
            ffprobePath = _ffmpegPath.Substring(0, _ffmpegPath.Length - 10) + "ffprobe.exe";
        }
        else if (_ffmpegPath != "ffmpeg")
        {
             var dir = Path.GetDirectoryName(_ffmpegPath);
             var ext = Path.GetExtension(_ffmpegPath);
             if (!string.IsNullOrEmpty(dir)) 
                 ffprobePath = Path.Combine(dir, "ffprobe" + ext);
        }

        var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{audioPath}\"";

        var output = await RunProcessAsync(ffprobePath, args, cancellationToken);
        if (double.TryParse(output.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var duration))
            return duration;

        return 60.0; // fallback
    }

    private async Task<string> RunFfmpegAsync(string args, Action<int>? progressCallback = null, double totalDuration = 0, CancellationToken cancellationToken = default)
    {
        if (progressCallback != null && totalDuration > 0)
        {
            return await RunProcessWithProgressAsync(_ffmpegPath, args, progressCallback, totalDuration, cancellationToken);
        }
        return await RunProcessAsync(_ffmpegPath, args, cancellationToken);
    }

    private static async Task<string> RunProcessAsync(string fileName, string args, CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var errorBuffer = new System.Collections.Concurrent.ConcurrentQueue<string>();
        process.ErrorDataReceived += (s, e) =>
        {
            if (!string.IsNullOrEmpty(e.Data))
            {
                errorBuffer.Enqueue(e.Data);
                if (errorBuffer.Count > 30) errorBuffer.TryDequeue(out _);
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        
        await process.WaitForExitAsync(cancellationToken);
        
        var output = await outputTask;
        var error = string.Join(Environment.NewLine, errorBuffer);

        if (process.ExitCode != 0 && !string.IsNullOrEmpty(error))
        {
            throw new InvalidOperationException($"FFmpeg error: {error}");
        }

        return string.IsNullOrEmpty(output) ? error : output;
    }

    private static async Task<string> RunProcessWithProgressAsync(
        string fileName, 
        string args, 
        Action<int> progressCallback,
        double totalDurationSeconds,
        CancellationToken cancellationToken)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            }
        };

        var errorBuffer = new System.Collections.Concurrent.ConcurrentQueue<string>();

        process.ErrorDataReceived += (sender, e) =>
        {
            if (e.Data == null) return;
            
            errorBuffer.Enqueue(e.Data);
            if (errorBuffer.Count > 30) errorBuffer.TryDequeue(out _);
            
            // FFmpeg progress format: frame=  123 fps=... time=00:00:15.34 ...
            if (e.Data.Contains("time="))
            {
                var timePart = e.Data.Substring(e.Data.IndexOf("time=") + 5);
                var timeSpanStr = timePart.Split(' ')[0].Trim();

                if (TimeSpan.TryParse(timeSpanStr, CultureInfo.InvariantCulture, out var timeSpan))
                {
                    if (totalDurationSeconds > 0)
                    {
                        var progress = (int)((timeSpan.TotalSeconds / totalDurationSeconds) * 100);
                        progress = Math.Clamp(progress, 0, 100);
                        progressCallback(progress);
                    }
                }
            }
        };

        process.Start();
        process.BeginErrorReadLine();
        
        var outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);

        await process.WaitForExitAsync(cancellationToken);
        var output = await outputTask;

        if (process.ExitCode != 0)
        {
            var errorOutput = string.Join(Environment.NewLine, errorBuffer);
            throw new InvalidOperationException($"FFmpeg error: {errorOutput}");
        }
        
        // Ensure 100% on success
        progressCallback(100);

        return string.IsNullOrEmpty(output) ? string.Join(Environment.NewLine, errorBuffer) : output;
    }
}
