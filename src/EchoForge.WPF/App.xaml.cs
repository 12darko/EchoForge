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
            // Dynamically construct update URL from ServerConfig instead of ApiClient.Instance which might be null early on
            var apiBase = Services.ServerConfig.GetServerUrl().TrimEnd('/');
            var updateUrl = $"{apiBase}/api/update/check"; 
            
            var updater = new Services.UpdateService(updateUrl);
            var info = await updater.CheckForUpdateAsync();

            if (info.Available)
            {
                var msg = $"A new version (v{info.LatestVersion}) is available.\n\nRelease Notes:\n{info.ReleaseNotes}\n\nWould you like to install it now?";
                var result = Views.EchoMessageBox.Show(msg, "Update Available", Views.EchoMessageBox.EchoMessageType.Question);

                if (result.ToString() == "Yes" || result.ToString() == "OK" || result.ToString() == "True" || result.ToString() == "Confirm")
                {
                    Views.EchoMessageBox.Show("Downloading update. The application will restart automatically. Please wait...", "Downloading", Views.EchoMessageBox.EchoMessageType.Info);
                    
                    var zipPath = await updater.DownloadUpdateAsync(info);
                    if (!string.IsNullOrEmpty(zipPath))
                    {
                        updater.InstallAndRestart(zipPath);
                    }
                    else
                    {
                        Views.EchoMessageBox.Show("Failed to download the update.", "Update Error", Views.EchoMessageBox.EchoMessageType.Error);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            // Show error so we know why it's failing during test
            Views.EchoMessageBox.Show("Update check failed: " + ex.Message, "Debug Error", Views.EchoMessageBox.EchoMessageType.Error);
        }
    }
}
