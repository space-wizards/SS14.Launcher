using System;
using System.Buffers;
using System.IO;
using SpaceWizards.Sodium;

namespace SS14.Launcher.Utility;

public static class Blake2B
{
    public static byte[] HashStream(Stream stream, int outputLength)
    {
        CryptoGenericHashBlake2B.State state;
        var pool = ArrayPool<byte>.Shared.Rent(65536);

        CryptoGenericHashBlake2B.Init(ref state, ReadOnlySpan<byte>.Empty, outputLength);

        while (true)
        {
            var read = stream.Read(pool, 0, pool.Length);
            if (read == 0)
                break;

            var readData = pool.AsSpan(0, read);
            CryptoGenericHashBlake2B.Update(ref state, readData);
        }

        ArrayPool<byte>.Shared.Return(pool);

        var result = new byte[outputLength];
        CryptoGenericHashBlake2B.Final(ref state, result);

        return result;
    }
}
