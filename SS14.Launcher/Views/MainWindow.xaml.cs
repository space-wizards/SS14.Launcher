using System;
using System.Linq;
using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Win32;
using SS14.Launcher.ViewModels;
using TerraFX.Interop.Windows;
using IDataObject = Avalonia.Input.IDataObject;

namespace SS14.Launcher.Views;

public partial class MainWindow : Window
{
    private MainWindowViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();

        DarkMode();

#if DEBUG
        this.AttachDevTools();
#endif

        AddHandler(DragDrop.DragEnterEvent, DragEnter);
        AddHandler(DragDrop.DragLeaveEvent, DragLeave);
        AddHandler(DragDrop.DragOverEvent, DragOver);
        AddHandler(DragDrop.DropEvent, Drop);
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Control = null;
        }

        _viewModel = DataContext as MainWindowViewModel;

        if (_viewModel != null)
        {
            _viewModel.Control = this;
        }

        base.OnDataContextChanged(e);
    }

    private unsafe void DarkMode()
    {
        if (PlatformImpl is not WindowImpl windowImpl || Environment.OSVersion.Version.Build < 22000)
            return;

        var type = windowImpl.GetType();
        var prop = type.GetProperty("Hwnd", BindingFlags.NonPublic | BindingFlags.Instance);
        if (prop == null)
        {
            // No need to log a warning, PJB will notice when this breaks.
            return;
        }

        var hWnd = (HWND)(nint)prop.GetValue(windowImpl)!;

        COLORREF r = 0x00262121;
        Windows.DwmSetWindowAttribute(hWnd, 35, &r, (uint) sizeof(COLORREF));

        // Remove top margin of the window on Windows 11, since there's ample space after we recolor the title bar.
        var margin = HeaderPanel.Margin;
        HeaderPanel.Margin = new Thickness(margin.Left, 0, margin.Right, margin.Bottom);
    }

    private void Drop(object? sender, DragEventArgs args)
    {
        DragDropOverlay.IsVisible = false;

        if (!IsDragDropValid(args.Data))
            return;

        var fileName = GetDragDropFileName(args.Data)!;

        _viewModel!.Dropped(fileName);
    }

    private void DragOver(object? sender, DragEventArgs args)
    {
        if (!IsDragDropValid(args.Data))
        {
            args.DragEffects = DragDropEffects.None;
            return;
        }

        args.DragEffects = DragDropEffects.Link;
    }

    private void DragLeave(object? sender, RoutedEventArgs args)
    {
        DragDropOverlay.IsVisible = false;
    }

    private void DragEnter(object? sender, DragEventArgs args)
    {
        if (!IsDragDropValid(args.Data))
            return;

        DragDropOverlay.IsVisible = true;
    }

    private bool IsDragDropValid(IDataObject dataObject)
    {
        if (_viewModel == null)
            return false;

        if (GetDragDropFileName(dataObject) is not { } fileName)
            return false;

        return _viewModel.IsContentBundleDropValid(fileName);
    }

    private static string? GetDragDropFileName(IDataObject dataObject)
    {
        if (!dataObject.Contains(DataFormats.FileNames))
            return null;

        return dataObject.GetFileNames()?.SingleOrDefault();
    }
}
