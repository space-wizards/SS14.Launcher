using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia.Platform;
using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Shared.Types.Bundle;
using Linguini.Syntax.Ast;
using Linguini.Syntax.Parser;
using Serilog;
using Splat;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Utility;

namespace SS14.Launcher.Localization;

public sealed class LocalizationManager
{
    private readonly DataManager _dataManager;

    private static readonly string Culture = "en-US";

    private FluentBundle _bundle = default!;

    public LocalizationManager(DataManager dataManager)
    {
        _dataManager = dataManager;
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

    public void Initialize()
    {
        LoadCulture(new CultureInfo(Culture));
    }

    private void LoadCulture(CultureInfo culture)
    {
        var resources = new List<Resource>();
        foreach (var ftl in AssetLoader.GetAssets(new Uri($"avares://SS14.Launcher/Assets/Locale/{culture.Name}"), null))
        {
            using var asset = AssetLoader.Open(ftl);
            using var reader = new StreamReader(asset, Encoding.UTF8);
            var resource = new LinguiniParser(reader).Parse();
            foreach (var resourceError in resource.Errors)
            {
                Log.Error("Error in loc {LocFile}: {Error}", ftl, resourceError);
            }
            resources.Add(resource);
        }

        var bundle = LinguiniBuilder.Builder()
            .CultureInfo(culture)
            .AddResources(resources)
            .SetUseIsolating(false)
            .UseConcurrent()
            .UncheckedBuild();

        _bundle = bundle;
    }

    public static LocalizationManager Instance => Locator.Current.GetRequiredService<LocalizationManager>();
}
