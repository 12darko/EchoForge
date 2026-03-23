using System.Diagnostics;
using EchoForge.Core.DTOs;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;
using NAudio.Wave;
using System.Linq;

namespace EchoForge.Infrastructure.Services.Audio;

public class AudioAnalysisService : IAudioService
{
    private readonly ILogger<AudioAnalysisService> _logger;
    private readonly string _ffmpegPath;

    public AudioAnalysisService(ILogger<AudioAnalysisService> logger, string? ffmpegPath = null)
    {
        _logger = logger;
        _ffmpegPath = ffmpegPath ?? "ffmpeg";

        // Auto-detect if ffmpeg is in the app directory or tools directory
        if (_ffmpegPath == "ffmpeg")
        {
            var localPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ffmpeg.exe");
            var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Tools", "ffmpeg", "ffmpeg.exe");
            
            if (File.Exists(toolsPath)) _ffmpegPath = toolsPath;
            else if (File.Exists(localPath)) _ffmpegPath = localPath;
        }
    }

    public async Task<string> ConvertToWavAsync(string inputPath, string? ffmpegPath = null, CancellationToken cancellationToken = default)
    {
        var outputPath = Path.ChangeExtension(inputPath, ".wav");
        var exePath = string.IsNullOrWhiteSpace(ffmpegPath) || ffmpegPath == "ffmpeg" 
            ? _ffmpegPath 
            : ffmpegPath;

        var args = $"-i \"{inputPath}\" -ar 44100 -ac 1 -y \"{outputPath}\"";
        await RunFfmpegAsync(exePath, args, cancellationToken);

        _logger.LogInformation("Converted {Input} to WAV: {Output}", inputPath, outputPath);
        return outputPath;
    }

    public async Task<AudioAnalysisResult> AnalyzeAsync(string audioFilePath, string? ffmpegPath = null, CancellationToken cancellationToken = default)
    {
        // If the passed ffmpegPath is empty or just "ffmpeg", prefer the auto-detected _ffmpegPath
        var currentFfmpegPath = string.IsNullOrWhiteSpace(ffmpegPath) || ffmpegPath == "ffmpeg" 
            ? _ffmpegPath 
            : ffmpegPath;
        _logger.LogInformation("Analyzing audio: {Path} using {Ffmpeg}", audioFilePath, currentFfmpegPath);

        // Get duration via ffprobe
        var duration = await GetDurationAsync(audioFilePath, currentFfmpegPath, cancellationToken);

        // Convert to WAV for BPM analysis
        var wavPath = await ConvertToWavAsync(audioFilePath, currentFfmpegPath, cancellationToken);
        
        // Detect BPM using energy-based analysis
        var bpm = await DetectBpmAsync(wavPath, currentFfmpegPath, cancellationToken);

        // Calculate scenes based on BPM and duration
        var beatInterval = 60.0 / bpm;
        var beatsPerScene = 4; // 4 beats per scene (1 bar)
        var sceneDuration = beatInterval * beatsPerScene;
        var sceneCount = Math.Max(1, (int)Math.Floor(duration / sceneDuration));

        // Cap scenes for shorts
        if (sceneCount > 40) sceneCount = 40;

        var result = new AudioAnalysisResult
        {
            Duration = Math.Round(duration, 2),
            BPM = Math.Round(bpm, 1),
            SceneCount = sceneCount,
            SceneDuration = Math.Round(sceneDuration, 2)
        };

        _logger.LogInformation("Audio analysis complete: BPM={BPM}, Duration={Duration}s, Scenes={Scenes}",
            result.BPM, result.Duration, result.SceneCount);

        // Cleanup temp WAV
        try { File.Delete(wavPath); } catch { /* ignore */ }

        return result;
    }

    private async Task<double> GetDurationAsync(string filePath, string ffmpegPath, CancellationToken cancellationToken)
    {
        // Assume ffprobe is in same dir as ffmpeg or just "ffprobe" if ffmpeg is "ffmpeg"
        var ffprobePath = "ffprobe";
        if (!string.IsNullOrWhiteSpace(ffmpegPath) && ffmpegPath != "ffmpeg")
        {
             var dir = Path.GetDirectoryName(ffmpegPath);
             var ext = Path.GetExtension(ffmpegPath); // .exe
             if (!string.IsNullOrEmpty(dir)) 
                 ffprobePath = Path.Combine(dir, "ffprobe" + ext);
        }

        var args = $"-v error -show_entries format=duration -of default=noprint_wrappers=1:nokey=1 \"{filePath}\"";
        
        var output = await RunProcessAsync(ffprobePath, args, cancellationToken);
        if (double.TryParse(output.Trim(), System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var duration))
        {
            return duration;
        }

        throw new InvalidOperationException($"Could not determine duration for: {filePath}");
    }

