using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using SS14.Launcher.ViewModels.Login;

namespace SS14.Launcher.Views.Login;

public sealed partial class ForgotPasswordView : UserControl
{
    public ForgotPasswordView()
    {
        InitializeComponent();

        EmailBox.KeyDown += InputBoxOnKeyDown;
    }

    private void InputBoxOnKeyDown(object? sender, KeyEventArgs args)
    {
        if (args.Key == Key.Enter && DataContext is ForgotPasswordViewModel vm)
        {
            vm.SubmitPressed();
        }
    }
}
