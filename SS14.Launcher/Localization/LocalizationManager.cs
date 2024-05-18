using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Avalonia.Platform;
using Linguini.Bundle;
using Linguini.Bundle.Builder;
using Linguini.Syntax.Ast;
using Linguini.Syntax.Parser;
using Serilog;
using SS14.Launcher.Models.Data;

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
}
