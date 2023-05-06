using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class ServerFilterViewModel : ObservableObject
{
    public ServerFilter Filter { get; }
    private readonly ServerListFiltersViewModel _parent;

    public string Name { get; }
    public string ShortName { get; }

    public bool Selected
    {
        get => _parent.GetFilter(Filter);
        set => _parent.SetFilter(Filter, value);
    }

    public ServerFilterViewModel(
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
