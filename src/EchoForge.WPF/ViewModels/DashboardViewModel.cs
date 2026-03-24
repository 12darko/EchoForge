using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using EchoForge.Core.DTOs;
using EchoForge.Core.Models;

namespace EchoForge.WPF.ViewModels;

public partial class DashboardViewModel : ObservableObject
{
    private readonly Services.ApiClient _apiClient;
    private readonly Services.ClientJobOrchestrator _orchestrator;

    [ObservableProperty]
    private ObservableCollection<ProjectDto> _projects = new();

    [ObservableProperty]
    private ProjectDto? _selectedProject;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusFilter = "All";

    [ObservableProperty] private int _totalProjects;
    [ObservableProperty] private int _totalCompleted;
    [ObservableProperty] private int _totalProcessing;
    [ObservableProperty] private string _totalStorageUsed = "0 MB";
    [ObservableProperty] private string _successRate = "0%";
    [ObservableProperty] private int _recentProjectsCount = 0;

    private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;
    private readonly Action<ProjectDto>? _onOpenEditor;

    public DashboardViewModel(Services.ApiClient apiClient, Services.ClientJobOrchestrator orchestrator, Action<ProjectDto>? onOpenEditor = null)
    {
        _apiClient = apiClient;
        _orchestrator = orchestrator;
        _onOpenEditor = onOpenEditor;

        _refreshTimer = new System.Windows.Threading.DispatcherTimer();
        _refreshTimer.Interval = TimeSpan.FromSeconds(3);
        _refreshTimer.Tick += async (s, e) => await SilentRefreshAsync();
        _refreshTimer.Start();
    }

    private bool _hasShownLoadError = false;

