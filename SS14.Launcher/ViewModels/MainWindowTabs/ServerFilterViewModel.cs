using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class ServerFilterViewModel : ServerFilterBaseViewModel
{

    public bool Selected
    {
        get => _parent.FilterExists(Filter);
        set => _parent.SetFilter(Filter, value);
    }

    public ServerFilterViewModel(
        string name,
        string shortName,
        ServerFilter filter,
        ServerListFiltersViewModel parent) : base(name, shortName, filter, parent)
    {
    }
}
