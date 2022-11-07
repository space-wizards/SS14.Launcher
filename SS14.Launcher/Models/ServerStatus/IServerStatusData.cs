using System.ComponentModel;

namespace SS14.Launcher.Models.ServerStatus;

/// <summary>
///     Contains data about the status of a single server.
/// </summary>
public interface IServerStatusData : INotifyPropertyChanged
{
    /// <summary>
    ///     The address of the server this status is for.
    /// </summary>
    string Address { get; }

    /// <summary>
    ///     The name reported by the server's status API.
    /// </summary>
    string? Name { get; set; }

    /// <summary>
    ///     Name of the game/fork the server is running.
    /// </summary>
    string? GameName { get; set; }

    ServerStatusCode Status { get; set; }
    int PlayerCount { get; set; }
}
