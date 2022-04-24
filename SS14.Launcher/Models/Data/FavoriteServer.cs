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
    private double _raiseTime;

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
    /// Format is a Unix timestamp in seconds as this provides simple, fast comparison - this isn't meant to really be used as a date.
    /// Defaults to 0, this is fine.
    /// </summary>
    [JsonProperty(PropertyName = "raiseTime")]
    public double RaiseTime
    {
        get => _raiseTime;
        set => this.RaiseAndSetIfChanged(ref _raiseTime, value);
    }
}
