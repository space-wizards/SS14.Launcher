using ReactiveUI.Fody.Helpers;

namespace SS14.Launcher.ViewModels.Login;

public abstract class BaseLoginViewModel : ViewModelBase, IErrorOverlayOwner
{
    [Reactive] public bool Busy { get; protected set; }
    [Reactive] public string? BusyText { get; protected set; }
    [Reactive] public ViewModelBase? OverlayControl { get; set; }
    public MainWindowLoginViewModel ParentVM { get; }

    protected BaseLoginViewModel(MainWindowLoginViewModel parentVM)
    {
        ParentVM = parentVM;
    }

    public virtual void Activated()
    {

    }

    public virtual void OverlayOk()
    {
        OverlayControl = null;
    }
}
