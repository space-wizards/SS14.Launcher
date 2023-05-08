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

    private void HubTextChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (sender is not TextBox textBox || e.Property != TextBox.TextProperty)
            return;

        if (!HubSettingsViewModel.IsValidHubUri(textBox.Text))
            textBox.Classes.Add("Invalid");
        else
            textBox.Classes.Remove("Invalid");

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

        if (textBox.Parent?.Parent?.Parent is { } stack)
        {
            foreach (var t in stack.GetLogicalDescendants().OfType<TextBox>())
            {
                if (dupes.Contains(Normalize(t.Text)))
                    t.Classes.Add("Duplicate");
                else
                    t.Classes.Remove("Duplicate");
            }
        }

        var allValid = _viewModel.HubList.All(h => HubSettingsViewModel.IsValidHubUri(h.Address));
        var noDupes = !dupes.Any();

        DoneButton.IsEnabled = allValid && noDupes;

        if (!noDupes)
            Warning.Text = "Duplicate hubs";
        else if (!allValid)
            Warning.Text = "Invalid hub (don't forget http(s)://)";
        else
            Warning.Text = "";
    }
}
