using System.Threading;

namespace SS14.Launcher.Utility;

public static class Language
{
    public static bool UserHasLanguage(string language)
    {
        var thread = Thread.CurrentThread;

        return thread.CurrentCulture.TwoLetterISOLanguageName == language ||
               thread.CurrentUICulture.TwoLetterISOLanguageName == language;
    }
}
