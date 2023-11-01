using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SS14.Launcher.ViewModels.Login;

namespace SS14.Launcher.Views.Login;

public partial class RegisterView : UserControl
{
    public RegisterView()
    {
        InitializeComponent();

        NameBox.KeyDown += OnTextBoxKeyDown;
        EmailBox.KeyDown += OnTextBoxKeyDown;
        PasswordBox.KeyDown += OnTextBoxKeyDown;
        PasswordConfirmBox.KeyDown += OnTextBoxKeyDown;
    }

    private void OnTextBoxKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && DataContext is RegisterViewModel vm)
        {
            vm.OnRegisterInButtonPressed();
        }
    }
}
