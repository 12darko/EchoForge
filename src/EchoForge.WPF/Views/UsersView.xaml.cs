using System.Windows.Controls;

namespace EchoForge.WPF.Views;

public partial class UsersView : UserControl
{
    public UsersView()
    {
        InitializeComponent();
    }

    private void PasswordInput_PasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        if (DataContext is ViewModels.UsersViewModel vm)
        {
            vm.NewPassword = PasswordInput.Password;
        }
    }
}
