using System;

namespace SS14.Launcher;

/// <summary>
/// A stable, language-independent identifier for a launcher failure.
///
/// Prefixed so automated things can pick up on it more easily.
/// </summary>
public readonly record struct ErrorCode(string Area, int Number)
{
    public const string Prefix = "SS14L";

    public override string ToString() => $"{Prefix}-{Area}-{Number:D3}";
}

/// <summary>
/// Implemented by exceptions that know which <see cref="ErrorCode"/> describes them.
/// </summary>
public interface IErrorCoded
{
    ErrorCode Code { get; }
}
