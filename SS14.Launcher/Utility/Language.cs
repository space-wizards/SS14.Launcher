using System.Threading;

namespace SS14.Launcher.Utility;

public static class Language
{
    /// <summary>
    ///     Checks if the user's current culture or UI culture matches the given language.
    /// </summary>
    /// <param name="language">Two letter ISO language name, for example "ru".</param>
    /// <returns>true if either the user's current culture or UI culture match the given language.</returns>
    public static bool UserHasLanguage(string language)
    {
        var thread = Thread.CurrentThread;

        return thread.CurrentCulture.TwoLetterISOLanguageName == language ||
               thread.CurrentUICulture.TwoLetterISOLanguageName == language;
    }
}
