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

public enum GameRoundStatus
{
    Unknown,
    InLobby,
    InRound,
}