    public async Task<string> ExtractBestPartAsync(string inputPath, int durationSec = 60, string? ffmpegPath = null, CancellationToken cancellationToken = default)
    {
        var exePath = string.IsNullOrWhiteSpace(ffmpegPath) || ffmpegPath == "ffmpeg" ? _ffmpegPath : ffmpegPath;

        // 1. Get total duration
        var totalDuration = await GetDurationAsync(inputPath, exePath, cancellationToken);
        if (totalDuration <= durationSec)
        {
            _logger.LogInformation("Audio is shorter than/equal to target {Duration}s, skipping crop", durationSec);
            return inputPath; // no need to crop
        }

        // 2. Convert to WAV temporarily for energy analysis
        var wavPath = await ConvertToWavAsync(inputPath, exePath, cancellationToken);

        // 3. Find highest energy window
        int bestStartSec = 0;
        try
        {
            using var reader = new WaveFileReader(wavPath);
            var sampleRate = reader.WaveFormat.SampleRate;
            var provider = reader.ToSampleProvider();
            var readBuffer = new float[sampleRate]; // 1 second chunks
            
            var energies = new List<double>();
            int samplesRead;
            while ((samplesRead = provider.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                energies.Add(readBuffer.Take(samplesRead).Sum(s => s * s));
            }

            double maxEnergy = 0;
            for (int i = 0; i <= energies.Count - durationSec; i++)
            {
                var windowEnergy = energies.Skip(i).Take(durationSec).Sum();
                if (windowEnergy > maxEnergy)
                {
                    maxEnergy = windowEnergy;
                    bestStartSec = i;
                }
            }
            _logger.LogInformation("Found best audio highlight starting at {Start}s", bestStartSec);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to analyze audio energy for smart crop. Trimming blindly.");
        }
        finally
        {
            try { File.Delete(wavPath); } catch { }
        }

        // 4. Trim the original audio
        var dir = Path.GetDirectoryName(inputPath) ?? "";
        var name = Path.GetFileNameWithoutExtension(inputPath);
        var ext = Path.GetExtension(inputPath);
        var outputPath = Path.Combine(dir, $"{name}_short{ext}");

        var trimArgs = $"-ss {bestStartSec} -t {durationSec} -i \"{inputPath}\" -c copy -y \"{outputPath}\"";
        
        // If copy fails (sometimes imprecise), could fallback to re-encoding.
        // Doing re-encoding by default for exact cut using target codec suitable for mp3:
        var encodeArgs = $"-y -ss {bestStartSec} -t {durationSec} -i \"{inputPath}\" -c:a libmp3lame -b:a 192k \"{outputPath}\"";

        await RunFfmpegAsync(exePath, encodeArgs, cancellationToken);

        _logger.LogInformation("Cropped audio saved to {Output}", outputPath);
        return outputPath;
    }

    private async Task<double> DetectBpmAsync(string wavPath, string ffmpegPath, CancellationToken cancellationToken)
    {
        // Use FFmpeg's energy-based BPM detection via ebur128 filter
        // Fallback to default BPM if detection fails
        try
        {
            var args = $"-i \"{wavPath}\" -af \"aresample=44100,lowpass=f=150,highpass=f=40\" -f null -";
            await RunFfmpegAsync(ffmpegPath, args, cancellationToken);

            // Simple energy-based BPM estimation using onset detection
            // For production: integrate a dedicated BPM library
            // Default to common BPM ranges for different genres
            var estimatedBpm = EstimateBpmFromAudio(wavPath);
            return estimatedBpm > 0 ? estimatedBpm : 120.0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "BPM detection failed, using default 120 BPM");
            return 120.0;
        }
    }

    private double EstimateBpmFromAudio(string wavPath)
    {
        try
        {
            using var reader = new NAudio.Wave.WaveFileReader(wavPath);
            var sampleRate = reader.WaveFormat.SampleRate;
            var totalSamples = (int)(reader.Length / (reader.WaveFormat.BitsPerSample / 8));
            var buffer = new float[totalSamples / reader.WaveFormat.Channels];

            var provider = reader.ToSampleProvider();
            var readBuffer = new float[sampleRate]; // 1 second chunks

            // Energy-based onset detection
            var energies = new List<double>();
            int samplesRead;
            while ((samplesRead = provider.Read(readBuffer, 0, readBuffer.Length)) > 0)
            {
                var energy = readBuffer.Take(samplesRead).Sum(s => s * s);
                energies.Add(energy);
            }

            if (energies.Count < 4) return 120.0;

            // Find peaks in energy (onsets)
            var peaks = new List<int>();
            var avgEnergy = energies.Average();
            for (int i = 1; i < energies.Count - 1; i++)
            {
                if (energies[i] > avgEnergy * 1.3 &&
                    energies[i] > energies[i - 1] &&
                    energies[i] > energies[i + 1])
                {
                    peaks.Add(i);
                }
            }

            if (peaks.Count < 2) return 120.0;

            // Calculate average interval between peaks
            var intervals = new List<double>();
            for (int i = 1; i < peaks.Count; i++)
            {
                intervals.Add(peaks[i] - peaks[i - 1]);
            }

            var avgInterval = intervals.Average(); // in seconds
            var bpm = 60.0 / avgInterval;

            // Clamp to reasonable range
            while (bpm < 60) bpm *= 2;
            while (bpm > 200) bpm /= 2;

            return Math.Round(bpm, 1);
        }
        catch (Exception)
        {
            return 120.0;
        }
    }

    private async Task<string> RunFfmpegAsync(string exePath, string args, CancellationToken cancellationToken)
    {
        return await RunProcessAsync(exePath, args, cancellationToken);
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

        return string.IsNullOrEmpty(output) ? error : output;
    }
}
