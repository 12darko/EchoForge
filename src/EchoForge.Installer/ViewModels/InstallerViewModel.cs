using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;

namespace EchoForge.Installer.ViewModels
{
    public partial class InstallerViewModel : ObservableObject
    {
        private readonly Window _window;

        [ObservableProperty]
        private bool _isWelcomeVisible = true;

        [ObservableProperty]
        private bool _isInstallingVisible = false;

        [ObservableProperty]
        private bool _isDoneVisible = false;

        [ObservableProperty]
        private string _installPath = @"C:\Program Files\EchoForge";

        [ObservableProperty]
        private bool _createDesktopShortcut = true;

        [ObservableProperty]
        private string _statusText = "Hazırlanıyor...";

        [ObservableProperty]
        private double _progressValue = 0;

        public InstallerViewModel(Window window)
        {
            _window = window;
        }

        [RelayCommand]
        private async Task InstallAsync()
        {
            IsWelcomeVisible = false;
            IsInstallingVisible = true;

            try
            {
                await RunInstallationAsync();

                IsInstallingVisible = false;
                IsDoneVisible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kurulum sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        [RelayCommand]
        private void Launch()
        {
            string exePath = Path.Combine(InstallPath, "EchoForge.WPF.exe");
            if (File.Exists(exePath))
            {
                Process.Start(new ProcessStartInfo { FileName = exePath, UseShellExecute = true });
            }
            Application.Current.Shutdown();
        }

        private async Task RunInstallationAsync()
        {
            if (!Directory.Exists(InstallPath))
            {
                Directory.CreateDirectory(InstallPath);
            }

            StatusText = "Dosyalar çıkarılıyor...";
            
            // Extract the embedded payload.zip
            using (var resourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("EchoForge.Installer.payload.zip"))
            {
                if (resourceStream == null)
                    throw new FileNotFoundException("Sistem derleme dosyası (payload.zip) installer içerisinde bulunamadı.");

                using (var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read))
                {
                    int totalEntries = archive.Entries.Count;
                    int currentEntry = 0;

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name)) // Directory
                        {
                            Directory.CreateDirectory(Path.Combine(InstallPath, entry.FullName));
                        }
                        else
                        {
                            string destPath = Path.Combine(InstallPath, entry.FullName);
                            string destDir = Path.GetDirectoryName(destPath)!;
                            
                            if (!Directory.Exists(destDir))
                                Directory.CreateDirectory(destDir);

                            // Extract the file
                            entry.ExtractToFile(destPath, overwrite: true);
                        }

                        currentEntry++;
                        
                        // Wait slightly to let UI update and look smooth
                        if (currentEntry % 5 == 0)
                            await Task.Delay(1);

                        ProgressValue = ((double)currentEntry / totalEntries) * 100;
                    }
                }
            }

            StatusText = "Kısayollar oluşturuluyor...";
            await Task.Delay(500);

            if (CreateDesktopShortcut)
            {
                CreateShortcut();
            }

            // Register Uninstaller (optional but good practice)
            CreateRegistryKeys();

            ProgressValue = 100;
        }

        private void CreateShortcut()
        {
            try
            {
                string targetPath = Path.Combine(InstallPath, "EchoForge.WPF.exe");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutLocation = Path.Combine(desktopPath, "EchoForge.lnk");

                // Using PowerShell to create a shortcut since WshShell COM is annoying to reference in modern .NET
                string psScript = $@"
$s = (New-Object -COM WScript.Shell).CreateShortcut('{shortcutLocation}')
$s.TargetPath = '{targetPath}'
$s.WorkingDirectory = '{InstallPath}'
$s.Save()";
                
                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(startInfo)?.WaitForExit();
            }
            catch { /* Ignore shortcut errors */ }
        }

        private void CreateRegistryKeys()
        {
            try
            {
                string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EchoForge";
                using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath))
                {
                    key.SetValue("DisplayName", "EchoForge AI Content Studio");
                    key.SetValue("DisplayIcon", Path.Combine(InstallPath, "EchoForge.WPF.exe"));
                    key.SetValue("DisplayVersion", "2.0.0");
                    key.SetValue("Publisher", "EchoForge");
                    key.SetValue("InstallLocation", InstallPath);
                    
                    // A simple uninstaller script just deletes the folder and key
                    string uninstallBat = Path.Combine(InstallPath, "uninstall.bat");
                    File.WriteAllText(uninstallBat, $@"@echo off
echo EchoForge siliniyor...
rmdir /S /Q ""{InstallPath}""
reg delete ""HKLM\{keyPath}"" /f
echo Silindi!
");
                    key.SetValue("UninstallString", uninstallBat);
                }
            }
            catch { /* Ignore registry errors if not running as full admin sometimes */ }
        }
    }
}
