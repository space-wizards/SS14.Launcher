using Avalonia.Controls;
using Avalonia.Interactivity;

namespace SS14.Launcher.Views.Login;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();
    }

    private void ToggleShowPassword(object? sender, RoutedEventArgs e)
    {
        PasswordBox.RevealPassword = ShowPassword.IsChecked ?? false;
    }
}
