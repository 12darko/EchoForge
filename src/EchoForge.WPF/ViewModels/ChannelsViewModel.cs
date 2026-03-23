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

public partial class ChannelsViewModel : ObservableObject
{
    private readonly ApiClient _apiClient;

    [ObservableProperty]
    private ObservableCollection<YouTubeChannel> _channels = new();

    [ObservableProperty]
    private ObservableCollection<YouTubeVideoDto> _selectedChannelVideos = new();

    [ObservableProperty]
    private YouTubeChannel? _selectedChannel;

    [ObservableProperty]
    private bool _isLoading;


    // YouTube OAuth Credentials (moved from Settings)
    [ObservableProperty]
    private string _youTubeClientId = string.Empty;

    [ObservableProperty]
    private string _youTubeClientSecret = string.Empty;

    // Tutorial visibility
    [ObservableProperty]
    private bool _isTutorialVisible;

    // Admin visibility
    [ObservableProperty]
    private bool _isAdmin;

    public bool HasNoChannels => Channels.Count == 0;

    public ChannelsViewModel(ApiClient apiClient, bool isAdmin = false)
    {
        _apiClient = apiClient;
        _isAdmin = isAdmin;
        LoadChannelsCommand.Execute(null);
        LoadCredentials();
    }

    private async void LoadCredentials()
    {
        try
        {
            var settings = await _apiClient.GetAllSettingsAsync(_isAdmin);
            var clientId = settings.FirstOrDefault(s => s.Key == "YouTube:ClientId")?.Value;
            if (!string.IsNullOrEmpty(clientId)) YouTubeClientId = clientId;
            
            var clientSecret = settings.FirstOrDefault(s => s.Key == "YouTube:ClientSecret")?.Value;
            if (!string.IsNullOrEmpty(clientSecret)) YouTubeClientSecret = clientSecret;
        }
        catch { /* Silent — credentials may not exist yet */ }
    }

    [RelayCommand]
    private void ShowTutorial() => IsTutorialVisible = true;

    [RelayCommand]
    private void CloseTutorial() => IsTutorialVisible = false;

    [RelayCommand]
    private async Task SaveCredentialsAsync()
    {
        try
        {
            if (!string.IsNullOrEmpty(YouTubeClientId))
                await _apiClient.SaveSettingAsync("YouTube:ClientId", YouTubeClientId, true);
            if (!string.IsNullOrEmpty(YouTubeClientSecret))
                await _apiClient.SaveSettingAsync("YouTube:ClientSecret", YouTubeClientSecret, true);

            EchoForge.WPF.Views.EchoMessageBox.Show("YouTube API credentials saved successfully!", "Success", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Success);
        }
        catch (Exception ex)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Failed to save credentials: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
    }

    [RelayCommand]
    public async Task LoadChannelsAsync()
    {
        IsLoading = true;
        try
        {
            var data = await _apiClient.GetChannelsAsync();
            Channels = new ObservableCollection<YouTubeChannel>(data);
            OnPropertyChanged(nameof(HasNoChannels));
        }
        catch (Exception ex)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Failed to load channels: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ConnectChannelAsync()
    {
        if (string.IsNullOrWhiteSpace(YouTubeClientId) || string.IsNullOrWhiteSpace(YouTubeClientSecret))
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Lütfen önce Ayarlar'dan YouTube API Kimlik Bilgilerini (Client ID & Client Secret) kaydedin.", "Credentials Required", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);
            return;
        }

        IsLoading = true;
        try
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Tarayıcınız YouTube yetkilendirmesi için açılıyor. Lütfen açılan sayfada giriş yapıp izinleri onaylayın.", "Info", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Info);

            var (tokenJson, authError) = await YouTubeAuthHelper.AuthorizeAndGetTokenAsync(YouTubeClientId, YouTubeClientSecret);
            
            if (tokenJson == null)
            {
                EchoForge.WPF.Views.EchoMessageBox.Show(
                    authError ?? "Yetkilendirme işlemi iptal edildi veya tarayıcıda başarısız oldu.", 
                    "YouTube Bağlantı Hatası", 
                    EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);
                return;
            }

            var channel = await _apiClient.ConnectYouTubeWithTokenAsync(tokenJson);
            
            if (channel != null)
            {
                await LoadChannelsAsync();
                SelectedChannel = Channels.FirstOrDefault(c => c.ChannelId == channel.ChannelId);
                EchoForge.WPF.Views.EchoMessageBox.Show($"'{channel.ChannelName}' kanalı başarıyla bağlandı ve sunucuya eklendi!", "Success", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Success);
            }
            else
            {
                EchoForge.WPF.Views.EchoMessageBox.Show("Kanal bilgileri veya Token sunucuya kaydedilemedi.", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
            }
        }
        catch (Exception ex)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Bağlantı hatası: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
        finally
        {
            IsLoading = false;
            ConnectChannelCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    public async Task DeleteChannelAsync(int id)
    {
        if (EchoForge.WPF.Views.EchoMessageBox.Show("Are you sure you want to delete this channel connection?", "Confirm Delete", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Question) == MessageBoxResult.OK)
        {
            await _apiClient.DeleteChannelAsync(id);
            await LoadChannelsAsync();
            if (SelectedChannel?.Id == id)
            {
                SelectedChannelItemsCollectionClear();
            }
        }
    }

    partial void OnSelectedChannelChanged(YouTubeChannel? value)
    {
        if (value != null)
        {
            LoadVideosForChannelAsync(value.ChannelId);
        }
        else
        {
            SelectedChannelItemsCollectionClear();
        }
    }

    private void SelectedChannelItemsCollectionClear()
    {
        SelectedChannelVideos.Clear();
    }

    private async void LoadVideosForChannelAsync(string channelId)
    {
        IsLoading = true;
        try
        {
            var vids = await _apiClient.GetChannelVideosAsync(channelId);
            SelectedChannelVideos = new ObservableCollection<YouTubeVideoDto>(vids);
        }
        catch (Exception ex)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Error loading videos: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
        finally
        {
            IsLoading = false;
        }
    }
}
