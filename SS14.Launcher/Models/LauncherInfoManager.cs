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
        catch (HttpRequestException e)
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
        string[] AllowedVersions,
        Dictionary<string, string?> OverrideAssets
    );
}
