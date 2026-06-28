using System;

namespace SS14.Launcher.Models.ServerStatus;

public enum ServerStatusCode
{
    Offline,
    FetchingStatus,
    Online
}

public enum ServerStatusInfoCode
{
    NotFetched,
    Fetching,
    Error,
    Fetched
}

public abstract record GameRoundStatus : IComparable<GameRoundStatus>
{
    public int CompareTo(GameRoundStatus? other)
        => (this, other) switch
        {
            (InRound a, InRound b) => a.TimeElapsed.CompareTo(b.TimeElapsed),
            ({ } a, { } b) => Ordering(a).CompareTo(Ordering(b)),
            _ => 1,
        };

    private static int Ordering(GameRoundStatus status)
        => status switch
        {
            Unknown => 0,
            InLobby => 1,
            InRound => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, "You should add an ordering value"),
        };
}

public record Unknown : GameRoundStatus;
public record InLobby : GameRoundStatus;

public record InRound(DateTime RoundStartTime) : GameRoundStatus
{
    public TimeSpan TimeElapsed => DateTime.UtcNow.Subtract(RoundStartTime);
}
