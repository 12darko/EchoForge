using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.DTOs;

namespace EchoForge.WPF.ViewModels;

public partial class MainViewModel : ObservableObject
{
    [ObservableProperty]
    private object? _currentView;

    [ObservableProperty]
    private string _currentPage = "Login";

    [ObservableProperty]
    private string _statusMessage = "Ready";

    [ObservableProperty]
    private bool _isLoggedIn;

    [ObservableProperty]
    private bool _isCurrentUserAdmin;

    [ObservableProperty]
    private bool _isDisconnected;

    private readonly Services.ApiClient _apiClient;

    private DashboardViewModel? _dashboardVm;
    private CreateProjectViewModel? _createProjectVm;
    private SettingsViewModel? _settingsVm;
    private ChannelsViewModel? _channelsVm;
    private EditorViewModel? _editorVm;
    private YouTubeVideosViewModel? _youtubeVideosVm;
    private UsersViewModel? _usersVm;
    private LoginViewModel? _loginVm;

    [ObservableProperty]
    private bool _isTutorialVisible;

    [ObservableProperty]
    private string _tutorialTitle = "Welcome to EchoForge!";

    [ObservableProperty]
    private string _tutorialText = "Here you can learn how to use the app.";

    [ObservableProperty]
    private string _tutorialIcon = "💡";

    public MainViewModel()
    {
        _apiClient = new Services.ApiClient(Services.ServerConfig.GetServerUrl());
        Services.ApiClient.ConnectionStateChanged += OnConnectionStateChanged;
        ShowLogin();
    }

    private void OnConnectionStateChanged(bool isConnected)
    {
        IsDisconnected = !isConnected;
    }

