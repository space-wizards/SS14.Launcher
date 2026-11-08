using System;

namespace SS14.Launcher.Utility;

/// <summary>
/// A download failed in a way we can name: the server refused it, or it was cut off partway.
/// </summary>
public sealed class DownloadException(string message, ErrorCode code, Exception? inner = null)
    : Exception(message, inner), IErrorCoded
{
    public ErrorCode Code { get; } = code;
}
