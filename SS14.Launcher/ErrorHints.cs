using SS14.Launcher.Localization;

namespace SS14.Launcher;

/// <summary>
/// Short, translated advice that goes with an <see cref="ErrorCode"/>, such as "free up some disk space".
/// </summary>
public static class ErrorHints
{
    /// <summary>
    /// The name of the string with the hint for this exact code.
    /// </summary>
    public static string GetKey(ErrorCode code)
    {
        return $"error-hint-{code.Area.ToLowerInvariant()}-{code.Number:D3}";
    }

    /// <summary>
    /// The name of the string with the generic hint for everything in this code's area, used when the code
    /// has no hint of its own.
    /// </summary>
    public static string GetAreaKey(ErrorCode code)
    {
        return $"error-hint-{code.Area.ToLowerInvariant()}";
    }

    /// <summary>
    /// The hint for a code, or if it has none the generic hint for its area, or null if there is neither.
    /// </summary>
    public static string? Get(ErrorCode code, LocalizationManager loc)
    {
        return Lookup(GetKey(code), loc) ?? Lookup(GetAreaKey(code), loc);
    }

    private static string? Lookup(string key, LocalizationManager loc)
    {
        var text = loc.GetString(key);

        // Asking for a string that doesn't exist gives back its name.
        return text == key ? null : text;
    }
}
