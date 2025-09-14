using System;

namespace SS14.Launcher.Models.ContentManagement;

// Simple model classes for the content DB.

public sealed class ContentVersion
{
    public long Id;
    public byte[] Hash = default!;
    public string ForkId = default!;
    public string ForkVersion = default!;
    public DateTime LastUsed = default!;
    public byte[]? ZipHash;
}
