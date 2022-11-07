using System;
using System.Text.Json.Serialization;
using ReactiveUI;

namespace SS14.Launcher.Models.Data;

public sealed class FavoriteServer : ReactiveObject
{
    private string? _name;
    private string? _gameName;
    private DateTimeOffset _raiseTime;

    // For serialization.
    public FavoriteServer()
    {
        Address = default!;
    }

    public FavoriteServer(string? name, string address)
    {
        Name = name;
        Address = address;
    }

    public FavoriteServer(string? name, string gameName, string address)
    {
        Name = name;
        GameName = gameName;
        Address = address;
    }

    public FavoriteServer(string? name, string address, DateTimeOffset raiseTime)
    {
        Name = name;
        Address = address;
        RaiseTime = raiseTime;
    }

    [JsonPropertyName("name")]
    public string? Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    [JsonPropertyName("game_name")]
    public string? GameName
    {
        get => _gameName;
        set => this.RaiseAndSetIfChanged(ref _gameName, value);
    }

    [JsonPropertyName("address")]
    public string Address { get; private set; } // Need private set for JSON.NET to work.

    /// <summary>
    /// Used to infer an exact ordering for servers in a simple, compatible manner.
    /// Defaults to 0, this is fine.
    /// This isn't saved in JSON because the launcher apparently doesn't use JSON for these anymore.
    /// </summary>
    public DateTimeOffset RaiseTime
    {
        get => _raiseTime;
        set => this.RaiseAndSetIfChanged(ref _raiseTime, value);
    }
}
