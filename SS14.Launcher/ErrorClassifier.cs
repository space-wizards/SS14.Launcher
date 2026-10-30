using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Net.Http;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.Data.Sqlite;
using SS14.Launcher.Utility;
using YamlDotNet.Core;

namespace SS14.Launcher;

/// <summary>
/// Turns arbitrary exceptions into an <see cref="ErrorCode"/>, so failures that nobody explicitly assigned a code to still show something useful.
/// Pray this never breaks >.>
/// </summary>
public static class ErrorClassifier
{
    // Windows error codes as they show up in IOException.HResult.
    private const int HResultSharingViolation = unchecked((int)0x80070020);
    private const int HResultLockViolation = unchecked((int)0x80070021);
    private const int HResultHandleDiskFull = unchecked((int)0x80070027);
    private const int HResultDiskFull = unchecked((int)0x80070070);

    // SQLite primary result codes.
    private const int SqliteBusy = 5;
    private const int SqliteLocked = 6;
    private const int SqliteReadOnly = 8;
    private const int SqliteIoError = 10;
    private const int SqliteCorrupt = 11;
    private const int SqliteFull = 13;
    private const int SqliteCannotOpen = 14;
    private const int SqliteProtocol = 15;
    private const int SqliteNotADatabase = 26;

    // Win32 error codes from starting a process.
    private const int Win32FileNotFound = 2;
    private const int Win32PathNotFound = 3;
    private const int Win32AccessDenied = 5;
    private const int Win32BadExeFormat = 193;

    /// <summary>
    /// Whether the exception is a request timing out, as opposed to somebody cancelling it.
    /// </summary>
    public static bool IsTimeout(Exception exception)
    {
        return exception is OperationCanceledException { InnerException: TimeoutException };
    }

    /// <summary>
    /// Picks the most specific code for an exception and everything inside it.
    /// </summary>
    /// <param name="exception">The exception to classify.</param>
    /// <param name="fallback">What to return if nothing about the exception says anything useful.</param>
    public static ErrorCode Classify(Exception exception, ErrorCode? fallback = null)
    {
        if (ClassifyPrecisely(exception) is { } precise)
            return precise;

        var chain = Flatten(exception);

        // Second pass: broad categories, so a specific inner exception beats its generic wrapper
        foreach (var e in chain)
        {
            if (ClassifyBroad(e) is { } code)
                return code;
        }

        return fallback ?? ErrorCodes.GenUnexpected;
    }

    /// <summary>
    /// Like <see cref="Classify"/>, but returns null unless the exception identifies the failure precisely.
    /// Lets callers use their own, more meaningful code for generic failures such as a plain HTTP error.
    /// </summary>
    public static ErrorCode? ClassifyPrecisely(Exception exception)
    {
        foreach (var e in Flatten(exception))
        {
            if (ClassifySpecific(e) is { } code)
                return code;
        }

        return null;
    }

    private static ErrorCode? ClassifySpecific(Exception e)
    {
        switch (e)
        {
            case IErrorCoded coded:
                return coded.Code;

            case OperationCanceledException when IsTimeout(e):
                return ErrorCodes.NetTimeout;

            case SocketException socket:
                return socket.SocketErrorCode switch
                {
                    SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData
                        => ErrorCodes.NetDnsFailed,
                    SocketError.TimedOut => ErrorCodes.NetTimeout,
                    SocketError.ConnectionRefused or SocketError.NetworkUnreachable
                        or SocketError.HostUnreachable or SocketError.NetworkDown
                        => ErrorCodes.NetConnectFailed,
                    _ => null
                };

            case AuthenticationException:
                return ErrorCodes.NetTls;

            case SqliteException sqlite:
                return sqlite.SqliteErrorCode switch
                {
                    SqliteBusy or SqliteLocked => ErrorCodes.DbBusy,
                    SqliteReadOnly or SqliteFull => ErrorCodes.DbFull,
                    SqliteCorrupt or SqliteNotADatabase => ErrorCodes.DbCorrupt,
                    SqliteCannotOpen => ErrorCodes.DbCannotOpen,
                    SqliteIoError or SqliteProtocol => ErrorCodes.DbIoError,
                    _ => null
                };

            case UnauthorizedAccessException:
                return ErrorCodes.IoAccessDenied;

            // Must come before IOException, EndOfStreamException is one.
            case ZStdException or EndOfStreamException or InvalidDataException:
                return ErrorCodes.UpdCorruptData;

            // Must come before IOException, it is one.
            case PathTooLongException:
                return ErrorCodes.IoPathTooLong;

            case IOException io:
                return ClassifyIo(io);

            case DllNotFoundException or BadImageFormatException:
                return ErrorCodes.GenMissingLibrary;

            case YamlException:
                return ErrorCodes.UpdResourceManifest;
        }

        return null;
    }

    private static ErrorCode? ClassifyIo(IOException io)
    {
        if (UnixErrno.CurrentFlavor is { } flavor)
            return UnixErrno.ClassifyIo(io.HResult, flavor);

        return io.HResult switch
        {
            HResultSharingViolation or HResultLockViolation => ErrorCodes.IoInUse,
            HResultDiskFull or HResultHandleDiskFull => ErrorCodes.IoDiskFull,
            _ => null
        };
    }

    /// <summary>
    /// The code for a failure to start the game loader, or null if we have no idea wtf happened.
    /// </summary>
    public static ErrorCode? ClassifyProcessStart(Win32Exception exception)
    {
        return ClassifyProcessStart(exception.NativeErrorCode, OperatingSystem.IsWindows());
    }

    /// <summary>
    /// The code for a failure to start the game loader, from its native error number.
    /// </summary>
    public static ErrorCode? ClassifyProcessStart(int nativeError, bool windows)
    {
        if (!windows)
            return nativeError == 2 ? ErrorCodes.CliNoLoader : UnixErrno.ClassifyProcessStart(nativeError);


        return nativeError switch
        {
            Win32FileNotFound or Win32PathNotFound => ErrorCodes.CliNoLoader,
            Win32AccessDenied => ErrorCodes.CliNotExecutable,
            Win32BadExeFormat => ErrorCodes.CliCannotRun,
            _ => null
        };
    }

    private static ErrorCode? ClassifyBroad(Exception e)
    {
        return e switch
        {
            HttpRequestException => ErrorCodes.NetHttp,
            SocketException => ErrorCodes.NetConnectFailed,
            SqliteException => ErrorCodes.DbOther,
            IOException => ErrorCodes.IoOther,
            _ => null
        };
    }

    // The exception first, then everything inside it (including AggregateException members), outermost first.
    private static List<Exception> Flatten(Exception exception)
    {
        var result = new List<Exception>();
        var stack = new Stack<Exception>();
        stack.Push(exception);

        while (stack.Count > 0)
        {
            var e = stack.Pop();
            result.Add(e);

            if (e is AggregateException aggregate)
            {
                for (var i = aggregate.InnerExceptions.Count - 1; i >= 0; i--)
                {
                    stack.Push(aggregate.InnerExceptions[i]);
                }
            }
            else if (e.InnerException != null)
            {
                stack.Push(e.InnerException);
            }
        }

        return result;
    }
}
