using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace SS14.Launcher.ViewModels.Login;

public abstract partial class BaseLoginViewModel(MainWindowLoginViewModel parentVM) : ViewModelBase, IErrorOverlayOwner
{
    [ObservableProperty] private bool _busy;
    [ObservableProperty] private string? _busyText;
    [ObservableProperty] private ViewModelBase? _overlayControl;
    public MainWindowLoginViewModel ParentVM { get; } = parentVM;

    public virtual void Activated()
    {
    }

    public virtual void OverlayOk()
    {
        OverlayControl = null;
    }
}
