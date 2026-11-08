using System;

namespace SS14.Launcher;

/// <summary>
/// A failure the launcher detected itself, with the <see cref="ErrorCode"/> that describes it.
/// </summary>
public sealed class LauncherException(string message, ErrorCode code, Exception? inner = null)
    : Exception(message, inner), IErrorCoded
{
    public ErrorCode Code { get; } = code;
}

/// <summary>
/// Sent through the messenger when something fails somewhere that has no UI of its own.
/// The main window shows it as an error overlay.
/// </summary>
/// <param name="TitleKey">Localization key for the overlay title.</param>
/// <param name="DescriptionKey">Localization key for the overlay text.</param>
/// <param name="Code">The error code to show.</param>
public sealed record ErrorOccurred(string TitleKey, string DescriptionKey, ErrorCode Code);
