using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
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

        var result = await window.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select replay or content bundle file",
            AllowMultiple = false,
            FileTypeFilter =
            [
                new FilePickerFileType("Replay or content bundle files")
                {
                    Patterns = ["*.zip", "*.rtbundle", "*.rtreplay"],
                    MimeTypes = ["application/zip", "x-scheme-handler/ss14", "x-scheme-handler/ss14s"],
                    // Retrived using "mdls -name kMDItemContentType file.zip/rtreplay/rtbundle"
                    // No I'm not tripping... THIS is apparently how macOS identifies our file extension.
                    AppleUniformTypeIdentifiers = ["public.zip-archive", "dyn.ah62d4rv4ge81e7dwqz2g22p3", "dyn.ah62d4rv4ge81e7dcsz1gk5df"]
                }
            ]
        });

        if (result.Count == 0) // Cancelled
            return;

        using var file = result[0];
        if (!mainVm.IsContentBundleDropValid(file))
        {
            // TODO: Report this nicely.
            return;
        }

        ConnectingViewModel.StartContentBundle(mainVm, file);
    }
}
