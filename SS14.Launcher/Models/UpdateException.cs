using System;

namespace SS14.Launcher.Models;

public class UpdateException : Exception, IErrorCoded
{
    public ErrorCode Code { get; }

    public UpdateException(string message, ErrorCode code, Exception? inner = null) : base(message, inner)
    {
        Code = code;
    }
}

public sealed class NoEngineForPlatformException(string message)
    : UpdateException(message, ErrorCodes.UpdNoEngineForPlatform);

public sealed class NoModuleForPlatformException(string message)
    : UpdateException(message, ErrorCodes.UpdNoModuleForPlatform);
