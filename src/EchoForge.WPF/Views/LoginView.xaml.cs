using System.Windows;
using System.Windows.Controls;

namespace EchoForge.WPF.Views;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
        // Wire PasswordBox since it doesn't support binding
        TxtPassword.PasswordChanged += (s, e) =>
        {
            if (DataContext is ViewModels.LoginViewModel vm)
                vm.Password = TxtPassword.Password;
        };

        // Pre-fill if remembered
        Loaded += (s, e) =>
        {
            if (DataContext is ViewModels.LoginViewModel vm && !string.IsNullOrEmpty(vm.Password))
                TxtPassword.Password = vm.Password;
        };
    }
}
