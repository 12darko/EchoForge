using System.Configuration;
using System.Data;
using System.Windows;
using System.IO;
using System;
using System.Threading.Tasks;
using System.Globalization;

namespace EchoForge.WPF;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        this.DispatcherUnhandledException += (s, args) =>
        {
            File.WriteAllText("crash_log.txt", args.Exception.ToString() + "\nInner: " + args.Exception.InnerException?.ToString());
            MessageBox.Show("Crash: " + args.Exception.Message + "\nInner: " + args.Exception.InnerException?.Message);
        };
        
        try
        {
            base.OnStartup(e);

            // Otomatik Dil Seçimi — Sistem dili Türkçe ise TR, değilse EN
            // CurrentCulture, CurrentUICulture ve InstalledUICulture kontrol edilir
            bool isTurkish = CultureInfo.CurrentCulture.TwoLetterISOLanguageName == "tr"
                          || CultureInfo.CurrentUICulture.TwoLetterISOLanguageName == "tr"
                          || CultureInfo.InstalledUICulture.TwoLetterISOLanguageName == "tr";
            EchoForge.WPF.Localization.TranslationManager.Instance.CurrentLanguage = isTurkish ? "tr" : "en";

            await CheckForUpdatesAsync();
        }
        catch (Exception ex)
        {
            File.WriteAllText("crash_log_startup.txt", ex.ToString());
            MessageBox.Show("Startup Crash: " + ex.Message);
        }
    }

    private async Task CheckForUpdatesAsync()
    {
        try
        {
            // Wait a bit for MainWindow to fully load
            await Task.Delay(2000);

            const string updateUrl = "https://vibeoracle.cloud/updates/latest.json";
            
            var updater = new Services.UpdateService(updateUrl);
            var info = await updater.CheckForUpdateAsync();

            if (info.Available)
            {
                // Show question on UI thread and wait for answer
                bool userAccepted = false;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    var msg = $"A new version (v{info.LatestVersion}) is available.\n\nRelease Notes:\n{info.ReleaseNotes}\n\nWould you like to install it now?";
                    var result = Views.EchoMessageBox.Show(msg, "Update Available", Views.EchoMessageBox.EchoMessageType.Question);
                    var r = result.ToString();
                    userAccepted = r == "Yes" || r == "OK" || r == "True" || r == "Confirm";
                });

                if (userAccepted)
                {
                    // Open progress popup
                    Views.UpdateProgressWindow? progressWindow = null;
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        progressWindow = new Views.UpdateProgressWindow(info.LatestVersion);
                        progressWindow.Show();
                    });

                    var zipPath = await updater.DownloadUpdateAsync(info, progress =>
                    {
                        var pct = progress * 100;
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            progressWindow?.UpdateProgress(pct, $"Downloading... {pct:F0}%");
                        });
                    });

                    if (!string.IsNullOrEmpty(zipPath))
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            progressWindow?.SetCompleted();
                        });
                        await Task.Delay(800);
                        updater.InstallAndRestart(zipPath);
                    }
                    else
                    {
                        Application.Current.Dispatcher.Invoke(() =>
                        {
                            progressWindow?.SetError("Download failed.");
                        });
                    }
                }
            }
        }
        catch (Exception)
        {
            // Silent failure on startup — don't interrupt user if server is offline
        }
    }
}
