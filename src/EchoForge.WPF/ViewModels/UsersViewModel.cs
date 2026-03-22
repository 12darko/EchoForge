using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.DTOs;

namespace EchoForge.WPF.ViewModels;

public partial class UsersViewModel : ObservableObject
{
    private readonly Services.ApiClient _apiClient;

    [ObservableProperty]
    private ObservableCollection<UserDto> _users = new();

    [ObservableProperty]
    private string _newUsername = string.Empty;

    [ObservableProperty]
    private string _newPassword = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _isCreating;

    public UsersViewModel(Services.ApiClient apiClient)
    {
        _apiClient = apiClient;
    }

    [RelayCommand]
    private async Task LoadUsers()
    {
        try
        {
            IsLoading = true;
            StatusMessage = "";
            var users = await _apiClient.GetUsersAsync();
            Users = new ObservableCollection<UserDto>(users);
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Kullanıcılar yüklenemedi: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private async Task CreateUser()
    {
        if (string.IsNullOrWhiteSpace(NewUsername) || string.IsNullOrWhiteSpace(NewPassword))
        {
            StatusMessage = "⚠️ Kullanıcı adı ve şifre boş bırakılamaz.";
            return;
        }

        if (NewPassword.Length < 4)
        {
            StatusMessage = "⚠️ Şifre en az 4 karakter olmalıdır.";
            return;
        }

        try
        {
            IsCreating = true;
            StatusMessage = "⏳ Kullanıcı oluşturuluyor...";

            var success = await _apiClient.CreateUserAsync(NewUsername, NewPassword);
            if (success)
            {
                StatusMessage = $"✅ '{NewUsername}' başarıyla oluşturuldu!";
                NewUsername = string.Empty;
                NewPassword = string.Empty;
                await LoadUsers();
            }
            else
            {
                StatusMessage = "❌ Kullanıcı oluşturulamadı. Kullanıcı adı zaten mevcut olabilir.";
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Hata: {ex.Message}";
        }
        finally
        {
            IsCreating = false;
        }
    }

    [RelayCommand]
    private async Task ToggleUserActive(UserDto user)
    {
        if (user == null) return;

        try
        {
            var success = await _apiClient.ToggleUserActiveAsync(user.Id);
            if (success)
            {
                StatusMessage = user.IsActive
                    ? $"🔒 '{user.Username}' devre dışı bırakıldı."
                    : $"🔓 '{user.Username}' tekrar aktifleştirildi.";
                await LoadUsers();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Durum değiştirilemedi: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task DeleteUser(UserDto user)
    {
        if (user == null) return;

        if (user.Username.Equals("admin", StringComparison.OrdinalIgnoreCase))
        {
            StatusMessage = "⚠️ Admin hesabı silinemez!";
            return;
        }

        try
        {
            var result = EchoForge.WPF.Views.EchoMessageBox.Show(
                $"'{user.Username}' kullanıcısını silmek istediğinize emin misiniz?\nBu işlem geri alınamaz!",
                "Kullanıcı Silme", 
                EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);

            if (result != System.Windows.MessageBoxResult.OK) return;

            var success = await _apiClient.DeleteUserAsync(user.Id);
            if (success)
            {
                StatusMessage = $"🗑️ '{user.Username}' başarıyla silindi.";
                await LoadUsers();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"❌ Silme hatası: {ex.Message}";
        }
    }
}
