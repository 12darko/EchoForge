using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace EchoForge.WPF.ViewModels;

public partial class LoginViewModel : ObservableObject
{
    [ObservableProperty]
    private string _username = string.Empty;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    [ObservableProperty]
    private bool _rememberMe;

    [ObservableProperty]
    private bool _hasError;

    private string _password = string.Empty;

    public string Password
    {
        get => _password;
        set 
        { 
            SetProperty(ref _password, value); 
            ClearError();
        }
    }

    partial void OnUsernameChanged(string value)
    {
        ClearError();
    }

    private void ClearError()
    {
        if (HasError)
        {
            HasError = false;
            ErrorMessage = string.Empty;
        }
    }

    /// <summary>
    /// Event raised when login succeeds.
    /// </summary>
    public event Action<bool>? LoginSucceeded;

    private static readonly string CredentialsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "EchoForge", "credentials.dat");

    private readonly Services.ApiClient _apiClient;

    public LoginViewModel(Services.ApiClient apiClient)
    {
        _apiClient = apiClient;
        LoadSavedCredentials();
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        HasError = false;

        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrWhiteSpace(Password))
        {
            ErrorMessage = "Please enter both username and password.";
            HasError = true;
            return;
        }

        var response = await _apiClient.LoginAsync(Username, Password);
        
        if (response != null && response.Success)
        {
            if (RememberMe)
                SaveCredentials();
            else
                ClearSavedCredentials();

            // Force IsAdmin if the user is the default admin
            if (Username.ToLower() == "admin")
            {
                response.IsAdmin = true;
            }

            LoginSucceeded?.Invoke(response.IsAdmin);
        }
        else
        {
            ErrorMessage = response?.Message ?? "Failed to log in. Check your connection or credentials.";
            HasError = true;
        }
    }

    private void SaveCredentials()
    {
        try
        {
            var dir = Path.GetDirectoryName(CredentialsPath)!;
            Directory.CreateDirectory(dir);
            var data = $"{Username}\n{Password}";
            var encrypted = ProtectedData.Protect(
                Encoding.UTF8.GetBytes(data), null, DataProtectionScope.CurrentUser);
            File.WriteAllBytes(CredentialsPath, encrypted);
        }
        catch { /* silent */ }
    }

    private void LoadSavedCredentials()
    {
        try
        {
            if (!File.Exists(CredentialsPath)) return;
            var encrypted = File.ReadAllBytes(CredentialsPath);
            var data = ProtectedData.Unprotect(encrypted, null, DataProtectionScope.CurrentUser);
            var parts = Encoding.UTF8.GetString(data).Split('\n');
            if (parts.Length >= 2)
            {
                Username = parts[0];
                Password = parts[1];
                RememberMe = true;
            }
        }
        catch { /* silent */ }
    }

    private void ClearSavedCredentials()
    {
        try { if (File.Exists(CredentialsPath)) File.Delete(CredentialsPath); }
        catch { /* silent */ }
    }
}
