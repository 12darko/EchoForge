using System.Windows;
using System.Windows.Controls;
using EchoForge.WPF.ViewModels;

namespace EchoForge.WPF.Views;

public partial class CreateProjectView : UserControl
{
    public CreateProjectView()
    {
        InitializeComponent();
    }

    private void StyleButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Tag is string style && DataContext is CreateProjectViewModel vm)
        {
            vm.ImageStyle = style;
        }
    }
}
