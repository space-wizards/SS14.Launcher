using System.Collections;
using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Collections;
using Avalonia.Controls;
using SS14.Launcher.ViewModels.MainWindowTabs;
using static System.ComponentModel.ListSortDirection;

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

        ServerGrid.Sorting += (_, args) =>
        {
            args.Handled = true; // Stop Avalonia from messing with our custom behavior

            if (ServerGrid.ItemsSource is not DataGridCollectionView view)
                return;

            var currentSort = view.SortDescriptions.FirstOrDefault();
            view.SortDescriptions.Clear(); // Start fresh

            // This key/path corresponds to what the user wants to sort
            var clickedKey = args.Column.SortMemberPath;

            ListSortDirection? direction = currentSort switch
            {
                // No column is sorted: set clicked column to ascending
                null => Ascending,
                // Clicked column does not match currently sorted column: set clicked column to ascending
                { PropertyPath: { } currentKey } when currentKey != clickedKey => Ascending,
                // Cycle through ascending -> descending -> no sort -> ascending
                { Direction: Ascending } => Descending,
                { Direction: Descending } => null,
                _ => Ascending,
            };

            if (direction is not { } d)
                return; // Do not set any sorting

            var comparer = ServerEntryViewModel.ComparerMapping[clickedKey];
            view.SortDescriptions.Add(DataGridSortDescription.FromPath(clickedKey, d, comparer));
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

    public static readonly DirectProperty<ServerList, IEnumerable> EntriesProperty =
        AvaloniaProperty.RegisterDirect<ServerList, IEnumerable>(nameof(Entries),
            l => l.Entries,
            (l, e) => l.Entries = e);

    public IEnumerable Entries
    {
        get => ServerGrid.ItemsSource;
        set
        {
            ServerGrid.ItemsSource = new DataGridCollectionView(value);
            ServerGrid.SelectedItem = null;
        }
    }
}
