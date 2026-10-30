using System;
using System.IO;

namespace SS14.Launcher.Utility;

public static class TempFile
{
    public static FileStream CreateTempFile()
    {
        try
        {
            return new TempFileStream(Path.GetTempFileName());
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new LauncherException(
                "Could not create a temporary file",
                ErrorClassifier.ClassifyPrecisely(e) ?? ErrorCodes.IoTempFile,
                e);
        }
    }

    private sealed class TempFileStream : FileStream
    {
        public TempFileStream(string path) : base(path, FileMode.Open, FileAccess.ReadWrite)
        {
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);

            File.Delete(Name);
        }
    }
}
