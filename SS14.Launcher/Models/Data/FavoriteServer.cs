using System;
using Newtonsoft.Json;
using ReactiveUI;

namespace SS14.Launcher.Models.Data;

[Serializable]
// Without OptIn JSON.NET chokes on ReactiveObject.
[JsonObject(MemberSerialization.OptIn)]
public sealed class FavoriteServer : ReactiveObject
{
    private string? _name;
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

    public FavoriteServer(string? name, string address, DateTimeOffset raiseTime)
    {
        Name = name;
        Address = address;
        RaiseTime = raiseTime;
    }

    [JsonProperty(PropertyName = "name")]
    public string? Name
    {
        get => _name;
        set => this.RaiseAndSetIfChanged(ref _name, value);
    }

    [JsonProperty(PropertyName = "address")]
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
