using SS14.Launcher.Localization;

namespace SS14.Launcher;

/// <summary>
/// Short, translated advice that goes with an <see cref="ErrorCode"/>, such as "free up some disk space".
/// </summary>
public static class ErrorHints
{
    public static string GetKey(ErrorCode code)
    {
        return $"error-hint-{code.Area.ToLowerInvariant()}-{code.Number:D3}";
    }

    public static string? Get(ErrorCode code, LocalizationManager loc)
    {
        var key = GetKey(code);
        var text = loc.GetString(key);

        // Asking for a string that doesn't exist gives back its name.
        return text == key ? null : text;
    }
}
