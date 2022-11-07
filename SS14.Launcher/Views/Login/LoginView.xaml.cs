using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SS14.Launcher.ViewModels.Login;

namespace SS14.Launcher.Views.Login;

public partial class LoginView : UserControl
{
    public LoginView()
    {
        InitializeComponent();

        var nameBox = this.FindControl<TextBox>("NameBox");
        var passwordBox = this.FindControl<TextBox>("PasswordBox");

        nameBox.KeyDown += InputBoxOnKeyDown;
        passwordBox.KeyDown += InputBoxOnKeyDown;
    }

    private void InputBoxOnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && DataContext is LoginViewModel vm)
        {
            vm.OnLogInButtonPressed();
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }
}
