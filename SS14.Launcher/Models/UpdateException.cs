using System;

namespace SS14.Launcher.Models;

public class UpdateException : Exception
{
    public UpdateException(string message) : base(message)
    {
    }
}

public sealed class NoEngineForPlatformException(string message) : UpdateException(message);
public sealed class NoModuleForPlatformException(string message) : UpdateException(message);
