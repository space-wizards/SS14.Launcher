using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using SS14.Launcher.ViewModels.Login;
using YamlDotNet.Core;

namespace SS14.Launcher.Views.Login;

public sealed partial class AuthErrorsOverlayView : UserControl
{
    public AuthErrorsOverlayView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        OkButton.Focus();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (e.Key == Key.Enter && DataContext is AuthErrorsOverlayViewModel vm)
        {
            vm.Ok();
        }
    }
}
