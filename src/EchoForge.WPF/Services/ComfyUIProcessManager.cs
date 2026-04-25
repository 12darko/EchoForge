using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EchoForge.WPF.Services;

/// <summary>
/// Manages the ComfyUI process lifecycle: starts it hidden when the app opens,
/// monitors health, and kills it when the app closes.
/// </summary>
public class ComfyUIProcessManager : IDisposable
{
    private Process? _comfyProcess;
    private readonly ILogger<ComfyUIProcessManager> _logger;
    private readonly HttpClient _healthClient;
    private CancellationTokenSource? _healthCts;
    private string _comfyUIPath = "";
    private int _port = 8188;

    public bool IsRunning => _comfyProcess != null && !_comfyProcess.HasExited;
    public string BaseUrl => $"http://127.0.0.1:{_port}";
    public bool IsReady { get; private set; }

    public ComfyUIProcessManager(ILogger<ComfyUIProcessManager>? logger = null)
    {
        _logger = logger ?? NullLogger<ComfyUIProcessManager>.Instance;
        _healthClient = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
    }

    /// <summary>
    /// Starts ComfyUI as a hidden background process.
    /// </summary>
    public async Task<bool> StartAsync(string comfyUIInstallPath, int port = 8188, CancellationToken cancellationToken = default)
    {
        _comfyUIPath = comfyUIInstallPath;
        _port = port;

        if (IsRunning)
        {
            _logger.LogInformation("ComfyUI is already running.");
            return true;
        }

        // Find the ComfyUI executable
        string exePath = FindComfyUIExecutable(comfyUIInstallPath);
        if (string.IsNullOrEmpty(exePath))
        {
            _logger.LogError("ComfyUI executable not found in: {Path}", comfyUIInstallPath);
            return false;
        }

        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exePath,
                WorkingDirectory = comfyUIInstallPath,
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                WindowStyle = ProcessWindowStyle.Hidden
            };

            // Always use python_embeded directly to prevent browser auto-launch
            var pythonExe = Path.Combine(comfyUIInstallPath, "python_embeded", "python.exe");
            var mainPy = Path.Combine(comfyUIInstallPath, "ComfyUI", "main.py");

            if (File.Exists(pythonExe) && File.Exists(mainPy))
            {
                startInfo.FileName = pythonExe;
                startInfo.Arguments = $"-s \"{mainPy}\" --listen 127.0.0.1 --port {_port} --preview-method none --disable-auto-launch";
            }
            else if (exePath.EndsWith(".bat", StringComparison.OrdinalIgnoreCase))
            {
                // Fallback to bat file but add --disable-auto-launch
                startInfo.FileName = "cmd.exe";
                startInfo.Arguments = $"/c \"{exePath}\" --listen 127.0.0.1 --port {_port} --preview-method none --disable-auto-launch";
            }
            else
            {
                startInfo.Arguments = $"--listen 127.0.0.1 --port {_port} --preview-method none --disable-auto-launch";
            }

            _logger.LogInformation("Starting ComfyUI: {Exe} {Args}", startInfo.FileName, startInfo.Arguments);

            _comfyProcess = Process.Start(startInfo);
            if (_comfyProcess == null)
            {
                _logger.LogError("Failed to start ComfyUI process.");
                return false;
            }

