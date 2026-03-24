using System;
using System.Windows;

namespace EchoForge.WPF.Views;

public partial class UpdateProgressWindow : Window
{
    public UpdateProgressWindow(string version)
    {
        InitializeComponent();
        VersionText.Text = $"Version {version}";
    }

    public void UpdateProgress(double percent, string statusMessage = "")
    {
        Dispatcher.Invoke(() =>
        {
            var pct = Math.Clamp(percent, 0, 100);
            PercentText.Text = $"{pct:F0}%";
            
            // Animate the fill bar width
            var maxWidth = ActualWidth - 64; // account for padding
            if (maxWidth > 0)
                ProgressFill.Width = maxWidth * (pct / 100.0);

            if (!string.IsNullOrEmpty(statusMessage))
                StatusText.Text = statusMessage;

            if (pct >= 100)
            {
                TitleText.Text = "Installing Update...";
                StatusText.Text = "🔄 Extracting files and restarting...";
            }
        });
    }

    public void SetCompleted()
    {
        Dispatcher.Invoke(() =>
        {
            TitleText.Text = "Update Complete!";
            StatusText.Text = "🔄 Restarting application...";
            PercentText.Text = "100%";
        });
    }

    public void SetError(string message)
    {
        Dispatcher.Invoke(() =>
        {
            TitleText.Text = "Update Failed";
            StatusText.Text = $"❌ {message}";
            PercentText.Text = "";
        });
    }
}
