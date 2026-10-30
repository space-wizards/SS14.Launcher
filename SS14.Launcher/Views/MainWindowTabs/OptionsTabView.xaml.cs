using System;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Microsoft.Toolkit.Mvvm.Messaging;
using Serilog;
using Splat;
using SS14.Launcher.Localization;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.MainWindowTabs;

namespace SS14.Launcher.Views.MainWindowTabs;

public partial class OptionsTabView : UserControl
{
    public OptionsTabView()
    {
        InitializeComponent();
    }

    private void Flip(object? o, RoutedEventArgs routedEventArgs)
    {
        var window = (Window?) VisualRoot;
        if (window == null)
            return;

        window.Classes.Add("DoAFlip");

        DispatcherTimer.RunOnce(() => { window.Classes.Remove("DoAFlip"); }, TimeSpan.FromSeconds(1));
    }

    public async void ClearEnginesPressed(object? _1, RoutedEventArgs _2)
    {
        try
        {
            ((OptionsTabViewModel)DataContext!).ClearEngines();
        }
        catch (Exception e)
        {
            ReportError(e, ErrorCodes.IoClearEngines, "error-overlay-title-clear-engines", "error-overlay-desc-clear-engines");
            return;
        }

        await ClearEnginesButton.DisplayDoneMessage();
    }

    public async void ClearServerContentPressed(object? _1, RoutedEventArgs _2)
    {
        bool blocked;
        try
        {
            blocked = !await ((OptionsTabViewModel)DataContext!).ClearServerContent();
        }
        catch (Exception e)
        {
            ReportError(e, ErrorCodes.DbClearContent, "error-overlay-title-clear-content", "error-overlay-desc-clear-content");
            return;
        }

        var locMgr = Locator.Current.GetService<LocalizationManager>()!;

        await ClearServerContentButton.DisplayDoneMessage(
            blocked ? locMgr.GetString("tab-options-clear-content-close-client") : null);
    }

    private static void ReportError(Exception e, ErrorCode fallback, string titleKey, string descriptionKey)
    {
        var code = ErrorClassifier.ClassifyPrecisely(e) ?? fallback;
        Log.Error(e, "Options action failed ({ErrorCode})", code);
        WeakReferenceMessenger.Default.Send(new ErrorOccurred(titleKey, descriptionKey, code));
    }

    private async void OpenHubSettings(object? sender, RoutedEventArgs args)
    {
        await new HubSettingsDialog().ShowDialog((Window)this.GetVisualRoot()!);
    }
}
