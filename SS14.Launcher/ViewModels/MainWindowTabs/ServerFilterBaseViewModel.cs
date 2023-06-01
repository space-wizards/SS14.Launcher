using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class ServerFilterBaseViewModel : ObservableObject
{
    public ServerFilter Filter { get; protected set; }
    protected readonly ServerListFiltersViewModel _parent;

    public string Name { get; }
    public string ShortName { get; }

    public ServerFilterBaseViewModel(
        string name,
        string shortName,
        ServerFilter filter,
        ServerListFiltersViewModel parent)
    {
        Filter = filter;
        _parent = parent;
        Name = name;
        ShortName = shortName;
    }
}
