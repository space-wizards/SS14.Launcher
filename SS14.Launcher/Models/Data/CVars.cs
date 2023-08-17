using System;
using JetBrains.Annotations;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Models.Data;

/// <summary>
/// Contains definitions for all launcher configuration values.
/// </summary>
/// <remarks>
/// The fields of this class are automatically searched for all CVar definitions.
/// </remarks>
/// <see cref="DataManager"/>
[UsedImplicitly]
public static class CVars
{
    /// <summary>
    /// Default to using compatibility options for rendering etc,
    /// that are less likely to immediately crash on buggy drivers.
    /// </summary>
    public static readonly CVarDef<bool> CompatMode = CVarDef.Create("CompatMode", false);

    /// <summary>
    /// Run client with dynamic PGO.
    /// </summary>
    public static readonly CVarDef<bool> DynamicPgo = CVarDef.Create("DynamicPgo", true);

    /// <summary>
    /// On first launch, the launcher tells you that SS14 is EARLY ACCESS.
    /// This stores whether they dismissed that, though people will insist on pretending it defaults to true.
    /// </summary>
    public static readonly CVarDef<bool> HasDismissedEarlyAccessWarning
        = CVarDef.Create("HasDismissedEarlyAccessWarning", false);

    /// <summary>
    /// Disable checking engine build signatures when launching game.
    /// Only enable if you know what you're doing.
    /// </summary>
    /// <remarks>
    /// This is ignored on release builds, for security reasons.
    /// </remarks>
    public static readonly CVarDef<bool> DisableSigning = CVarDef.Create("DisableSigning", false);

    /// <summary>
    /// Enable logging of launched client instances to file.
    /// </summary>
    public static readonly CVarDef<bool> LogClient = CVarDef.Create("LogClient", false);

    /// <summary>
    /// Enable logging of launched client instances to file.
    /// </summary>
    public static readonly CVarDef<bool> LogLauncher = CVarDef.Create("LogLauncher", false);

    /// <summary>
    /// Verbose logging of launcher logs.
    /// </summary>
    public static readonly CVarDef<bool> LogLauncherVerbose = CVarDef.Create("LogLauncherVerbose", false);

    /// <summary>
    /// Enable multi-account support on release builds.
    /// </summary>
    public static readonly CVarDef<bool> MultiAccounts = CVarDef.Create("MultiAccounts", false);

    /// <summary>
    /// Currently selected login in the drop down.
    /// </summary>
    public static readonly CVarDef<string> SelectedLogin = CVarDef.Create("SelectedLogin", "");

    public static readonly CVarDef<string> Fingerprint = CVarDef.Create("Fingerprint", "");

    /// <summary>
    /// Maximum amount of TOTAL versions to keep in the content database.
    /// </summary>
    public static readonly CVarDef<int> MaxVersionsToKeep = CVarDef.Create("MaxVersionsToKeep", 15);

    /// <summary>
    /// Maximum amount of versions to keep of a specific fork ID.
    /// </summary>
    public static readonly CVarDef<int> MaxForkVersionsToKeep = CVarDef.Create("MaxForkVersionsToKeep", 3);

    /// <summary>
    /// Whether to display override assets (trans rights).
    /// </summary>
    public static readonly CVarDef<bool> OverrideAssets = CVarDef.Create("OverrideAssets", true);

    /// <summary>
    /// Stores the minimum player count value used by the "minimum player count" filter.
    /// </summary>
    /// <seealso cref="ServerFilter.PlayerCountMin"/>
    public static readonly CVarDef<int> FilterPlayerCountMinValue = CVarDef.Create("FilterPlayerCountMinValue", 0);

    /// <summary>
    /// Stores the maximum player count value used by the "maximum player count" filter.
    /// </summary>
    /// <seealso cref="ServerFilter.PlayerCountMax"/>
    public static readonly CVarDef<int> FilterPlayerCountMaxValue = CVarDef.Create("FilterPlayerCountMaxValue", 0);
}

/// <summary>
/// Base definition of a CVar.
/// </summary>
/// <seealso cref="DataManager"/>
/// <seealso cref="CVars"/>
public abstract class CVarDef
{
    public string Name { get; }
    public object? DefaultValue { get; }
    public Type ValueType { get; }

    private protected CVarDef(string name, object? defaultValue, Type type)
    {
        Name = name;
        DefaultValue = defaultValue;
        ValueType = type;
    }

    public static CVarDef<T> Create<T>(
        string name,
        T defaultValue)
        where T : notnull
    {
        return new CVarDef<T>(name, defaultValue);
    }
}

/// <summary>
/// Generic specialized definition of CVar definition.
/// </summary>
/// <typeparam name="T">The type of value stored in this CVar.</typeparam>
public sealed class CVarDef<T> : CVarDef
{
    public new T DefaultValue { get; }

    internal CVarDef(string name, T defaultValue) : base(name, defaultValue, typeof(T))
    {
        DefaultValue = defaultValue;
    }
}