    [RelayCommand]
    private async Task LoadProjects()
    {
        IsLoading = true;
        try
        {
            var projects = await _apiClient.GetProjectsAsync();
            _hasShownLoadError = false; // Reset on success
            Projects.Clear();
            foreach (var p in projects)
            {
                if (!string.IsNullOrEmpty(p.TimelineJson))
                {
                    try
                    {
                        var items = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(p.TimelineJson);
                        if (items != null)
                        {
                            foreach (var item in items) 
                            {
                                if (item != null) p.Scenes.Add(item);
                            }
                        }
                    }
                    catch { /* ignore bad json completely */ }
                }
                Projects.Add(p);
            }

            TotalProjects = Projects.Count;
            TotalCompleted = Projects.Count(p => p.Status == ProjectStatus.Completed);
            TotalProcessing = Projects.Count(p => p.Status == ProjectStatus.ComposingVideo || p.Status == ProjectStatus.GeneratingImages || p.Status == ProjectStatus.GeneratingSEO || p.Status == ProjectStatus.Analyzing);
            
            // Calculate Success Rate
            var finishedProjects = Projects.Count(p => p.Status == ProjectStatus.Completed || p.Status == ProjectStatus.Failed);
            if (finishedProjects > 0)
            {
                var rate = (double)TotalCompleted / finishedProjects * 100;
                SuccessRate = $"{Math.Round(rate, 1)}%";
            }
            else
            {
                SuccessRate = "100%";
            }

            // Calculate Last 7 Days
            var sevenDaysAgo = DateTime.UtcNow.AddDays(-7);
            RecentProjectsCount = Projects.Count(p => p.CreatedAt >= sevenDaysAgo);

            // Mock storage used based on project count
            TotalStorageUsed = $"{Projects.Count * 125} MB";
        }
        catch (Exception ex)
        {
            // Only show error popup once, not on every refresh cycle
            if (!_hasShownLoadError)
            {
                _hasShownLoadError = true;
                string userMessage = ex.Message.Contains("500") 
                    ? "Sunucuya bağlanılamadı veya sunucu hatası oluştu (500).\nSunucunun güncel olduğundan emin olun."
                    : $"Projeler yüklenemedi: {ex.Message}";
                EchoForge.WPF.Views.EchoMessageBox.Show(userMessage, "Bağlantı Hatası", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    private async Task SilentRefreshAsync()
    {
        if (IsLoading) return;
        
        // Sadece aktif işlem gören projeler varsa yenile
        var hasActive = Projects.Any(p => p.Status == ProjectStatus.ComposingVideo || 
                                          p.Status == ProjectStatus.GeneratingImages || 
                                          p.Status == ProjectStatus.GeneratingSEO || 
                                          p.Status == ProjectStatus.Analyzing);
        
        if (!hasActive && SelectedProject?.Status != ProjectStatus.ComposingVideo) return;

        try
        {
            var projects = await _apiClient.GetProjectsAsync();
            var selectedId = SelectedProject?.Id;

            Projects.Clear();
            foreach (var p in projects)
            {
                if (!string.IsNullOrEmpty(p.TimelineJson))
                {
                    try
                    {
                        var items = System.Text.Json.JsonSerializer.Deserialize<List<TimelineItemDto>>(p.TimelineJson);
                        if (items != null)
                        {
                            foreach (var item in items) p.Scenes.Add(item);
                        }
                    }
                    catch { }
                }
                Projects.Add(p);
            }

            if (selectedId.HasValue)
            {
                SelectedProject = Projects.FirstOrDefault(p => p.Id == selectedId.Value);
            }
        }
        catch { /* ignore background errors */ }
    }

    [RelayCommand]
    private async Task ApproveProject()
    {
        if (SelectedProject == null) return;
        if (SelectedProject.Status != ProjectStatus.AwaitingApproval)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Project is not awaiting approval", "Info", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Info);
            return;
        }

        var result = await _apiClient.ApproveProjectAsync(SelectedProject.Id);
        if (result)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Upload job queued successfully!", "Success", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Success);
            await LoadProjects();
        }
    }

    [RelayCommand]
    private async Task RejectProject()
    {
        if (SelectedProject == null) return;

        var result = EchoForge.WPF.Views.EchoMessageBox.Show(
            "Are you sure you want to reject this project?",
            "Confirm", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Question);

        if (result == System.Windows.MessageBoxResult.OK)
        {
            var success = await _apiClient.RejectProjectAsync(SelectedProject.Id);
            if (!success)
            {
                EchoForge.WPF.Views.EchoMessageBox.Show("Failed to reject project. Please try again.", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
            }
            await LoadProjects();
        }
    }

    [RelayCommand]
    private async Task RetryProject()
    {
        if (SelectedProject == null) return;

        var allowedStatuses = new[] { ProjectStatus.Failed, ProjectStatus.ComposingVideo, 
                                      ProjectStatus.GeneratingImages, ProjectStatus.GeneratingSEO };
        if (!allowedStatuses.Contains(SelectedProject.Status))
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("This project cannot be retried in its current state.", "Info", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Info);
            return;
        }

        var result = EchoForge.WPF.Views.EchoMessageBox.Show(
            $"Retry pipeline for '{SelectedProject.Title}'?\nThis will restart the entire process.",
            "Confirm Retry", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Question);

        if (result == System.Windows.MessageBoxResult.OK)
        {
            await _apiClient.UpdateProjectStatusAsync(SelectedProject.Id, ProjectStatus.Created, "");
            
            _ = Task.Run(async () =>
            {
                await _orchestrator.StartPipelineAsync(SelectedProject.Id, System.Threading.CancellationToken.None);
            });

            EchoForge.WPF.Views.EchoMessageBox.Show("Pipeline restarted locally!", "Success", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Success);
            
            await LoadProjects();
        }
    }

    [RelayCommand]
    private async Task DeleteProject(ProjectDto? project = null)
    {
        var targetProject = project ?? SelectedProject;
        if (targetProject == null) return;

        var result = EchoForge.WPF.Views.EchoMessageBox.Show(
            $"Delete project '{targetProject.Title}'?",
            "Confirm", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Question);

        if (result == System.Windows.MessageBoxResult.OK)
        {
            try
            {
                var success = await _apiClient.DeleteProjectAsync(targetProject.Id);
                if (!success)
                {
                    EchoForge.WPF.Views.EchoMessageBox.Show("Failed to delete project. Please try again.", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
                }
            }
            catch (Exception ex)
            {
                EchoForge.WPF.Views.EchoMessageBox.Show($"Delete failed: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
            }
            finally
            {
                await LoadProjects();
            }
        }
    }

    [RelayCommand]
    private void OpenVideo()
    {
        if (SelectedProject == null) return;
        
        if (string.IsNullOrEmpty(SelectedProject.OutputVideoPath))
        {
            EchoForge.WPF.Views.EchoMessageBox.Show("Video is not generated yet. Please wait for the job to complete or check for errors.", "Wait", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Warning);
            return;
        }

        if (!System.IO.File.Exists(SelectedProject.OutputVideoPath))
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Video file not found at: {SelectedProject.OutputVideoPath}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
            return;
        }

        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = SelectedProject.OutputVideoPath,
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Could not open video: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
    }

    [RelayCommand]
    private void OpenEditor(ProjectDto? project = null)
    {
        var targetProject = project ?? SelectedProject;
        if (targetProject == null) return;

        var allowedStatuses = new[] { ProjectStatus.ReviewingScenes, ProjectStatus.AwaitingApproval, ProjectStatus.Completed };
        if (!allowedStatuses.Contains(targetProject.Status))
        {
            return; // Block entry if project is still generating
        }

        _onOpenEditor?.Invoke(targetProject);
    }

    [RelayCommand]
    private void OpenYouTube(string? videoId)
    {
        if (string.IsNullOrWhiteSpace(videoId)) return;
        
        try
        {
            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = $"https://youtube.com/watch?v={videoId}",
                UseShellExecute = true
            };
            System.Diagnostics.Process.Start(psi);
        }
        catch (Exception ex)
        {
            EchoForge.WPF.Views.EchoMessageBox.Show($"Could not open browser: {ex.Message}", "Error", EchoForge.WPF.Views.EchoMessageBox.EchoMessageType.Error);
        }
    }

    [RelayCommand]
    private async Task RefreshProjects()
    {
        await LoadProjects();
    }

    public string GetStatusColor(ProjectStatus status) => status switch
    {
        ProjectStatus.Completed => "#10B981",
        ProjectStatus.Failed => "#EF4444",
        ProjectStatus.AwaitingApproval => "#F59E0B",
        ProjectStatus.Uploading => "#3B82F6",
        _ => "#8B5CF6"
    };
}
