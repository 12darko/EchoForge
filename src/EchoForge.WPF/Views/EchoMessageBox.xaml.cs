using System.Windows;

namespace EchoForge.WPF.Views;

public partial class EchoMessageBox : Window
{
    public MessageBoxResult Result { get; private set; } = MessageBoxResult.Cancel;

    private EchoMessageBox()
    {
        InitializeComponent();
        // Allow dragging the window
        MouseLeftButtonDown += (_, _) => DragMove();
    }

    private void BtnOk_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.OK;
        Close();
    }

    private void BtnCancel_Click(object sender, RoutedEventArgs e)
    {
        Result = MessageBoxResult.Cancel;
        Close();
    }

    // ─── Static API ─────────────────────────────────────────

    public enum EchoMessageType
    {
        Info,
        Success,
        Warning,
        Error,
        Question
    }

    /// <summary>
    /// Show a glassmorphic message box.
    /// </summary>
    public static MessageBoxResult Show(string message, string title = "EchoForge",
        EchoMessageType type = EchoMessageType.Info, bool showCancel = false)
    {
        var dlg = new EchoMessageBox();
        dlg.TitleText.Text = title;
        dlg.MessageText.Text = message;

        switch (type)
        {
            case EchoMessageType.Info:
                dlg.IconText.Text = "ℹ️";
                break;
            case EchoMessageType.Success:
                dlg.IconText.Text = "✅";
                break;
            case EchoMessageType.Warning:
                dlg.IconText.Text = "⚠️";
                break;
            case EchoMessageType.Error:
                dlg.IconText.Text = "❌";
                break;
            case EchoMessageType.Question:
                dlg.IconText.Text = "❓";
                dlg.BtnOk.Content = "Yes";
                dlg.BtnCancel.Content = "No";
                showCancel = true;
                break;
        }

        if (showCancel)
        {
            dlg.BtnCancel.Visibility = Visibility.Visible;
        }

        // Try to set owner
        if (Application.Current?.MainWindow != null && Application.Current.MainWindow != dlg)
        {
            dlg.Owner = Application.Current.MainWindow;
        }

        dlg.ShowDialog();
        return dlg.Result;
    }
}
