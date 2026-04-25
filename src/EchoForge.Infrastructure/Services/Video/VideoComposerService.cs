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
        
        try
        {
            Directory.CreateDirectory(_outputDir);
        }
        catch (Exception)
        {
            _outputDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            Directory.CreateDirectory(_outputDir);
        }
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
        CancellationToken cancellationToken = default,
        List<TimelineItemDto>? timelineItems = null,
        double audioFadeInDuration = 0,
        double audioFadeOutDuration = 0)
    {
        _logger.LogInformation("Starting video composition: {ImageCount} images, {Width}x{Height}",
            imagePaths.Count, settings.Width, settings.Height);

        var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var targetDir = !string.IsNullOrWhiteSpace(outputDirectory) ? outputDirectory : Path.Combine(myDocs, "EchoForge", "Publishing");
        try
        {
            Directory.CreateDirectory(targetDir);
        }
        catch (Exception)
        {
            targetDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "output");
            Directory.CreateDirectory(targetDir);
        }

        var outputPath = Path.Combine(targetDir, $"echoforge_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        
        var tempMainDir = Path.Combine(Path.GetTempPath(), "EchoForge_Rendering");
        var tempDir = Path.Combine(tempMainDir, "temp_" + Guid.NewGuid().ToString("N")[..8]);
        Directory.CreateDirectory(tempDir);

        try
        {
            var audioDuration = await GetAudioDurationAsync(audioPath, cancellationToken);
            var effectiveDuration = Math.Min(audioDuration, settings.MaxDurationSeconds);

            // Use per-scene durations from timeline if available
            bool hasTimeline = timelineItems != null && timelineItems.Count == imagePaths.Count;
            var sceneDurations = new List<double>();
            if (hasTimeline)
            {
                sceneDurations = timelineItems!.Select(t => t.Duration).ToList();
                // Clamp total to effective duration
                var totalTimelineDuration = sceneDurations.Sum();
                if (totalTimelineDuration > effectiveDuration)
                {
                    var ratio = effectiveDuration / totalTimelineDuration;
                    sceneDurations = sceneDurations.Select(d => d * ratio).ToList();
                }
                effectiveDuration = sceneDurations.Sum();
            }
            else
            {
                var uniformDuration = effectiveDuration / imagePaths.Count;
                sceneDurations = Enumerable.Repeat(uniformDuration, imagePaths.Count).ToList();
            }

            _logger.LogInformation("Audio: {Duration}s, {SceneCount} scenes, timeline={HasTimeline}",
                effectiveDuration, imagePaths.Count, hasTimeline);

            // Generate TimelineJson
            var resultTimelineItems = new List<TimelineItemDto>();
            for (int i = 0; i < imagePaths.Count; i++)
            {
                resultTimelineItems.Add(new TimelineItemDto
                {
                    SceneNumber = i + 1,
                    Duration = sceneDurations[i],
                    ImagePath = imagePaths[i],
                    Transition = hasTimeline ? timelineItems![i].Transition : transition,
                    TransitionDuration = hasTimeline ? timelineItems![i].TransitionDuration : null,
                    TransitionDirection = hasTimeline ? timelineItems![i].TransitionDirection : null,
                    Speed = hasTimeline ? timelineItems![i].Speed : 1.0,
                    FadeInDuration = hasTimeline ? timelineItems![i].FadeInDuration : 0,
                    FadeOutDuration = hasTimeline ? timelineItems![i].FadeOutDuration : 0,
                    Filter = hasTimeline ? timelineItems![i].Filter : "none",
                    Prompt = hasTimeline ? timelineItems![i].Prompt : "Auto-generated scene",
                    Brightness = hasTimeline ? timelineItems![i].Brightness : 0,
                    Contrast = hasTimeline ? timelineItems![i].Contrast : 1.0,
                    Saturation = hasTimeline ? timelineItems![i].Saturation : 1.0,
                    Temperature = hasTimeline ? timelineItems![i].Temperature : 6500,
                    Tint = hasTimeline ? timelineItems![i].Tint : 0
                });
            }
            var timelineJson = System.Text.Json.JsonSerializer.Serialize(resultTimelineItems);

            // Always use the advanced pipeline when we have timeline data
            bool useAdvancedPipeline = hasTimeline 
                || (!string.IsNullOrWhiteSpace(visualEffect) && visualEffect != "none")
                || (!string.IsNullOrWhiteSpace(transition) && transition != "none" && imagePaths.Count > 1);

            if (!useAdvancedPipeline && imagePaths.Count == 1)
            {
                // Single image basic approach
                var concatFilePath = Path.Combine(tempDir, "concat.txt");
                var concatLines = new List<string>();
                concatLines.Add($"file '{imagePaths[0].Replace("\\", "/").Replace("'", "'\\''")}'");
                concatLines.Add($"duration {effectiveDuration.ToString("F4", CultureInfo.InvariantCulture)}");
                concatLines.Add($"file '{imagePaths[0].Replace("\\", "/").Replace("'", "'\\''")}'");
                await File.WriteAllLinesAsync(concatFilePath, concatLines, cancellationToken);

                var durStr = effectiveDuration.ToString("F2", CultureInfo.InvariantCulture);
                var filter = $"scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=decrease,pad={settings.Width}:{settings.Height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={settings.FPS}";
                var args = $"-hwaccel auto -f concat -safe 0 -i \"{concatFilePath}\" -i \"{audioPath}\" -vf \"{filter}\" -c:v {settings.Codec} -preset fast -pix_fmt yuv420p -c:a aac -b:a 192k -t {durStr} -shortest -movflags +faststart -y \"{outputPath}\"";
                await RunFfmpegAsync(args, progressCallback, effectiveDuration, cancellationToken);
            }
            else
            {
                // Advanced pipeline with per-scene support
                outputPath = await ComposeVideoWithTimelineAsync(imagePaths, audioPath, settings, resultTimelineItems, visualEffect, targetDir, tempDir, effectiveDuration, sceneDurations, progressCallback, cancellationToken, audioFadeInDuration, audioFadeOutDuration);
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
            filter.Append($"[{inputIndex}:v]scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=decrease:flags=lanczos,pad={settings.Width}:{settings.Height}:(ow-iw)/2:(oh-ih)/2:color=black,setsar=1,fps={settings.FPS},format=yuv420p[v{inputIndex}]; ");
            
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

    private async Task<string> ComposeVideoWithTimelineAsync(
        List<string> imagePaths, string audioPath, VideoRenderSettings settings, 
        List<TimelineItemDto> timeline, string? visualEffect, 
        string targetDir, string tempDir, double effectiveDuration, List<double> sceneDurations,
        Action<int>? progressCallback, CancellationToken cancellationToken, double audioFadeInDuration, double audioFadeOutDuration)
    {
        var outputPath = Path.Combine(targetDir, $"echoforge_{DateTime.Now:yyyyMMdd_HHmmss}.mp4");
        
        var sbInputs = new System.Text.StringBuilder();
        var sbFilter = new System.Text.StringBuilder();

        // 1. Inputs — each image gets its own duration + transition overlap
        for (int i = 0; i < imagePaths.Count; i++)
        {
            var tempImgPath = Path.Combine(tempDir, $"{i}.jpg");
            File.Copy(imagePaths[i], tempImgPath, true);
            double transOverlap = (i < imagePaths.Count - 1) ? Math.Min(1.0, sceneDurations[i] * 0.3) : 0;
            double inputDuration = sceneDurations[i] + transOverlap;
            sbInputs.Append($"-loop 1 -t {inputDuration.ToString("F4", CultureInfo.InvariantCulture)} -i \"{tempImgPath}\" ");
        }

        // 2. Per-scene filtergraph: Scale + Speed + FadeIn/Out + Filter
        string globalVfx = "";
        if (!string.IsNullOrWhiteSpace(visualEffect))
        {
            globalVfx = visualEffect.ToLowerInvariant() switch
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
            var scene = timeline[i];
            var dur = sceneDurations[i];

            sbFilter.Append($"[{i}:v]scale={settings.Width}:{settings.Height}:force_original_aspect_ratio=increase:flags=lanczos,crop={settings.Width}:{settings.Height},setsar=1,fps={settings.FPS}");

            // Zoompan if transition is zoompan
            if (scene.Transition == "zoompan")
            {
                double transOverlap = (i < imagePaths.Count - 1) ? Math.Min(1.0, dur * 0.3) : 0;
                sbFilter.Append($",zoompan=z='min(zoom+0.0015,1.5)':d={((int)(settings.FPS * (dur + transOverlap)))}:s={settings.Width}x{settings.Height}");
            }

            // Speed: setpts=PTS/speed (speed > 1 = faster)
            if (Math.Abs(scene.Speed - 1.0) > 0.01 && scene.Speed > 0.1)
            {
                sbFilter.Append($",setpts=PTS/{scene.Speed.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            // Reverse playback
            if (scene.IsReversed)
            {
                sbFilter.Append($",reverse");
            }

            // ═══ Per-scene COLOR FILTER (15 filters matching sidebar) ═══
            var sceneFilter = scene.Filter?.ToLowerInvariant() ?? "none";
            var filterStr = sceneFilter switch
            {
                "grayscale" => ",colorchannelmixer=.3:.4:.3:0:.3:.4:.3:0:.3:.4:.3",
                "sepia" => ",colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131",
                "warm" => ",colortemperature=temperature=6500",
                "cool" => ",colortemperature=temperature=3500",
                "winter" => ",colortemperature=temperature=3200,eq=brightness=-0.02:saturation=0.85",
                "highcontrast" => ",eq=contrast=1.4:saturation=1.2",
                "vintage" => ",colorchannelmixer=.393:.769:.189:0:.349:.686:.168:0:.272:.534:.131,vignette=PI/4",
                "vivid" => ",eq=contrast=1.15:brightness=0.03:saturation=1.5",
                "dreamy" => ",gblur=sigma=3:steps=1,eq=brightness=0.06:saturation=1.2",
                "faded" => ",eq=contrast=0.8:brightness=0.08:saturation=0.6",
                "muted" => ",eq=saturation=0.4:contrast=0.9",
                "bwfilm" => ",colorchannelmixer=.3:.4:.3:0:.3:.4:.3:0:.3:.4:.3,noise=c0s=8:c0f=t+u,eq=contrast=1.15",
                "tealorange" => ",colorbalance=rs=0.15:gs=-0.05:bs=-0.15:rh=-0.1:gh=0.05:bh=0.2",
                "cinematic" or "cinema" => ",eq=contrast=1.2:saturation=1.1:brightness=-0.03,vignette=PI/4",
                "vignette" => ",vignette=PI/4",
                "blur" => ",gblur=sigma=5",
                "retro" => ",eq=contrast=1.1:saturation=0.7:brightness=0.05,colorbalance=rs=0.15:bh=-0.1,noise=c0s=6:c0f=t+u",
                "35mm" => ",noise=c0s=10:c0f=t+u,eq=contrast=1.15:saturation=0.9,vignette=PI/5",
                _ => ""
            };
            if (!string.IsNullOrEmpty(filterStr))
                sbFilter.Append(filterStr);

            // ═══ Per-scene VISUAL EFFECT (12 effects matching sidebar) ═══
            // Effects are applied as filtergraph elements that add motion/distortion
            var sceneEffect = scene.Filter?.ToLowerInvariant() ?? "none";
            // Check if the "Filter" field actually holds an effect name (from EffectCard_Click)
            var effectStr = sceneEffect switch
            {
                "flash" => "", // flash is a transition preview, no per-frame FFmpeg filter
                "pulse" => "", // pulse is a transition preview
                "spin" => "",  // spin is a transition preview
                "vhs" => ",rgbashift=rh=-3:bh=3,noise=c0s=11:c0f=t+u",
                "glitch" => ",rgbashift=rh=-5:bv=5,noise=c0s=15:c0f=t+u",
                "vaporwave" => ",eq=saturation=1.6:contrast=1.1,colorbalance=rs=0.3:gs=-0.1:bs=0.2",
                "chromatic" => ",rgbashift=rh=-2:bh=2:rv=1:bv=-1",
                "slowzoom" => "", // handled via zoompan separately
                "crashzoom" => "", // handled via zoompan separately
                "smoke" => ",gblur=sigma=2:steps=2",
                "sharpen" => ",unsharp=5:5:1.5",
                "denoise" => ",hqdn3d=4:3:6:4.5",
                _ => ""
            };
            // Only add effect if filter wasn't already applied (avoid double-applying)
            if (!string.IsNullOrEmpty(effectStr) && string.IsNullOrEmpty(filterStr))
                sbFilter.Append(effectStr);

            // ═══ Per-scene COLOR ADJUSTMENTS (Brightness, Contrast, Saturation, Temperature, Tint) ═══
            bool hasColorAdj = Math.Abs(scene.Brightness) > 0.001 || Math.Abs(scene.Contrast - 1.0) > 0.01 || Math.Abs(scene.Saturation - 1.0) > 0.01;
            if (hasColorAdj)
            {
                sbFilter.Append($",eq=brightness={scene.Brightness.ToString("F3", CultureInfo.InvariantCulture)}:contrast={scene.Contrast.ToString("F2", CultureInfo.InvariantCulture)}:saturation={scene.Saturation.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            // Temperature (default 6500K — only apply if changed)
            if (Math.Abs(scene.Temperature - 6500) > 50)
            {
                sbFilter.Append($",colortemperature=temperature={scene.Temperature.ToString("F0", CultureInfo.InvariantCulture)}");
            }

            // Tint (shifts green-magenta axis via colorbalance)
            if (Math.Abs(scene.Tint) > 0.01)
            {
                var tintG = (-scene.Tint * 0.3).ToString("F3", CultureInfo.InvariantCulture);
                var tintM = (scene.Tint * 0.3).ToString("F3", CultureInfo.InvariantCulture);
                sbFilter.Append($",colorbalance=gs={tintG}:gm={tintG}:gh={tintG}:rs={tintM}:rm={tintM}:rh={tintM}");
            }

            // ═══ Text Overlays ═══
            if (scene.TextOverlays != null && scene.TextOverlays.Any())
            {
                foreach (var overlay in scene.TextOverlays)
                {
                    if (string.IsNullOrWhiteSpace(overlay.Text)) continue;
                    
                    var escapedText = overlay.Text.Replace("'", "'\\''").Replace(":", "\\:");
                    var fontColorList = overlay.Color ?? "#FFFFFF";
                    if (fontColorList.StartsWith("#")) fontColorList = "0x" + fontColorList.Substring(1) + 
                        (overlay.Transparency < 100 ? ((int)(2.55 * overlay.Transparency)).ToString("X2") : "FF");

                    var fontSize = overlay.FontSize > 0 ? overlay.FontSize : 48;
                    
                    // ═══ XY Positioning & Animations ═══
                    var posXStr = overlay.PositionX.ToString("F3", CultureInfo.InvariantCulture);
                    var posYStr = overlay.PositionY.ToString("F3", CultureInfo.InvariantCulture);
                    
                    string xExpr = $"(w-text_w)*{posXStr}";
                    string yExpr = $"(h-text_h)*{posYStr}";
                    string alphaExpr = "";

                    var anim = overlay.Animation?.ToLowerInvariant() ?? "none";
                    if (anim == "fade")
                    {
                        alphaExpr = ":alpha='min(t\\,1)'";
                    }
                    else if (anim == "slide-in")
                    {
                        xExpr = $"(w-text_w)*{posXStr}-w*max(1-t\\,0)";
                    }
                    else if (anim == "typewriter")
                    {
                        // Reveal characters one-by-one over 2 seconds
                        alphaExpr = ":alpha='if(lt(t\\,0.1)\\,0\\,1)'";
                        // Use enable to simulate typewriter by showing text only after delay
                        // FFmpeg doesn't have native per-char reveal, so we use a rapid fade-in as approximation
                        alphaExpr = ":alpha='min(t*3\\,1)'";
                    }
                    else if (anim == "bounce")
                    {
                        // Vertical oscillation: bounce up/down using abs(sin) 
                        yExpr = $"(h-text_h)*{posYStr}-abs(sin(t*4))*50";
                    }

                    // Handle SRT / Subtitle StartTime & EndTime
                    string enableExpr = "";
                    if (overlay.StartTime.HasValue && overlay.EndTime.HasValue)
                    {
                        enableExpr = $"enable='between(t\\,{overlay.StartTime.Value.ToString("F2", CultureInfo.InvariantCulture)}\\,{overlay.EndTime.Value.ToString("F2", CultureInfo.InvariantCulture)})':";
                    }

                    string alignPos = $"x={xExpr}:y={yExpr}{alphaExpr}";
                    string textFilter = $"drawtext={enableExpr}text='{escapedText}':fontfile='C\\:/Windows/Fonts/arial.ttf':fontcolor={fontColorList}:fontsize={(int)fontSize}:{alignPos}";
                    
                    if (overlay.OutlineThickness > 0)
                    {
                        textFilter += $":borderw={(int)overlay.OutlineThickness}:bordercolor=black";
                    }
                    if (overlay.ShadowOpacity > 0)
                    {
                        var alpha = (int)(255 * overlay.ShadowOpacity);
                        textFilter += $":shadowx=4:shadowy=4:shadowcolor=0x000000{alpha:X2}";
                    }
                    
                    sbFilter.Append($",{textFilter}");
                }
            }

            // FadeIn / FadeOut per scene
            if (scene.FadeInDuration > 0)
            {
                sbFilter.Append($",fade=t=in:st=0:d={scene.FadeInDuration.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            if (scene.FadeOutDuration > 0)
            {
                var fadeOutStart = dur - scene.FadeOutDuration;
                if (fadeOutStart < 0) fadeOutStart = 0;
                sbFilter.Append($",fade=t=out:st={fadeOutStart.ToString("F2", CultureInfo.InvariantCulture)}:d={scene.FadeOutDuration.ToString("F2", CultureInfo.InvariantCulture)}");
            }

            sbFilter.Append($"{globalVfx}[v{i}];\n");
        }

        // 3. XFade transitions between scenes
        // FFmpeg xfade supported: fade, fadeblack, fadewhite, dissolve, 
        //   wipeleft, wiperight, wipeup, wipedown, slideleft, slideright,
        //   pixelize, circlecrop, horzopen, vertopen, diagtl, diagtr, diagbl, diagbr,
        //   smoothleft, smoothright, smoothup, smoothdown, ...
        string lastNode = "[v0]";
        double cumulativeOffset = 0;
        
        for (int i = 1; i < imagePaths.Count; i++)
        {
            cumulativeOffset += sceneDurations[i - 1];
            var scene = timeline[i];
            
            // Allow user-defined transition duration or default to 30% of previous scene (max 1.0s)
            double transitionDuration = scene.TransitionDuration ?? Math.Min(1.0, sceneDurations[i - 1] * 0.3);
            
            double offset = cumulativeOffset - transitionDuration;
            if (offset < 0) offset = 0;

            // Normalize transition name to valid FFmpeg xfade transition, considering direction
            string xfadeEffect = NormalizeXfadeTransition(scene.Transition, scene.TransitionDirection);

            sbFilter.Append($"{lastNode}[v{i}]xfade=transition={xfadeEffect}:duration={transitionDuration.ToString("F2", CultureInfo.InvariantCulture)}:offset={offset.ToString("F4", CultureInfo.InvariantCulture)}[f{i}];\n");
            lastNode = $"[f{i}]";
        }

        // 4. Handle global project audio fades
        string audioMapNode = $"{imagePaths.Count}:a";
        if (audioFadeInDuration > 0 || audioFadeOutDuration > 0)
        {
            var audioFilterStr = new System.Text.StringBuilder();
            audioFilterStr.Append($"[{imagePaths.Count}:a]");
            if (audioFadeInDuration > 0)
            {
                audioFilterStr.Append($"afade=t=in:st=0:d={audioFadeInDuration.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            if (audioFadeOutDuration > 0)
            {
                if (audioFadeInDuration > 0) audioFilterStr.Append(",");
                var outStart = effectiveDuration - audioFadeOutDuration;
                if (outStart < 0) outStart = 0;
                audioFilterStr.Append($"afade=t=out:st={outStart.ToString("F2", CultureInfo.InvariantCulture)}:d={audioFadeOutDuration.ToString("F2", CultureInfo.InvariantCulture)}");
            }
            audioFilterStr.Append("[aout];\n");
            sbFilter.Append(audioFilterStr.ToString());
            audioMapNode = "\"[aout]\"";
        }

        var filterScriptPath = Path.Combine(tempDir, "filter.txt");
        await File.WriteAllTextAsync(filterScriptPath, sbFilter.ToString(), cancellationToken);

        var argsBuilder = new System.Text.StringBuilder();
        argsBuilder.Append("-hwaccel auto ");
        argsBuilder.Append(sbInputs.ToString());
        argsBuilder.Append($"-i \"{audioPath}\" ");
        argsBuilder.Append($"-filter_complex_script \"{filterScriptPath}\" ");
        argsBuilder.Append($"-map \"{lastNode}\" -map {audioMapNode} ");
        argsBuilder.Append($"-c:v {settings.Codec} -preset fast -pix_fmt yuv420p ");
        argsBuilder.Append($"-c:a aac -b:a 192k ");
        argsBuilder.Append($"-t {effectiveDuration.ToString("F2", CultureInfo.InvariantCulture)} -shortest ");
        argsBuilder.Append($"-movflags +faststart ");
        argsBuilder.Append($"-y \"{outputPath}\"");

        _logger.LogInformation("Building timeline video: {Width}x{Height} @ {FPS}fps, {Scenes} scenes", settings.Width, settings.Height, settings.FPS, imagePaths.Count);
        await RunFfmpegAsync(argsBuilder.ToString(), progressCallback, effectiveDuration, cancellationToken);
        
        return outputPath;
    }

    /// <summary>
    /// Maps UI transition names to valid FFmpeg xfade transition names.
    /// FFmpeg supports: fade, fadeblack, fadewhite, dissolve, wipeleft, wiperight, wipeup, wipedown,
    /// slideleft, slideright, pixelize, circlecrop, horzopen, vertopen, diagtl, diagtr, diagbl, diagbr,
    /// smoothleft, smoothright, smoothup, smoothdown, radial, etc.
    /// </summary>
    private static string NormalizeXfadeTransition(string? transition, string? direction)
    {
        var t = transition?.ToLowerInvariant()?.Trim() ?? "none";
        var dir = direction?.ToLowerInvariant()?.Trim() ?? "";

        // Apply direction if it's a generic wipe or slide
        if (!string.IsNullOrEmpty(dir))
        {
            if (t == "wipe" || t == "slide" || t == "smooth")
            {
                if (dir == "left" || dir == "right" || dir == "up" || dir == "down")
                    return t + dir;
            }
        }

        return t switch
        {
            "none" or "" => "fade",
            "zoompan" => "fade",
            "crossfade" => "fade",
            "diamond" => "radial",          // diamond shape → radial is closest
            "shake" => "fade",              // no FFmpeg equivalent → fallback
            "glitch" => "pixelize",         // visual glitch → pixelize
            "flash" => "fade",              // flash → fade
            "pulse" => "fade",              // pulse → fade
            "spin" => "radial",             // spin → radial
            "blinds" => "horzopen",         // blinds → horizontal open
            "wipe" => "wipeleft",           // generic wipe → wipe left
            // Direct FFmpeg xfade names — pass through
            "fade" or "fadeblack" or "fadewhite" or "dissolve" => t,
            "wipeleft" or "wiperight" or "wipeup" or "wipedown" => t,
            "slideleft" or "slideright" => t,
            "pixelize" or "circlecrop" => t,
            "horzopen" or "vertopen" => t,
            "diagtl" or "diagtr" or "diagbl" or "diagbr" => t,
            "smoothleft" or "smoothright" or "smoothup" or "smoothdown" => t,
            "radial" => t,
            _ => "fade" // safe fallback
        };
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
