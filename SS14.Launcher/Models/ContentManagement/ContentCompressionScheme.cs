namespace SS14.Launcher.Models.ContentManagement;

/// <summary>
/// Compression schemes for data stored in the content database.
/// </summary>
public enum ContentCompressionScheme
{
    None = 0,
    Deflate = 1,

    /// <summary>
    /// ZStandard compression. In the future may use SS14 specific dictionary IDs in the frame header.
    /// </summary>
    ZStd = 2,
}
