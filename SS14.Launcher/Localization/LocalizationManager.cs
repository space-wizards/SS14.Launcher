using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Avalonia.Platform;
using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Shared.Types.Bundle;
using Linguini.Syntax.Parser;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Localization;

public sealed class LocalizationManager
{
    private readonly DataManager _dataManager;

    public static readonly ImmutableArray<LanguageInfo> AvailableLanguages =
    [
        new LanguageInfo(FallbackCulture),
        new LanguageInfo("nl"),
        new LanguageInfo("el"),
        new LanguageInfo("de"),
        new LanguageInfo("ru"),
        new LanguageInfo("pt-BR"),
        new LanguageInfo("es"),
        new LanguageInfo("uk"),
        new LanguageInfo("fr"),
        new LanguageInfo("tr"),
        new LanguageInfo("sv"),
        new LanguageInfo("fi"),
        new LanguageInfo("zh-Hans"),
        new LanguageInfo("et"),
        new LanguageInfo("pl"),
        new LanguageInfo("ja"),
        new LanguageInfo("it"),
        new LanguageInfo("nb-NO"),
        new LanguageInfo("hu"),
        new LanguageInfo("eo"),
        new LanguageInfo("ro"),
    ];

    private const string FallbackCulture = "en";
    private const string FallbackCultureSub = "en-US";

    private FluentBundle _bundle = default!;

    public CultureInfo SystemCulture { get; private set; } = CultureInfo.InvariantCulture;

    public event Action? LanguageSwitched;

    public LocalizationManager(DataManager dataManager)
    {
        _dataManager = dataManager;
    }

    public void Initialize()
    {
        var currentUiCulture = CultureInfo.CurrentUICulture;
        Log.Debug("CurrentUICulture: {Culture}", currentUiCulture);
        SystemCulture = MatchCultureAgainstAvailable(currentUiCulture) ?? new CultureInfo(FallbackCulture);
        Log.Debug("Matched available system culture: {Culture}", SystemCulture);
        var setLanguage = _dataManager.GetCVar(CVars.Language);
        if (string.IsNullOrEmpty(setLanguage))
        {
            Log.Verbose("No language saved in options, using system culture");
            LoadCulture(SystemCulture);
        }
        else
        {
            Log.Verbose("Using culture from options: {Culture}", setLanguage);
            LoadCulture(new CultureInfo(setLanguage));
        }
    }

    public string GetString(string key)
    {
        return _bundle.GetMessage(key) ?? key;
    }

    public string GetString(string key, params (string, object?)[] args)
    {
        var argsDict = new Dictionary<string, IFluentType>(args.Length);

        foreach (var (argKey, argValue) in args)
        {
            argsDict.Add(argKey, ToFluentType(argValue));
        }

        return _bundle.GetMessage(key, args: argsDict) ?? key;
    }

    private static IFluentType ToFluentType(object? o)
    {
        return o switch
        {
            string s => new FluentString(s),
            float f => (FluentNumber)f,
            double d => (FluentNumber)d,
            int i => (FluentNumber)i,
            long l => (FluentNumber)l,
            null => FluentNone.None,
            _ => new FluentString(o.ToString())
        };
    }

    private void LoadCulture(CultureInfo culture)
    {
        Log.Debug("initializing localization for culture: {CultureName} ({CultureDisplayName})", culture.Name, culture.DisplayName);

        var bundle = LinguiniBuilder.Builder()
            .CultureInfo(culture)
            .SkipResources()
            .SetUseIsolating(false)
            .UseConcurrent()
            .UncheckedBuild();

        AddLanguageFiles(bundle, new CultureInfo(FallbackCultureSub));

        AddLanguageFiles(bundle, culture);

        _bundle = bundle;

        CultureInfo.CurrentUICulture = culture;
    }

    private void AddLanguageFiles(FluentBundle bundle, CultureInfo culture)
    {
        if (!culture.Parent.Equals(CultureInfo.InvariantCulture))
            AddLanguageFiles(bundle, culture.Parent);

        var count = 0;
        string[] attemptNames = [$"avares://SS14.Launcher/Assets/Locale/{culture.Name}"];
        // Weblate stores secondary language codes (like zh-Hans) with an UNDERSCORE.
        // WHY.
        if (culture.Name.Contains('-'))
            attemptNames = [..attemptNames, $"avares://SS14.Launcher/Assets/Locale/{culture.Name.Replace("-", "_")}"];

        foreach (var location in attemptNames)
        {
            foreach (var ftl in AssetLoader.GetAssets(new Uri(location), null))
            {
                using var asset = AssetLoader.Open(ftl);
                using var reader = new StreamReader(asset, Encoding.UTF8);
                var resource = new LinguiniParser(reader).Parse();
                foreach (var resourceError in resource.Errors)
                {
                    Log.Error("Error in loc {LocFile}: {Error}", ftl, resourceError);
                }
                bundle.AddResourceOverriding(resource);
                count += 1;
            }
        }

        Log.Verbose("Loaded {Count} files for locale: {CultureName} ({CultureDisplayName})", count, culture.Name, culture.DisplayName);
    }

    public void SwitchToLanguage(CultureInfo? culture)
    {
        LoadCulture(culture ?? SystemCulture);
        // Trigger re-creation of main window view which will refresh localization strings everywhere.
        LanguageSwitched?.Invoke();
    }

    private static CultureInfo? MatchCultureAgainstAvailable(CultureInfo culture)
    {
        foreach (var parent in EnumerateParents(culture))
        {
            if (AvailableLanguages.Any(lang => lang.Name == parent.Name))
            {
                return parent;
            }
        }

        return null;
    }

    private static IEnumerable<CultureInfo> EnumerateParents(CultureInfo culture)
    {
        while (!culture.Equals(CultureInfo.InvariantCulture))
        {
            yield return culture;
            culture = culture.Parent;
        }
    }

    public static LocalizationManager Instance => Locator.Current.GetRequiredService<LocalizationManager>();

    // ReSharper disable once NotAccessedPositionalProperty.Global
    public sealed record LanguageInfo(string Name)
    {
        public CultureInfo Culture { get; } = new(Name);
    }
}
