using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public class ServerFilterViewModel : ObservableObject
{
    public ServerFilter Filter { get; }
    protected readonly ServerListFiltersViewModel Parent;

    public string Name { get; }
    public string ShortName { get; }

    public bool Selected
    {
        get => Parent.GetFilter(Filter);
        set
        {
            Parent.SetFilter(Filter, value);
            OnPropertyChanged();
        }
    }

    public ServerFilterViewModel(
        string name,
        string shortName,
        ServerFilter filter,
        ServerListFiltersViewModel parent)
    {
        Filter = filter;
        Parent = parent;
        Name = name;
        ShortName = shortName;
    }
}

public sealed class ServerFilterCounterViewModel : ServerFilterViewModel
{
    public ICVarEntry<int> CVar { get; }

    public int CounterValue
    {
        get => CVar.Value;
        set
        {
            CVar.Value = value;
            Parent.CounterUpdated();
        }
    }

    public ServerFilterCounterViewModel(
        string name,
        string shortName,
        ServerFilter filter,
        ICVarEntry<int> cVar,
        ServerListFiltersViewModel parent) : base(name, shortName, filter, parent)
    {
        CVar = cVar;
    }
}
