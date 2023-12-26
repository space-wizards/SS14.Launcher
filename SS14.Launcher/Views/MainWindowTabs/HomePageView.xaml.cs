using System;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Serilog;
using SS14.Launcher.ViewModels;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class HomePageView : UserControl
{
    private HomePageViewModel? _viewModel;

    public HomePageView()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Control = null;
        }

        _viewModel = DataContext as HomePageViewModel;

        if (_viewModel != null)
        {
            _viewModel.Control = this;
        }

        base.OnDataContextChanged(e);
    }

    private async void OpenReplayClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel?.MainWindowViewModel is not { } mainVm)
            return;

        if (this.GetVisualRoot() is not Window window)
        {
            Log.Error("Visual root isn't a window!");
            return;
        }

        var dialog = new OpenFileDialog
        {
            Filters = new List<FileDialogFilter>
            {
                new() { Extensions = new List<string> { "zip" }, Name = "Content bundle files" }
            },
            Title = "Open replay or content bundle"
        };

        var result = await dialog.ShowAsync(window);
        if (result == null || result.Length == 0) // Canceled
            return;

        var file = result[0];
        if (!mainVm.IsContentBundleDropValid(file))
        {
            // TODO: Report this nicely.
            return;
        }

        ConnectingViewModel.StartContentBundle(mainVm, file);
    }
}
