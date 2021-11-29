using System;
using Splat;

namespace SS14.Launcher.Utility;

public static class LocatorExt
{
    public static T GetRequiredService<T>(this IReadonlyDependencyResolver resolver)
    {
        return resolver.GetService<T>() ?? throw new InvalidOperationException("Service does not exist!");
    }
}