            // Consume stdout and stderr to prevent deadlocks
            _comfyProcess.OutputDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogInformation("ComfyUI: {output}", e.Data);
            };
            
            _comfyProcess.ErrorDataReceived += (sender, e) => {
                if (!string.IsNullOrEmpty(e.Data))
                    _logger.LogWarning("ComfyUI Err: {err}", e.Data);
            };
            
            _comfyProcess.BeginOutputReadLine();
            _comfyProcess.BeginErrorReadLine();

            // Wait for ComfyUI to become ready (max 120 seconds)
            IsReady = await WaitForReadyAsync(120, cancellationToken);
            
            if (IsReady)
            {
                _logger.LogInformation("ComfyUI is ready and listening on port {Port}", _port);
                StartHealthMonitor();
            }
            else
            {
                _logger.LogWarning("ComfyUI started but did not become ready within timeout.");
            }

            return IsReady;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start ComfyUI.");
            return false;
        }
    }

    /// <summary>
    /// Stops the ComfyUI process gracefully, then forcefully if needed.
    /// </summary>
    public async Task StopAsync()
    {
        _healthCts?.Cancel();
        IsReady = false;

        if (_comfyProcess == null || _comfyProcess.HasExited)
        {
            _logger.LogInformation("ComfyUI is not running.");
            return;
        }

        try
        {
            _logger.LogInformation("Stopping ComfyUI...");
            
            // Try graceful shutdown first
            _comfyProcess.CloseMainWindow();
            
            // Wait 5 seconds for graceful exit
            if (!_comfyProcess.WaitForExit(5000))
            {
                _logger.LogWarning("ComfyUI did not exit gracefully, killing process.");
                _comfyProcess.Kill(true); // Kill entire process tree
            }

            _logger.LogInformation("ComfyUI stopped.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping ComfyUI.");
            try { _comfyProcess?.Kill(true); } catch { }
        }
        finally
        {
            _comfyProcess?.Dispose();
            _comfyProcess = null;
        }
    }

    /// <summary>
    /// Checks if ComfyUI API is responding.
    /// </summary>
    public async Task<bool> HealthCheckAsync()
    {
        try
        {
            var response = await _healthClient.GetAsync($"{BaseUrl}/system_stats");
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private async Task<bool> WaitForReadyAsync(int timeoutSeconds, CancellationToken cancellationToken)
    {
        var deadline = DateTime.UtcNow.AddSeconds(timeoutSeconds);
        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            if (await HealthCheckAsync())
                return true;

            await Task.Delay(2000, cancellationToken);
        }
        return false;
    }

    private void StartHealthMonitor()
    {
        _healthCts = new CancellationTokenSource();
        var token = _healthCts.Token;

        _ = Task.Run(async () =>
        {
            while (!token.IsCancellationRequested)
            {
                await Task.Delay(30000, token); // Check every 30 seconds
                if (!await HealthCheckAsync())
                {
                    _logger.LogWarning("ComfyUI health check failed!");
                    IsReady = false;
                }
                else
                {
                    IsReady = true;
                }
            }
        }, token);
    }

    private string FindComfyUIExecutable(string installPath)
    {
        // Priority order for finding ComfyUI
        string[] candidates = new[]
        {
            Path.Combine(installPath, "run_nvidia_gpu.bat"),
            Path.Combine(installPath, "run_cpu.bat"),
            Path.Combine(installPath, "main.py"),
            Path.Combine(installPath, "ComfyUI", "main.py"),
            Path.Combine(installPath, "python_embeded", "python.exe"),
        };

        foreach (var candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                _logger.LogInformation("Found ComfyUI executable: {Path}", candidate);
                
                // If it's main.py, we need to find the python executable
                if (candidate.EndsWith("main.py"))
                {
                    var pythonEmbed = Path.Combine(installPath, "python_embeded", "python.exe");
                    if (File.Exists(pythonEmbed))
                    {
                        // Return a special format that StartAsync will handle
                        return candidate; // Will be handled via .bat detection
                    }
                }
                
                return candidate;
            }
        }

        // Search recursively for run_nvidia_gpu.bat or main.py
        try
        {
            var batFiles = Directory.GetFiles(installPath, "run_nvidia_gpu.bat", SearchOption.TopDirectoryOnly);
            if (batFiles.Length > 0) return batFiles[0];
            
            var mainPy = Directory.GetFiles(installPath, "main.py", SearchOption.AllDirectories);
            if (mainPy.Length > 0) return mainPy[0];
        }
        catch { }

        return "";
    }

    public void Dispose()
    {
        _healthCts?.Cancel();
        _healthCts?.Dispose();
        
        try
        {
            if (_comfyProcess != null && !_comfyProcess.HasExited)
            {
                _comfyProcess.Kill(true);
            }
        }
        catch { }
        
        _comfyProcess?.Dispose();
        _healthClient?.Dispose();
    }
}
