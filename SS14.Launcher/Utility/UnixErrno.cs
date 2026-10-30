using System;

namespace SS14.Launcher.Utility;

/// <summary>
/// Which family of Unix error numbers is in use. Linux and the BSDs (including macOS) agree on the classic
/// numbers but not on a handful of others, such as the "disk quota" and "name too long" errors.
///
/// Please for the love of god don't tell me there is more that I missed, compiling this list already sucked...
/// </summary>
public enum UnixFlavor
{
    Linux,

    /// <summary>macOS and FreeBSD.</summary>
    Bsd
}

/// <summary>
/// Maps Unix error numbers (errno) to <see cref="ErrorCode"/>s. .NET puts the errno in
/// <see cref="System.IO.IOException.HResult"/> and <see cref="System.ComponentModel.Win32Exception.NativeErrorCode"/>
/// on Unix, where Windows would give an HRESULT or Win32 error code. Because why make it easy, right?
/// </summary>
public static class UnixErrno
{
    // Identical on Linux, macOS and FreeBSD.
    private const int EPERM = 1;
    private const int ENOEXEC = 8;
    private const int EACCES = 13;
    private const int EBUSY = 16;
    private const int ENFILE = 23;
    private const int EMFILE = 24;
    private const int ETXTBSY = 26;
    private const int ENOSPC = 28;
    private const int EROFS = 30;

    // Different between Linux and the BSDs.
    private const int EagainLinux = 11;
    private const int EagainBsd = 35;
    private const int EnametoolongLinux = 36;
    private const int EnametoolongBsd = 63;
    private const int EdquotLinux = 122;
    private const int EdquotBsd = 69;

    /// <summary>
    /// The flavor of the Unix we are running on.
    /// </summary>
    public static UnixFlavor? CurrentFlavor
    {
        get
        {
            if (OperatingSystem.IsWindows())
                return null;

            return OperatingSystem.IsLinux() ? UnixFlavor.Linux : UnixFlavor.Bsd;
        }
    }

    /// <summary>
    /// The code for an errno carried by an <see cref="System.IO.IOException"/>, or null if it isn't one we recognise.
    /// </summary>
    public static ErrorCode? ClassifyIo(int errno, UnixFlavor flavor)
    {
        var linux = flavor == UnixFlavor.Linux;

        // File locks report "would block" when someone else holds the file.
        var wouldBlock = linux ? EagainLinux : EagainBsd;
        var nameTooLong = linux ? EnametoolongLinux : EnametoolongBsd;
        var quota = linux ? EdquotLinux : EdquotBsd;

        if (errno == EBUSY || errno == ETXTBSY || errno == wouldBlock)
            return ErrorCodes.IoInUse;

        if (errno == ENOSPC || errno == quota)
            return ErrorCodes.IoDiskFull;

        if (errno == EROFS)
            return ErrorCodes.IoReadOnly;

        if (errno == EMFILE || errno == ENFILE)
            return ErrorCodes.IoTooManyFiles;

        if (errno == nameTooLong)
            return ErrorCodes.IoPathTooLong;

        if (errno == EACCES || errno == EPERM)
            return ErrorCodes.IoAccessDenied;

        return null;
    }

    /// <summary>
    /// The code for an errno from starting the game loader (<c>Process.Start</c>), or null if it isn't one we recognise.
    /// </summary>
    public static ErrorCode? ClassifyProcessStart(int errno)
    {
        return errno switch
        {
            EACCES or EPERM => ErrorCodes.CliNotExecutable,
            ENOEXEC => ErrorCodes.CliCannotRun,
            ETXTBSY => ErrorCodes.IoInUse,
            _ => null
        };
    }
}
