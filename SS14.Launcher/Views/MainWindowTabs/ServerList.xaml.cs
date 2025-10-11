using System.Collections;
using Avalonia;
using Avalonia.Controls;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public sealed partial class ServerList : UserControl
{
    public ServerList()
    {
        InitializeComponent();

        ServerGrid.SelectionChanged += (_, args) =>
        {
            foreach (ServerEntryViewModel rem in args.RemovedItems)
            {
                rem.IsExpanded = false;
            }

            foreach (ServerEntryViewModel add in args.AddedItems)
            {
                add.IsExpanded = true;
            }
        };
    }

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

    public static readonly DirectProperty<ServerList, string?> ListTextProperty =
        AvaloniaProperty.RegisterDirect<ServerList, string?>(
            nameof(ListText),
            o => o.ListText,
            (o, v) => o.ListText = v
        );

    private string? _listText;

    /// <summary>
    /// Optional text which will be displayed in the server list area.
    /// If null or empty no text will be added.
    /// </summary>
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

    public static readonly StyledProperty<IEnumerable> ListProperty =
        AvaloniaProperty.Register<ServerList, IEnumerable>(nameof(List));

    public IEnumerable List
    {
        get => ServerGrid.ItemsSource;
        set => ServerGrid.ItemsSource = value;
    }
}
