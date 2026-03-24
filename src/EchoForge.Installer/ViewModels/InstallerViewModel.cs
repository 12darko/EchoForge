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
        private readonly bool _isUninstallMode;

        // === Install Screen ===
        [ObservableProperty]
        private bool _isWelcomeVisible = false;

        // === Uninstall Screen ===
        [ObservableProperty]
        private bool _isUninstallWelcomeVisible = false;

        // === Shared Screens ===
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

        [ObservableProperty]
        private string _doneTitle = "✅ Kurulum Başarıyla Tamamlandı!";

        [ObservableProperty]
        private string _doneSubtitle = "EchoForge içerik stüdyosu kullanıma hazır.";

        [ObservableProperty]
        private bool _showLaunchButton = true;

        public InstallerViewModel(Window window)
        {
            _window = window;
            
            // Check if running in uninstall mode
            var args = Environment.GetCommandLineArgs();
            _isUninstallMode = Array.Exists(args, a => a.Equals("--uninstall", StringComparison.OrdinalIgnoreCase));

            if (_isUninstallMode)
            {
                // Try to read install path from registry
                try
                {
                    using var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EchoForge");
                    if (key != null)
                    {
                        InstallPath = key.GetValue("InstallLocation")?.ToString() ?? InstallPath;
                    }
                }
                catch { }

                IsUninstallWelcomeVisible = true;
            }
            else
            {
                IsWelcomeVisible = true;
            }
        }

        // ========================
        //  INSTALL
        // ========================
        [RelayCommand]
        private async Task InstallAsync()
        {
            IsWelcomeVisible = false;
            IsInstallingVisible = true;

            try
            {
                await RunInstallationAsync();

                DoneTitle = "✅ Kurulum Başarıyla Tamamlandı!";
                DoneSubtitle = "EchoForge içerik stüdyosu kullanıma hazır.";
                ShowLaunchButton = true;
                IsInstallingVisible = false;
                IsDoneVisible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kurulum sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
                Application.Current.Shutdown();
            }
        }

        // ========================
        //  UNINSTALL
        // ========================
        [RelayCommand]
        private async Task UninstallAsync()
        {
            IsUninstallWelcomeVisible = false;
            IsInstallingVisible = true;

            try
            {
                await RunUninstallAsync();

                DoneTitle = "🗑️ Kaldırma İşlemi Tamamlandı!";
                DoneSubtitle = "EchoForge bilgisayarınızdan başarıyla kaldırıldı.";
                ShowLaunchButton = false;
                IsInstallingVisible = false;
                IsDoneVisible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kaldırma sırasında hata oluştu:\n{ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
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

        [RelayCommand]
        private void CloseApp()
        {
            Application.Current.Shutdown();
        }

        // ========================
        //  INSTALL LOGIC
        // ========================
        private async Task RunInstallationAsync()
        {
            if (!Directory.Exists(InstallPath))
                Directory.CreateDirectory(InstallPath);

            StatusText = "Dosyalar çıkarılıyor...";
            
            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = System.Linq.Enumerable.FirstOrDefault(assembly.GetManifestResourceNames(), n => n.EndsWith("payload.zip"));
            if (resourceName == null)
                throw new FileNotFoundException("Sistem derleme dosyası (payload.zip) installer içerisinde bulunamadı.");

            using (var resourceStream = assembly.GetManifestResourceStream(resourceName))
            {
                using (var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read))
                {
                    int totalEntries = archive.Entries.Count;
                    int currentEntry = 0;

                    foreach (var entry in archive.Entries)
                    {
                        if (string.IsNullOrEmpty(entry.Name))
                        {
                            Directory.CreateDirectory(Path.Combine(InstallPath, entry.FullName));
                        }
                        else
                        {
                            string destPath = Path.Combine(InstallPath, entry.FullName);
                            string destDir = Path.GetDirectoryName(destPath)!;
                            
                            if (!Directory.Exists(destDir))
                                Directory.CreateDirectory(destDir);

                            entry.ExtractToFile(destPath, overwrite: true);
                        }

                        currentEntry++;
                        if (currentEntry % 5 == 0) await Task.Delay(1);
                        ProgressValue = ((double)currentEntry / totalEntries) * 100;
                    }
                }
            }

            StatusText = "Kısayollar oluşturuluyor...";
            await Task.Delay(500);

            if (CreateDesktopShortcut)
                CreateShortcut();

            // Copy ourselves as the uninstaller
            CopySelfAsUninstaller();

            CreateRegistryKeys();
            ProgressValue = 100;
        }

        // ========================
        //  UNINSTALL LOGIC
        // ========================
        private async Task RunUninstallAsync()
        {
            StatusText = "Masaüstü kısayolu siliniyor...";
            await Task.Delay(300);
            ProgressValue = 10;

            // Delete desktop shortcut
            try
            {
                string shortcut = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "EchoForge.lnk");
                if (File.Exists(shortcut)) File.Delete(shortcut);
            }
            catch { }

            StatusText = "Uygulama dosyaları siliniyor...";
            ProgressValue = 30;

            // Delete all files except EchoForge_Uninstall.exe (ourselves)
            if (Directory.Exists(InstallPath))
            {
                var files = Directory.GetFiles(InstallPath, "*", SearchOption.AllDirectories);
                int total = files.Length;
                int current = 0;
                string selfExe = Path.Combine(InstallPath, "EchoForge_Uninstall.exe");

                foreach (var file in files)
                {
                    try
                    {
                        if (!file.Equals(selfExe, StringComparison.OrdinalIgnoreCase))
                            File.Delete(file);
                    }
                    catch { }

                    current++;
                    if (current % 3 == 0) await Task.Delay(1);
                    ProgressValue = 30 + ((double)current / total) * 50;
                }

                // Delete subdirectories
                foreach (var dir in Directory.GetDirectories(InstallPath))
                {
                    try { Directory.Delete(dir, true); } catch { }
                }
            }

            StatusText = "Kayıt defteri temizleniyor...";
            ProgressValue = 90;
            await Task.Delay(300);

            // Remove registry key
            try
            {
                Microsoft.Win32.Registry.LocalMachine.DeleteSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EchoForge", false);
            }
            catch { }

            // Schedule self-deletion after the process exits
            ScheduleSelfDelete();

            ProgressValue = 100;
            StatusText = "Tamamlandı!";
        }

        private void ScheduleSelfDelete()
        {
            try
            {
                // Create a temp batch file that waits, then deletes the install folder
                string batPath = Path.Combine(Path.GetTempPath(), "echoforge_cleanup.bat");
                File.WriteAllText(batPath, $@"@echo off
ping 127.0.0.1 -n 3 > nul
rmdir /S /Q ""{InstallPath}""
del ""%~f0""
");
                Process.Start(new ProcessStartInfo
                {
                    FileName = batPath,
                    CreateNoWindow = true,
                    UseShellExecute = false
                });
            }
            catch { }
        }

        // ========================
        //  HELPERS
        // ========================
        private void CopySelfAsUninstaller()
        {
            try
            {
                string selfPath = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (!string.IsNullOrEmpty(selfPath))
                {
                    string destPath = Path.Combine(InstallPath, "EchoForge_Uninstall.exe");
                    File.Copy(selfPath, destPath, true);
                }
            }
            catch { }
        }

        private void CreateShortcut()
        {
            try
            {
                string targetPath = Path.Combine(InstallPath, "EchoForge.WPF.exe");
                string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
                string shortcutLocation = Path.Combine(desktopPath, "EchoForge.lnk");

                string psScript = $@"
$s = (New-Object -COM WScript.Shell).CreateShortcut('{shortcutLocation}')
$s.TargetPath = '{targetPath}'
$s.WorkingDirectory = '{InstallPath}'
$s.Save()";
                
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = $"-NoProfile -ExecutionPolicy Bypass -Command \"{psScript}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false
                })?.WaitForExit();
            }
            catch { }
        }

        private void CreateRegistryKeys()
        {
            try
            {
                string keyPath = @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\EchoForge";
                string uninstallExe = Path.Combine(InstallPath, "EchoForge_Uninstall.exe");
                string version = Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "2.2.2";

                using (var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(keyPath))
                {
                    key.SetValue("DisplayName", "EchoForge AI Content Studio");
                    key.SetValue("DisplayIcon", Path.Combine(InstallPath, "EchoForge.WPF.exe"));
                    key.SetValue("DisplayVersion", version);
                    key.SetValue("Publisher", "EchoForge");
                    key.SetValue("InstallLocation", InstallPath);
                    key.SetValue("UninstallString", $"\"{uninstallExe}\" --uninstall");
                    key.SetValue("QuietUninstallString", $"\"{uninstallExe}\" --uninstall");
                }
            }
            catch { }
        }
    }
}
