using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using SS14.Launcher.ViewModels;
using static SS14.Launcher.ViewModels.HubSettingsViewModel;

namespace SS14.Launcher.Views;

public partial class HubSettingsDialog : Window
{
    private readonly HubSettingsViewModel _viewModel;

    public HubSettingsDialog()
    {
        InitializeComponent();

#if DEBUG
        this.AttachDevTools();
#endif

        _viewModel = (DataContext as HubSettingsViewModel)!; // Should have been set in XAML
        _viewModel.HubList.CollectionChanged += (_, _) => Verify();
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);

        _viewModel.Populate();
        Verify(); // Just in case the settings are messed up somehow.
    }

    private void Done(object? sender, RoutedEventArgs args)
    {
        _viewModel.Save();
        Close();
    }

    private void Cancel(object? sender, RoutedEventArgs args) => Close();

    private void HubTextChanged(object? sender, AvaloniaPropertyChangedEventArgs e) => Verify();

    private void Verify()
    {
        var dupes = _viewModel.GetDupes();

        foreach (var t in Hubs.GetLogicalDescendants().OfType<TextBox>())
        {
            if (!IsValidHubUri(t.Text))
                t.Classes.Add("Invalid");
            else
                t.Classes.Remove("Invalid");

            if (dupes.Contains(NormalizeHubUri(t.Text)))
                t.Classes.Add("Duplicate");
            else
                t.Classes.Remove("Duplicate");
        }

        var anyHubs = Hubs.ItemCount > 0;
        var allValid = _viewModel.HubList.All(h => IsValidHubUri(h.Address));
        var noDupes = !dupes.Any();

        DoneButton.IsEnabled = anyHubs && allValid && noDupes;

        if (!anyHubs)
            Warning.Text = "Specify at least one hub";
        else if (!allValid)
            Warning.Text = "Invalid hub (don't forget http(s)://)";
        else if (!noDupes)
            Warning.Text = "Duplicate hubs";
        else
            Warning.Text = "";
    }
}
