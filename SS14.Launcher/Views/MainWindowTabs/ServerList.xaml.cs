using System;
using Avalonia;
using Avalonia.Controls.Primitives;
using System.Collections.Generic;
using Serilog;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public sealed partial class ServerList : TemplatedControl
{
    public static readonly DirectProperty<ServerList, bool> ShowHeaderProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ShowHeader),
            o => o.ShowHeader,
            (o, v) => o.ShowHeader = v
        );

    private bool _showHeader;

    public bool ShowHeader
    {
        get => _showHeader;
        set => SetAndRaise(ShowHeaderProperty, ref _showHeader, value);
    }

    public static readonly DirectProperty<ServerList, bool> ListTextVisibleProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(ListTextVisible),
            o => o.ListTextVisible,
            (o, v) => o.ListTextVisible = v
        );

    private bool _listTextVisible;

    public bool ListTextVisible
    {
        get => _listTextVisible;
        set => SetAndRaise(ListTextVisibleProperty, ref _listTextVisible, value);
    }

    // TODO No need for two properties for this, the nullable string should be enough
    public static readonly DirectProperty<ServerList, string?> ListTextProperty =
        AvaloniaProperty.RegisterDirect<ServerList, string?>(
            nameof(ListText),
            o => o.ListText,
            (o, v) => o.ListText = v
        );

    private string? _listText;

    public string? ListText
    {
        get => _listText;
        set => SetAndRaise(ListTextProperty, ref _listText, value);
    }

    public static readonly DirectProperty<ServerList, bool> SpinnerVisibleProperty =
        AvaloniaProperty.RegisterDirect<ServerList, bool>(
            nameof(SpinnerVisible),
            o => o.SpinnerVisible,
            (o, v) => o.SpinnerVisible = v
        );

    private bool _spinnerVisible;

    public bool SpinnerVisible
    {
        get => _spinnerVisible;
        set => SetAndRaise(SpinnerVisibleProperty, ref _spinnerVisible, value);
    }

    public static readonly DirectProperty<ServerList, IReadOnlyCollection<ServerEntryViewModel>> ListProperty =
        AvaloniaProperty.RegisterDirect<ServerList, IReadOnlyCollection<ServerEntryViewModel>>(
            nameof(List),
            o => o.List,
            (o, v) => o.List = v
        );

    private IReadOnlyCollection<ServerEntryViewModel> _serverList = Array.Empty<ServerEntryViewModel>();

    public IReadOnlyCollection<ServerEntryViewModel> List
    {
        get => _serverList;
        set => SetAndRaise(ListProperty, ref _serverList, value);
    }
}
