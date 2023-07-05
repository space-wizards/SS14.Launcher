namespace SS14.Launcher.Utility;

/// <summary>
/// Specifies a single filter checkbox the user can toggle to filter servers.
/// </summary>
/// <remarks>
/// <para>
/// Filters are specified with two pieces of data: <see cref="Category"/> and <see cref="Data"/>.
/// </para>
/// <para>
/// Categories are groups like language, region, role-play level, etc.
/// Data is a specific identifier within that category. Contents depend on the filter:
/// </para>
/// <list type="bullet|number|table">
///     <item>
///         <term>18+</term>
///         <description><c>true</c> or <c>false</c></description>
///     </item>
///     <item>
///         <term>Region</term>
///         <description>Region tag value</description>
///     </item>
///     <item>
///         <term>Language</term>
///         <description>Primary language tag</description>
///     </item>
///     <item>
///         <term>RP</term>
///         <description>RP tag value</description>
///     </item>
/// </list>
/// </remarks>
/// <param name="Category">The category for this filter.</param>
/// <param name="Data">Contains the data for a single server filter.</param>
public record struct ServerFilter(ServerFilterCategory Category, string Data)
{
    /// <summary>
    /// Special value used to indicate "filter that the server does not specify value in this category".
    /// </summary>
    public const string DataUnspecified = "unspecified";

    public const string DataTrue = "true";

    public const string DataFalse = "false";
}

public enum ServerFilterCategory : byte
{
    Language = 1,
    Region = 2,
    RolePlay = 3,
    EighteenPlus = 4,
    Hub = 5,
}
