using System;

namespace SS14.Launcher.Utility;

/// <summary>
/// Wraps a byte array and implements equality for it, for use as a hashset/dictionary key.
/// </summary>
public struct HashKey(byte[] data) : IEquatable<HashKey>
{
    public byte[] Data = data;

    public readonly bool Equals(HashKey other)
    {
        return Data.AsSpan().SequenceEqual(other.Data);
    }

    public readonly override bool Equals(object? obj)
    {
        return obj is HashKey other && Equals(other);
    }

    public readonly override int GetHashCode()
    {
        var hc = new HashCode();
        hc.AddBytes(Data);
        return hc.ToHashCode();
    }

    public static bool operator ==(HashKey left, HashKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(HashKey left, HashKey right)
    {
        return !left.Equals(right);
    }
}
