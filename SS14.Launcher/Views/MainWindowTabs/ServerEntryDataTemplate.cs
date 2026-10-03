using Avalonia.Controls;
using Avalonia.Controls.Templates;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public sealed class ServerEntryDataTemplate : IDataTemplate, IRecyclingDataTemplate
{
    public bool SupportsRecycling => true;

    public Control Build(object? data)
    {
        return new ServerEntryView();
    }

    public Control Build(object? data, Control? existing)
    {
        return existing as ServerEntryView ?? new ServerEntryView();
    }

    public bool Match(object? data)
    {
        return data is ServerEntryViewModel;
    }
}
