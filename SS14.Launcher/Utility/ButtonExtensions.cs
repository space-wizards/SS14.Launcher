using System.Threading.Tasks;
using Avalonia.Controls;
using Splat;
using SS14.Launcher.Localization;

namespace SS14.Launcher.Utility;

public static class ButtonExtensions
{
    /// <summary>
    /// Sets the content of a button to a specified message ("Done!" by default) for a specified duration (2s by
    /// default), and disabled the button for this duration.
    /// </summary>
    public static async Task DisplayDoneMessage(this Button button, string? message = null, int duration = 2000)
    {
        if (message == null)
        {
            var locMgr = Locator.Current.GetService<LocalizationManager>()!;
            message = locMgr.GetString("button-done");
        }

        var previousContent = button.Content;
        button.Content = message;
        button.IsEnabled = false;
        await Task.Delay(duration);
        button.Content = previousContent;
        button.IsEnabled = true;
    }
}
