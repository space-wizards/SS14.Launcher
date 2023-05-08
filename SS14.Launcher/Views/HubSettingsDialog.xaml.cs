using System;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using SS14.Launcher.ViewModels;

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
        string Normalize(string address)
        {
            if (!Uri.TryCreate(address, UriKind.Absolute, out var uri))
                return address;

            if (!uri.AbsoluteUri.EndsWith('/'))
            {
                return uri.AbsoluteUri + '/';
            }

            return uri.AbsoluteUri;
        }

        var dupes = _viewModel.HubList.GroupBy(h => Normalize(h.Address))
            .Where(group => group.Count() > 1)
            .Select(x => x.Key)
            .ToList();

        foreach (var t in Hubs.GetLogicalDescendants().OfType<TextBox>())
        {
            if (!HubSettingsViewModel.IsValidHubUri(t.Text))
                t.Classes.Add("Invalid");
            else
                t.Classes.Remove("Invalid");

            if (dupes.Contains(Normalize(t.Text)))
                t.Classes.Add("Duplicate");
            else
                t.Classes.Remove("Duplicate");
        }

        var allValid = _viewModel.HubList.All(h => HubSettingsViewModel.IsValidHubUri(h.Address));
        var noDupes = !dupes.Any();

        DoneButton.IsEnabled = allValid && noDupes;

        if (!allValid)
            Warning.Text = "Invalid hub (don't forget http(s)://)";
        else if (!noDupes)
            Warning.Text = "Duplicate hubs";
        else
            Warning.Text = "";
    }
}
