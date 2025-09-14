using System;
using Splat;

namespace SS14.Launcher.Localization;

public sealed class LocExtension
{
    public string Key { get; }

    public LocExtension(string key)
    {
        Key = key;
    }

    public object ProvideValue(IServiceProvider services)
    {
        if (Key == "server-entry-connect")
        {
            return "Connect!";
        }

        var locMgr = Locator.Current.GetService<LocalizationManager>()!;
        return locMgr.GetString(Key);
    }
}
