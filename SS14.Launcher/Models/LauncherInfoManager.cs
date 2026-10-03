using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using Serilog;

namespace SS14.Launcher.Models;

/// <summary>
/// Fetches and caches information from <see cref="ConfigConstants.UrlLauncherInfo"/>.
/// </summary>
public sealed class LauncherInfoManager(HttpClient httpClient)
{
    private readonly Random _messageRandom = new();
    private string[]? _messages;

    private LauncherInfoModel? _model;

    public LauncherInfoModel? Model
    {
        get
        {
            if (!LoadTask.IsCompleted)
                throw new InvalidOperationException("Data has not been loaded yet");

            return _model;
        }
    }

    public Task LoadTask { get; private set; } = default!;

    public void Initialize()
    {
        LoadTask = LoadData();
    }

    private async Task LoadData()
    {
        LauncherInfoModel? info;
        try
        {
            Log.Debug("Loading launcher info... {Url}", ConfigConstants.UrlLauncherInfo);
            info = await ConfigConstants.UrlLauncherInfo.GetFromJsonAsync<LauncherInfoModel>(httpClient);
            if (info == null)
            {
                Log.Warning("Launcher info response was null.");
                return;
            }
        }
        catch (Exception e)
        {
            Log.Warning(e, "Loading launcher info failed");
            return;
        }

        // This is future-proofed to support multiple languages,
        // but for now the launcher only supports English so it'll have to do.
        info.Messages.TryGetValue("en-US", out _messages);

        _model = info;
    }

    public string? GetRandomMessage()
    {
        if (_messages == null)
            return null;

        return _messages[_messageRandom.Next(_messages.Length)];
    }

    public sealed record LauncherInfoModel(
        Dictionary<string, string[]> Messages,
        string MinVersion,
        string[] BlockedVersions,
        Dictionary<string, string?> OverrideAssets
    );

    /// <summary>
    /// Checks whether the running launcher version is allowed by the remote
    /// <see cref="LauncherInfoModel.MinVersion"/> and <see cref="LauncherInfoModel.BlockedVersions"/>.
    /// </summary>
    public static bool IsVersionAllowed(LauncherInfoModel info, Version current)
    {
        current = Normalize(current);

        if (!Version.TryParse(info.MinVersion, out var min))
        {
            Log.Warning("Unable to parse remote minimum launcher version '{MinVersion}', ignoring it.", info.MinVersion);
        }
        else if (current < Normalize(min))
        {
            return false;
        }

        foreach (var blockedStr in info.BlockedVersions)
        {
            if (!Version.TryParse(blockedStr, out var blocked))
            {
                Log.Warning("Unable to parse remote blocked launcher version '{Blocked}', ignoring it.", blockedStr);
                continue;
            }

            if (current == Normalize(blocked))
                return false;
        }

        return true;
    }

    // Needed to parse the versioning we use
    private static Version Normalize(Version v)
    {
        return new Version(v.Major, v.Minor, Math.Max(v.Build, 0), Math.Max(v.Revision, 0));
    }
}