    [RelayCommand]
    private async Task ReconnectAsync()
    {
        IsDisconnected = false; // Hide overlay while testing
        bool success = await _apiClient.TestConnectionAsync();
        IsDisconnected = !success;
        if (!success)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show(Localization.TranslationManager.Instance.Strings["Error_ConnectionFailed"] ?? "Failed to reconnect. Please ensure the server is running.", "Connection Failed", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
    }

    private void ShowLogin()
    {
        _loginVm = new LoginViewModel(_apiClient);
        _loginVm.LoginSucceeded += (bool isAdmin) =>
        {
            IsLoggedIn = true;
            IsCurrentUserAdmin = isAdmin;
            NavigateToDashboard();
        };

        // Auto-login check
        if (_loginVm.RememberMe && !string.IsNullOrEmpty(_loginVm.Username) && !string.IsNullOrEmpty(_loginVm.Password))
        {
            // Execute login flow which validates the saved credentials
            _loginVm.LoginCommand.Execute(null);
            
            // If there's an error after trying to login auto, show the login screen
            if (!_loginVm.HasError && IsLoggedIn)
            {
                return; // Navigation handled by LoginSucceeded event
            }
        }

        CurrentView = _loginVm;
        CurrentPage = "Login";
    }

    [RelayCommand]
    private void NavigateToDashboard()
    {
        CurrentPage = "Dashboard";
        // Always recreate DashboardVM or just refresh? Let's refresh projects if it exists.
        if (_dashboardVm == null)
        {
            _dashboardVm = new DashboardViewModel(_apiClient, NavigateToEditor);
            _dashboardVm.LoadProjectsCommand.Execute(null);
        }
        else
        {
            _dashboardVm.LoadProjectsCommand.Execute(null);
        }
        CurrentView = _dashboardVm;
    }

    private void NavigateToEditor(ProjectDto project)
    {
        CurrentPage = "Editor";
        // Recreate editor VM every time to ensure fresh state
        _editorVm = new EditorViewModel(project, _apiClient, () => NavigateToDashboard());
        CurrentView = _editorVm;
    }

    [RelayCommand]
    private void NavigateToCreate()
    {
        CurrentPage = "Create";
        _createProjectVm = new CreateProjectViewModel(_apiClient);
        _createProjectVm.ProjectCreated += () => NavigateToDashboard();
        CurrentView = _createProjectVm;
        _createProjectVm.LoadTemplatesCommand.Execute(null);
    }

    [RelayCommand]
    private void NavigateToSettings()
    {
        CurrentPage = "Settings";
        _settingsVm ??= new SettingsViewModel(_apiClient, IsCurrentUserAdmin);
        _settingsVm.IsAdmin = IsCurrentUserAdmin;
        CurrentView = _settingsVm;
    }

    [RelayCommand]
    private void NavigateToChannels()
    {
        CurrentPage = "Channels";
        _channelsVm ??= new ChannelsViewModel(_apiClient, IsCurrentUserAdmin);
        _channelsVm.IsAdmin = IsCurrentUserAdmin;
        CurrentView = _channelsVm;
        _channelsVm.LoadChannelsCommand.Execute(null);
    }

    [RelayCommand]
    private void NavigateToMyVideos()
    {
        CurrentPage = "MyVideos";
        _youtubeVideosVm ??= new YouTubeVideosViewModel(_apiClient);
        CurrentView = _youtubeVideosVm;
        _youtubeVideosVm.LoadChannelsCommand.Execute(null);
    }

    [RelayCommand]
    private void NavigateToUsers()
    {
        if (!IsCurrentUserAdmin) return;
        CurrentPage = "Users";
        _usersVm ??= new UsersViewModel(_apiClient);
        CurrentView = _usersVm;
        _usersVm.LoadUsersCommand.Execute(null);
    }

    [RelayCommand]
    private void ToggleTutorial()
    {
        if (IsTutorialVisible)
        {
            IsTutorialVisible = false;
            return;
        }

        // Set dynamic content based on CurrentPage
        switch (CurrentPage)
        {
            case "Dashboard":
                TutorialTitle = "Dashboard Hub";
                TutorialText = "Welcome to your studio command center.\n\n• New projects generate in the background.\n• When a project hits 'ReviewingScenes', you can click it to open the Editor.\n• The ⚙️ section tracks rendering progress.\n• Need to delete? Use the 🗑️ icon on the bottom right of each project card.";
                TutorialIcon = "📊";
                break;
            case "Create":
                TutorialTitle = "New Generation Pipeline";
                TutorialText = "Create AI-powered music videos easily.\n\n1. Pick a high-energy audio track (or keep the default).\n2. Select your vibe & template.\n3. Turn ON Auto-Shorts if you want AI to grab the most viral 60 seconds automatically.\n4. Type detailed visual instructions or rely entirely on AI logic.";
                TutorialIcon = "✨";
                break;
            case "Editor":
                TutorialTitle = "Interactive Timeline Editor";
                TutorialText = "Perfect your creation before uploading.\n\n• Left click a scene block to preview and edit its Transition/Zoom effects.\n• Drag the edges of a scene block to extend or shrink its duration.\n• Use the Playhead to preview the video flow.\n• Hit 'Render Video' when your masterpiece is ready!";
                TutorialIcon = "🎞️";
                break;
            case "Channels":
                TutorialTitle = "Connected Channels";
                TutorialText = "Manage where your videos go.\n\n• Link a specific YouTube Channel simply by Logging in.\n• The application uses a global developer API Key to securely route tokens per channel.\n• Once authenticated, you can direct pipeline uploads straight to specific channels.";
                TutorialIcon = "📺";
                break;
            case "Settings":
                TutorialTitle = "API Keys & Preferences";
                TutorialText = "Configure global application logic.\n\n• You MUST enter your Pollinations URL and Groq Key here so AI can generate visuals and SEO text.\n• The YouTube section here is for DEVELOPER KEYS, not your personal channel login.\n• Set a Default Intro/Outro video that will be glued to every single generated video automatically!";
                TutorialIcon = "⚙️";
                break;
            case "MyVideos":
                TutorialTitle = "Your Uploaded VODs";
                TutorialText = "See what you've uploaded historically to the active channel.\n\n• The AI uses YouTube Data APIs to fetch Thumbnails, Titles, and View Counts directly.\n• Refresh the page if a video just finished uploading by clicking the sidebar.";
                TutorialIcon = "📽️";
                break;
            case "Users":
                TutorialTitle = "Kullanıcı Yönetimi";
                TutorialText = "Bu panel sadece Admin kullanıcılarına açıktır.\n\n• Sol taraftaki formdan yeni kullanıcı hesabı oluşturabilirsiniz.\n• Sağ taraftaki listeden mevcut kullanıcıları görüntüleyebilir, devre dışı bırakabilir veya silebilirsiniz.\n• Admin hesabı silinemez.";
                TutorialIcon = "👥";
                break;
            default:
                TutorialTitle = "Need Help?";
                TutorialText = "Click around the sidebar to navigate EchoForge. You can always click the '?' button for page-specific assistance.";
                TutorialIcon = "💡";
                break;
        }

        IsTutorialVisible = true;
    }
}
