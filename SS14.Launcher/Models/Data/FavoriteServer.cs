using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace SS14.Launcher.Models.Data;

public sealed partial class FavoriteServer : ObservableObject
{
    public readonly string? Name;

    public readonly string Address;

    /// <summary>
    /// Used to infer an exact ordering for servers in a simple, compatible manner.
    /// Defaults to 0, this is fine.
    /// </summary>
    [ObservableProperty] private DateTimeOffset _raiseTime;

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
}
