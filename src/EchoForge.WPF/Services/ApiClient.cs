using System.IO;
using System.Net.Http;
using System.Net.Http.Json;
using EchoForge.Core.DTOs;
using EchoForge.Core.Models;

namespace EchoForge.WPF.Services;

public class ApiClient
{
    private readonly HttpClient _httpClient;

    public static ApiClient? Instance { get; private set; }
    public string BaseUrl => _httpClient.BaseAddress?.ToString() ?? "http://localhost:5035";

    public static bool IsConnected { get; private set; } = true;
    public int CurrentUserId { get; private set; }

    public static event Action<bool>? ConnectionStateChanged;

    public static void SetConnectionState(bool isConnected)
    {
        if (IsConnected == isConnected) return;
        IsConnected = isConnected;
        System.Windows.Application.Current.Dispatcher.Invoke(() => 
        {
            ConnectionStateChanged?.Invoke(IsConnected);
        });
    }

    private class ConnectionCheckHandler : DelegatingHandler
    {
        public ConnectionCheckHandler(HttpMessageHandler innerHandler) : base(innerHandler) { }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            try
            {
                var response = await base.SendAsync(request, cancellationToken);
                SetConnectionState(true);
                return response;
            }
            catch (HttpRequestException)
            {
                SetConnectionState(false);
                throw;
            }
            catch (System.Net.Sockets.SocketException)
            {
                SetConnectionState(false);
                throw;
            }
        }
    }

    public ApiClient(string baseUrl = "http://localhost:5000")
    {
        var handler = new ConnectionCheckHandler(new HttpClientHandler());
        _httpClient = new HttpClient(handler) { BaseAddress = new Uri(baseUrl) };
        Instance = this;
    }

    public void SetBaseUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        if (!url.EndsWith("/")) url += "/";
        _httpClient.BaseAddress = new Uri(url);
    }

    private async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response)
    {
        if (!response.IsSuccessStatusCode)
        {
            string errorMessage = response.ReasonPhrase ?? "Unknown API error";
            try
            {
                var content = await response.Content.ReadAsStringAsync();
                using var doc = System.Text.Json.JsonDocument.Parse(content);
                if (doc.RootElement.TryGetProperty("message", out var msgElement) || 
                    doc.RootElement.TryGetProperty("Message", out msgElement))
                {
                    errorMessage = msgElement.GetString() ?? errorMessage;
                }
            }
            catch { /* Ignore parsing errors, fallback to ReasonPhrase */ }
            
            throw new Exception(errorMessage);
        }
    }

    public async Task<bool> TestConnectionAsync()
    {
        try
        {
            var response = await _httpClient.GetAsync("api/settings");
            bool success = response.IsSuccessStatusCode;
            SetConnectionState(success);
            return success;
        }
        catch
        {
            SetConnectionState(false);
            return false;
        }
    }

    // Auth
    public async Task<AuthResponse?> LoginAsync(string username, string password)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync("api/auth/login", new LoginRequest { Username = username, Password = password });
            if (response.IsSuccessStatusCode)
            {
                var authResp = await response.Content.ReadFromJsonAsync<AuthResponse>();
                if (authResp != null && authResp.Success)
                {
                    CurrentUserId = authResp.UserId;
                }
                return authResp;
            }
            return new AuthResponse { Success = false, Message = "Invalid credentials or server error." };
        }
        catch
        {
            return new AuthResponse { Success = false, Message = "Failed to connect to the authentication server." };
        }
    }

    // Projects
    public async Task<List<ProjectDto>> GetProjectsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<ProjectDto>>("api/projects") ?? new();
    }

    public async Task<ProjectDto?> GetProjectAsync(int id)
    {
        return await _httpClient.GetFromJsonAsync<ProjectDto>($"api/projects/{id}");
    }

    public async Task<ProjectDto?> CreateProjectAsync(string title, int templateId, FormatType format, string audioFilePath,
        bool extractAutoShorts = false, string transitionStyle = "fade", string privacyStatus = "private", string imageModel = "flux", int uniqueImageCount = 8, double? manualImageDurationSec = null, string imageStyle = "",
        string? customInstructions = null, string? targetPlatforms = null, int? targetChannelId = null, string? visualEffect = null)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(title), "Title");
        form.Add(new StringContent(templateId.ToString()), "TemplateId");
        form.Add(new StringContent(((int)format).ToString()), "FormatType");
        form.Add(new StringContent(extractAutoShorts.ToString()), "ExtractAutoShorts");
        form.Add(new StringContent(transitionStyle), "TransitionStyle");
        
        if (!string.IsNullOrEmpty(visualEffect))
            form.Add(new StringContent(visualEffect), "VisualEffect");

        form.Add(new StringContent(privacyStatus), "PrivacyStatus");
        form.Add(new StringContent(imageModel), "ImageModel");
        form.Add(new StringContent(uniqueImageCount.ToString()), "UniqueImageCount");
        if (manualImageDurationSec.HasValue)
            form.Add(new StringContent(manualImageDurationSec.Value.ToString()), "ManualImageDurationSec");
            
        form.Add(new StringContent(imageStyle ?? ""), "ImageStyle");

        if (!string.IsNullOrEmpty(customInstructions))
            form.Add(new StringContent(customInstructions), "CustomInstructions");

        if (!string.IsNullOrEmpty(targetPlatforms))
            form.Add(new StringContent(targetPlatforms), "TargetPlatforms");

        if (targetChannelId.HasValue)
            form.Add(new StringContent(targetChannelId.Value.ToString()), "TargetChannelId");

        var fileBytes = await File.ReadAllBytesAsync(audioFilePath);
        var fileContent = new ByteArrayContent(fileBytes);
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("audio/mpeg");
        form.Add(fileContent, "audioFile", Path.GetFileName(audioFilePath));

        var response = await _httpClient.PostAsync("api/projects", form);
        await EnsureSuccessOrThrowAsync(response);
        return await response.Content.ReadFromJsonAsync<ProjectDto>();
    }


    public async Task<bool> ApproveProjectAsync(int id)
    {
        var response = await _httpClient.PostAsync($"api/projects/{id}/approve", null);
        await EnsureSuccessOrThrowAsync(response);
        return true;
    }

    public async Task<bool> RetryProjectAsync(int id)
    {
        var response = await _httpClient.PostAsync($"api/projects/{id}/retry", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> RejectProjectAsync(int id)
    {
        var response = await _httpClient.PostAsync($"api/projects/{id}/reject", null);
        await EnsureSuccessOrThrowAsync(response);
        return true;
    }

    public async Task<bool> DeleteProjectAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/projects/{id}");
        await EnsureSuccessOrThrowAsync(response);
        return true;
    }

    public async Task<bool> RenderProjectAsync(int id)
    {
        var response = await _httpClient.PostAsync($"api/projects/{id}/render", null);
        return response.IsSuccessStatusCode;
    }

    public async Task<bool> UpdateProjectScenesAsync(int id, List<TimelineItemDto> scenes)
    {
        var response = await _httpClient.PutAsJsonAsync($"api/projects/{id}/scenes", scenes);
        return response.IsSuccessStatusCode;
    }

    public async Task<ProjectDto?> RegenerateSceneAsync(int id, int sceneNumber, string prompt)
    {
        var request = new { SceneNumber = sceneNumber, Prompt = prompt };
        var response = await _httpClient.PostAsJsonAsync($"api/projects/{id}/scenes/regenerate", request);
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<ProjectDto>();
        }
        return null;
    }

    // Templates
    public async Task<List<Template>> GetTemplatesAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<Template>>("api/templates") ?? new();
    }

    // Settings
    public async Task<bool> SaveSettingAsync(string key, string value, bool encrypt = false)
    {
        var response = await _httpClient.PostAsJsonAsync("api/settings", new { Key = key, Value = value, Encrypt = encrypt });
        return response.IsSuccessStatusCode;
    }

    public async Task<string?> GetSettingAsync(string key)
    {
        try 
        {
            var response = await _httpClient.GetAsync($"api/settings/{key}");
            if (!response.IsSuccessStatusCode) return null;
            var result = await response.Content.ReadFromJsonAsync<SettingDto>();
            return result?.Value;
        }
        catch { return null; }
    }

    public async Task<List<SettingDto>> GetAllSettingsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<SettingDto>>("api/settings") ?? new();
    }

    public class SettingDto { public string Key { get; set; } = string.Empty; public string Value { get; set; } = string.Empty; }

    public async Task<List<EchoForge.Core.Entities.YouTubeChannel>> GetChannelsAsync()
    {
        return await _httpClient.GetFromJsonAsync<List<EchoForge.Core.Entities.YouTubeChannel>>($"api/youtubechannels?userId={CurrentUserId}") ?? new();
    }

    public async Task<List<EchoForge.Core.DTOs.YouTubeVideoDto>> GetChannelVideosAsync(string channelId)
    {
        return await _httpClient.GetFromJsonAsync<List<EchoForge.Core.DTOs.YouTubeVideoDto>>($"api/youtubechannels/{channelId}/videos") ?? new();
    }

    public async Task<bool> DeleteChannelAsync(int id)
    {
        var response = await _httpClient.DeleteAsync($"api/youtubechannels/{id}");
        return response.IsSuccessStatusCode;
    }

    public async Task<EchoForge.Core.Entities.YouTubeChannel?> ConnectYouTubeAsync()
    {
        try
        {
            var response = await _httpClient.PostAsync("api/youtubechannels/connect", null);
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<EchoForge.Core.Entities.YouTubeChannel>();
            }
            return null;
        }
        catch { return null; }
    }
}
