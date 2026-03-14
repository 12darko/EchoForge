using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.DTOs;
using EchoForge.Core.Entities;
using EchoForge.WPF.Services;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace EchoForge.WPF.ViewModels;

public partial class YouTubeVideosViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;

    [ObservableProperty]
    private ObservableCollection<YouTubeChannel> _channels = new();

    [ObservableProperty]
    private YouTubeChannel? _selectedChannel;

    [ObservableProperty]
    private ObservableCollection<YouTubeVideoDto> _videos = new();

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Select a channel to view its videos.";

    [ObservableProperty]
    private int _totalVideoCount;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    private List<YouTubeVideoDto> _allVideos = new();

    public YouTubeVideosViewModel(ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    public async Task LoadChannelsAsync()
    {
        IsLoading = true;
        try
        {
            var data = await _apiClient.GetChannelsAsync();
            Channels = new ObservableCollection<YouTubeChannel>(data);
            StatusMessage = Channels.Count > 0
                ? $"{Channels.Count} channel(s) found. Select one to view videos."
                : "No channels connected. Go to My Channels to add one.";
            
            // Auto-select first channel if only one exists
            if (Channels.Count == 1)
            {
                SelectedChannel = Channels[0];
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error loading channels: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedChannelChanged(YouTubeChannel? value)
    {
        if (value != null)
        {
            _ = LoadVideosAsync(value.ChannelId);
        }
        else
        {
            Videos.Clear();
            _allVideos.Clear();
            TotalVideoCount = 0;
        }
    }

    partial void OnSearchQueryChanged(string value)
    {
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        if (string.IsNullOrWhiteSpace(SearchQuery))
        {
            Videos = new ObservableCollection<YouTubeVideoDto>(_allVideos);
        }
        else
        {
            var q = SearchQuery.ToLowerInvariant();
            Videos = new ObservableCollection<YouTubeVideoDto>(
                _allVideos.Where(v => v.Title.ToLowerInvariant().Contains(q)));
        }
    }

    private async Task LoadVideosAsync(string channelId)
    {
        IsLoading = true;
        StatusMessage = "Loading videos...";
        try
        {
            var vids = await _apiClient.GetChannelVideosAsync(channelId);
            _allVideos = vids;
            Videos = new ObservableCollection<YouTubeVideoDto>(vids);
            TotalVideoCount = vids.Count;
            StatusMessage = $"Showing {vids.Count} video(s) from {SelectedChannel?.ChannelName ?? channelId}";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenVideoInBrowser(string videoId)
    {
        if (!string.IsNullOrWhiteSpace(videoId))
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = $"https://www.youtube.com/watch?v={videoId}",
                    UseShellExecute = true
                });
            }
            catch { }
        }
    }
}
