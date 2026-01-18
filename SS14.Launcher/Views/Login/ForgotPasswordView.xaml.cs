using Avalonia.Controls;

namespace SS14.Launcher.Views.Login;

public sealed partial class ForgotPasswordView : UserControl
{
    public ForgotPasswordView()
    {
        InitializeComponent();

        EmailBox.TextChanged += (_, _) => SubmitButton.IsEnabled = EmailBox.Text?.Contains('@') ?? false;
    }
}
