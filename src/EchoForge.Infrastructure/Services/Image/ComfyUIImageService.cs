using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using EchoForge.Core.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace EchoForge.Infrastructure.Services.Image;

/// <summary>
/// Image generation service that uses a locally running ComfyUI instance.
/// Sends SDXL workflow JSON to ComfyUI's REST API, waits for generation, and downloads the result.
/// Supports native HD (1920x1080) and 4K (3840x2160) generation.
/// </summary>
public class ComfyUIImageService : IImageGenerationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ComfyUIImageService> _logger;
    private readonly string _baseUrl;
    private readonly string _cacheDir;
    private readonly Random _random = new();
    private string? _detectedModelCache = null;

    private static readonly string[] PromptVariations =
    {
        "cinematic lighting, high detail, 8k quality",
        "dramatic atmosphere, ultra realistic, masterpiece",
        "volumetric fog, epic composition, photorealistic",
        "moody lighting, sharp focus, professional quality",
        "atmospheric perspective, stunning detail, award winning",
        "ray tracing, hyper detailed, concept art quality",
        "golden hour lighting, dreamlike, ethereal glow",
        "neon lights, cyberpunk mood, high contrast"
    };

    public ComfyUIImageService(HttpClient httpClient, ILogger<ComfyUIImageService>? logger = null, string baseUrl = "http://127.0.0.1:8188", string? cacheDir = null)
    {
        _httpClient = httpClient;
        _httpClient.Timeout = TimeSpan.FromMinutes(5);
        _logger = logger ?? NullLogger<ComfyUIImageService>.Instance;
        _baseUrl = baseUrl.TrimEnd('/');
        _cacheDir = cacheDir ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cache", "images");
        Directory.CreateDirectory(_cacheDir);
    }

    public async Task<List<string>> GenerateImagesAsync(string basePrompt, int count, int width, int height,
        string? model = null, int? maxUniqueImages = null, Action<int, string>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var uniqueCount = Math.Min(count, maxUniqueImages ?? 8);
        uniqueCount = Math.Clamp(uniqueCount, 1, 20);

        _logger.LogInformation("ComfyUI: Generating {Count} images at {W}x{H} with prompt: {Prompt}", uniqueCount, width, height, basePrompt);

        var generatedFiles = new List<string>();

        for (int i = 0; i < uniqueCount; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var variation = PromptVariations[i % PromptVariations.Length];
            var prompt = $"{basePrompt}, {variation}";
            var seed = _random.Next(1, 999999999);

            var imagePath = await GenerateSingleImageAsync(prompt, width, height, seed, model, progressCallback, cancellationToken);
            generatedFiles.Add(imagePath);

            _logger.LogInformation("ComfyUI: Generated image {Index}/{Total}: {Path}", i + 1, uniqueCount, imagePath);
        }

        // Distribute across requested count
        var result = new List<string>();
        for (int i = 0; i < count; i++)
        {
            result.Add(generatedFiles[i % generatedFiles.Count]);
        }

        return result;
    }

    public async Task<string> GenerateSingleImageAsync(string prompt, int width, int height, int? seed = null,
        string? model = null, Action<int, string>? progressCallback = null, CancellationToken cancellationToken = default)
    {
        var actualSeed = seed ?? _random.Next(1, 999999999);
        
        string effectiveModel = model ?? "sd_xl_base_1.0.safetensors";
        if (effectiveModel == "sd_xl_base_1.0.safetensors" || effectiveModel == "local" || effectiveModel == "comfyui")
        {
            // Auto-detect available local model if default SDXL is requested, since user might not have it installed
            if (_detectedModelCache == null)
            {
                try 
                {
                    var response = await _httpClient.GetStringAsync($"{_baseUrl}/object_info/CheckpointLoaderSimple", cancellationToken);
                    using var doc = JsonDocument.Parse(response);
                    var choices = doc.RootElement.GetProperty("CheckpointLoaderSimple").GetProperty("input").GetProperty("required").GetProperty("ckpt_name").EnumerateArray().ElementAt(0).EnumerateArray();
                    
                    var firstChoice = choices.FirstOrDefault();
                    if (firstChoice.ValueKind == JsonValueKind.String)
                        _detectedModelCache = firstChoice.GetString();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Could not fetch local ComfyUI models. Falling back to default.");
                }
            }
            if (!string.IsNullOrEmpty(_detectedModelCache))
            {
                effectiveModel = _detectedModelCache;
                _logger.LogInformation("ComfyUI Model auto-selected: {Model}", effectiveModel);
            }
        }

        // Check cache
        var cacheKey = $"comfyui_{prompt}_{width}x{height}_{actualSeed}_{effectiveModel}".GetHashCode().ToString("X8");
        var cachedPath = Path.Combine(_cacheDir, $"{cacheKey}.png");
        if (File.Exists(cachedPath))
        {
            _logger.LogDebug("ComfyUI cache hit: {Path}", cachedPath);
            return cachedPath;
        }

        // Build the SDXL workflow
        // For resolutions > 1024, we use a 2-pass approach:
        // Pass 1: Generate at optimal SDXL resolution (1024x576 for 16:9)
        // Pass 2: Upscale to target resolution using the model itself (HiRes Fix)
        bool needsHiResFix = width > 1024 || height > 1024;
        
        int baseWidth, baseHeight;
        if (needsHiResFix)
        {
            // Calculate optimal base resolution maintaining aspect ratio
            double aspect = (double)width / height;
            if (aspect >= 1.0)
            {
                baseWidth = 1024;
                baseHeight = (int)(1024 / aspect);
                baseHeight = baseHeight - (baseHeight % 8); // Must be divisible by 8
            }
            else
            {
                baseHeight = 1024;
                baseWidth = (int)(1024 * aspect);
                baseWidth = baseWidth - (baseWidth % 8);
            }
        }
        else
        {
            baseWidth = width - (width % 8);
            baseHeight = height - (height % 8);
        }

        var workflowJson = BuildWorkflow(prompt, baseWidth, baseHeight, width, height, actualSeed, effectiveModel, needsHiResFix);

        var clientId = Guid.NewGuid().ToString();
        using var ws = new System.Net.WebSockets.ClientWebSocket();
        
        try 
        {
            await ws.ConnectAsync(new Uri($"ws://127.0.0.1:8188/ws?clientId={clientId}"), cancellationToken);
            _logger.LogInformation("ComfyUI WS connected for prompt base={BW}x{BH}", baseWidth, baseHeight);
        } 
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "ComfyUI WS connect failed. Falling back to REST polling only.");
        }

        // Queue the prompt
        var promptId = await QueuePromptAsync(workflowJson, clientId, cancellationToken);
        if (string.IsNullOrEmpty(promptId))
        {
            throw new Exception("ComfyUI: Failed to queue prompt.");
        }

        _logger.LogInformation("ComfyUI: Prompt queued with ID: {Id}", promptId);

        // Wait for completion
        var outputInfo = await WaitForCompletionAsync(promptId, ws, progressCallback, cancellationToken);
        if (outputInfo == null)
        {
            throw new Exception("ComfyUI: Generation timed out or failed.");
        }

        // Download the generated image
        var imagePath = await DownloadOutputAsync(outputInfo.Value.filename, outputInfo.Value.subfolder, outputInfo.Value.type, cachedPath, cancellationToken);
        
        _logger.LogInformation("ComfyUI: Image saved: {Path}", imagePath);
        return imagePath;
    }

    private string BuildWorkflow(string prompt, int baseWidth, int baseHeight, int targetWidth, int targetHeight, int seed, string model, bool hiResFix)
    {
        string negativePrompt = "blurry, low quality, distorted, deformed, ugly, bad anatomy, watermark, text, signature";

        if (!hiResFix)
        {
            // Simple single-pass workflow
            var workflow = new JsonObject
            {
                ["3"] = new JsonObject  // KSampler
                {
                    ["class_type"] = "KSampler",
                    ["inputs"] = new JsonObject
                    {
                        ["model"] = new JsonArray { "4", 0 },
                        ["positive"] = new JsonArray { "6", 0 },
                        ["negative"] = new JsonArray { "7", 0 },
                        ["latent_image"] = new JsonArray { "5", 0 },
                        ["seed"] = seed,
                        ["steps"] = 25,
                        ["cfg"] = 7.0,
                        ["sampler_name"] = "euler_ancestral",
                        ["scheduler"] = "normal",
                        ["denoise"] = 1.0
                    }
                },
                ["4"] = new JsonObject  // CheckpointLoader
                {
                    ["class_type"] = "CheckpointLoaderSimple",
                    ["inputs"] = new JsonObject
                    {
                        ["ckpt_name"] = model
                    }
                },
                ["5"] = new JsonObject  // EmptyLatentImage
                {
                    ["class_type"] = "EmptyLatentImage",
                    ["inputs"] = new JsonObject
                    {
                        ["width"] = baseWidth,
                        ["height"] = baseHeight,
                        ["batch_size"] = 1
                    }
                },
                ["6"] = new JsonObject  // CLIP Text Encode (positive)
                {
                    ["class_type"] = "CLIPTextEncode",
                    ["inputs"] = new JsonObject
                    {
                        ["text"] = prompt,
                        ["clip"] = new JsonArray { "4", 1 }
                    }
                },
                ["7"] = new JsonObject  // CLIP Text Encode (negative)
                {
                    ["class_type"] = "CLIPTextEncode",
                    ["inputs"] = new JsonObject
                    {
                        ["text"] = negativePrompt,
                        ["clip"] = new JsonArray { "4", 1 }
                    }
                },
                ["8"] = new JsonObject  // VAE Decode
                {
                    ["class_type"] = "VAEDecode",
                    ["inputs"] = new JsonObject
                    {
                        ["samples"] = new JsonArray { "3", 0 },
                        ["vae"] = new JsonArray { "4", 2 }
                    }
                },
                ["9"] = new JsonObject  // Save Image
                {
                    ["class_type"] = "SaveImage",
                    ["inputs"] = new JsonObject
                    {
                        ["images"] = new JsonArray { "8", 0 },
                        ["filename_prefix"] = "EchoForge"
                    }
                }
            };

            return workflow.ToJsonString();
        }
        else
        {
            // Two-pass workflow: Generate base → Upscale → Refine
            var workflow = new JsonObject
            {
                // === PASS 1: Base generation ===
                ["4"] = new JsonObject  // CheckpointLoader
                {
                    ["class_type"] = "CheckpointLoaderSimple",
                    ["inputs"] = new JsonObject
                    {
                        ["ckpt_name"] = model
                    }
                },
                ["5"] = new JsonObject  // EmptyLatentImage
                {
                    ["class_type"] = "EmptyLatentImage",
                    ["inputs"] = new JsonObject
                    {
                        ["width"] = baseWidth,
                        ["height"] = baseHeight,
                        ["batch_size"] = 1
                    }
                },
                ["6"] = new JsonObject  // CLIP positive
                {
                    ["class_type"] = "CLIPTextEncode",
                    ["inputs"] = new JsonObject
                    {
                        ["text"] = prompt,
                        ["clip"] = new JsonArray { "4", 1 }
                    }
                },
                ["7"] = new JsonObject  // CLIP negative
                {
                    ["class_type"] = "CLIPTextEncode",
                    ["inputs"] = new JsonObject
                    {
                        ["text"] = negativePrompt,
                        ["clip"] = new JsonArray { "4", 1 }
                    }
                },
                ["3"] = new JsonObject  // KSampler - Base
                {
                    ["class_type"] = "KSampler",
                    ["inputs"] = new JsonObject
                    {
                        ["model"] = new JsonArray { "4", 0 },
                        ["positive"] = new JsonArray { "6", 0 },
                        ["negative"] = new JsonArray { "7", 0 },
                        ["latent_image"] = new JsonArray { "5", 0 },
                        ["seed"] = seed,
                        ["steps"] = 25,
                        ["cfg"] = 7.0,
                        ["sampler_name"] = "euler_ancestral",
                        ["scheduler"] = "normal",
                        ["denoise"] = 1.0
                    }
                },
                ["8"] = new JsonObject  // VAE Decode base
                {
                    ["class_type"] = "VAEDecode",
                    ["inputs"] = new JsonObject
                    {
                        ["samples"] = new JsonArray { "3", 0 },
                        ["vae"] = new JsonArray { "4", 2 }
                    }
                },

                // === PASS 2: Upscale to target resolution ===
                ["10"] = new JsonObject  // ImageScale (Lanczos upscale to target)
                {
                    ["class_type"] = "ImageScale",
                    ["inputs"] = new JsonObject
                    {
                        ["image"] = new JsonArray { "8", 0 },
                        ["upscale_method"] = "lanczos",
                        ["width"] = targetWidth,
                        ["height"] = targetHeight,
                        ["crop"] = "disabled"
                    }
                },
                ["11"] = new JsonObject  // VAE Encode (encode upscaled image to latent for refinement)
                {
                    ["class_type"] = "VAEEncode",
                    ["inputs"] = new JsonObject
                    {
                        ["pixels"] = new JsonArray { "10", 0 },
                        ["vae"] = new JsonArray { "4", 2 }
                    }
                },
                ["12"] = new JsonObject  // KSampler - Refine (low denoise to add detail)
                {
                    ["class_type"] = "KSampler",
                    ["inputs"] = new JsonObject
                    {
                        ["model"] = new JsonArray { "4", 0 },
                        ["positive"] = new JsonArray { "6", 0 },
                        ["negative"] = new JsonArray { "7", 0 },
                        ["latent_image"] = new JsonArray { "11", 0 },
                        ["seed"] = seed,
                        ["steps"] = 15,
                        ["cfg"] = 7.0,
                        ["sampler_name"] = "euler_ancestral",
                        ["scheduler"] = "normal",
                        ["denoise"] = 0.4 // Low denoise preserves base composition while adding HD detail
                    }
                },
                ["13"] = new JsonObject  // VAE Decode final
                {
                    ["class_type"] = "VAEDecode",
                    ["inputs"] = new JsonObject
                    {
                        ["samples"] = new JsonArray { "12", 0 },
                        ["vae"] = new JsonArray { "4", 2 }
                    }
                },
                ["9"] = new JsonObject  // Save Image
                {
                    ["class_type"] = "SaveImage",
                    ["inputs"] = new JsonObject
                    {
                        ["images"] = new JsonArray { "13", 0 },
                        ["filename_prefix"] = "EchoForge"
                    }
                }
            };

            return workflow.ToJsonString();
        }
    }

    private async Task<string?> QueuePromptAsync(string workflowJson, string clientId, CancellationToken cancellationToken)
    {
        var requestBody = new JsonObject
        {
            ["prompt"] = JsonNode.Parse(workflowJson),
            ["client_id"] = clientId
        };

        var content = new StringContent(requestBody.ToJsonString(), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync($"{_baseUrl}/prompt", content, cancellationToken);
        var responseStr = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError("ComfyUI queue prompt failed: {Status} — {Error}", response.StatusCode, responseStr);
            throw new Exception($"ComfyUI error: {responseStr}");
        }

        using var doc = JsonDocument.Parse(responseStr);
        return doc.RootElement.GetProperty("prompt_id").GetString();
    }

    private async Task<(string filename, string subfolder, string type)?> WaitForCompletionAsync(string promptId, System.Net.WebSockets.ClientWebSocket ws, Action<int, string>? progressCallback, CancellationToken cancellationToken)
    {
        if (ws.State != System.Net.WebSockets.WebSocketState.Open)
        {
            return await FallbackPollingAsync(promptId, cancellationToken);
        }

        var buffer = new byte[8192];
        while (ws.State == System.Net.WebSockets.WebSocketState.Open && !cancellationToken.IsCancellationRequested)
        {
            var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
            {
                var message = Encoding.UTF8.GetString(buffer, 0, result.Count);
                try 
                {
                    using var doc = JsonDocument.Parse(message);
                    if (doc.RootElement.TryGetProperty("type", out var typeEl))
                    {
                        var type = typeEl.GetString();
                        
                        if (type == "progress")
                        {
                            var data = doc.RootElement.GetProperty("data");
                            int value = data.GetProperty("value").GetInt32();
                            int max = data.GetProperty("max").GetInt32();
                            
                            int percent = max > 0 ? (int)((value / (float)max) * 100) : 0;
                            progressCallback?.Invoke(percent, $"ComfyUI İlerleyişi: %{percent} ({value}/{max} Adım)");
                        }
                        else if (type == "executed" || type == "execution_cached")
                        {
                            var data = doc.RootElement.GetProperty("data");
                            var pid = data.GetProperty("prompt_id").GetString();
                            if (pid == promptId)
                            {
                                return await FallbackPollingAsync(promptId, cancellationToken);
                            }
                        }
                        else if (type == "execution_error")
                        {
                            var data = doc.RootElement.GetProperty("data");
                            var pid = data.GetProperty("prompt_id").GetString();
                            if (pid == promptId)
                            {
                                throw new Exception($"ComfyUI Error: {data.GetProperty("exception_message").GetString()}");
                            }
                        }
                    }
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    // Ignore JSON parse errors for non-matching structure
                }
            }
        }
        
        return null;
    }

    private async Task<(string filename, string subfolder, string type)?> FallbackPollingAsync(string promptId, CancellationToken cancellationToken)
    {
        int maxWaitSeconds = 300; // 5 minutes max
        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSeconds);

        while (DateTime.UtcNow < deadline && !cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1500, cancellationToken);

            try
            {
                var response = await _httpClient.GetStringAsync($"{_baseUrl}/history/{promptId}", cancellationToken);
                using var doc = JsonDocument.Parse(response);

                if (doc.RootElement.TryGetProperty(promptId, out var promptResult))
                {
                    if (promptResult.TryGetProperty("status", out var status) && 
                        status.TryGetProperty("status_str", out var statusStr) &&
                        statusStr.GetString() == "error")
                    {
                        throw new Exception("ComfyUI generation failed.");
                    }

                    if (promptResult.TryGetProperty("outputs", out var outputs))
                    {
                        foreach (var nodeOutput in outputs.EnumerateObject())
                        {
                            if (nodeOutput.Value.TryGetProperty("images", out var images) && images.GetArrayLength() > 0)
                            {
                                var img = images[0];
                                var filename = img.GetProperty("filename").GetString() ?? "";
                                var subfolder = img.TryGetProperty("subfolder", out var sf) ? sf.GetString() ?? "" : "";
                                var type = img.TryGetProperty("type", out var tp) ? tp.GetString() ?? "output" : "output";

                                return (filename, subfolder, type);
                            }
                        }
                    }
                }
            }
            catch (HttpRequestException)
            {
                // Retry
            }
        }

        return null;
    }

    private async Task<string> DownloadOutputAsync(string filename, string subfolder, string type, string savePath, CancellationToken cancellationToken)
    {
        var url = $"{_baseUrl}/view?filename={Uri.EscapeDataString(filename)}&subfolder={Uri.EscapeDataString(subfolder)}&type={Uri.EscapeDataString(type)}";
        var imageBytes = await _httpClient.GetByteArrayAsync(url, cancellationToken);
        
        if (imageBytes.Length < 1000)
        {
            throw new Exception($"ComfyUI returned tiny image ({imageBytes.Length} bytes).");
        }

        await File.WriteAllBytesAsync(savePath, imageBytes, cancellationToken);
        return savePath;
    }
}
