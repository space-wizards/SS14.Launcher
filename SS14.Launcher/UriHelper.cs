using System;
using System.Diagnostics.CodeAnalysis;
using System.Diagnostics.Contracts;

namespace SS14.Launcher;

/// <summary>
///     Helpers for managing SS14 URIs like <c>ss14://</c>.
/// </summary>
/// <remarks>
///     See this for details:
///     https://github.com/space-wizards/RobustToolbox/wiki/ss14:---and-ss14s:---URI-handling
/// </remarks>
public static class UriHelper
{
    public const string SchemeSs14 = "ss14";

    // ReSharper disable once InconsistentNaming
    public const string SchemeSs14s = "ss14s";

    /// <summary>
    ///     Parses an <c>ss14://</c> or <c>ss14s://</c> URI,
    ///     defaulting to <c>ss14://</c> if no scheme is specified.
    /// </summary>
    [Pure]
    public static Uri ParseSs14Uri(string address)
    {
        if (!TryParseSs14Uri(address, out var uri))
        {
            throw new FormatException("Not a valid SS14 URI");
        }

        return uri;
    }

    [Pure]
    public static bool TryParseSs14Uri(string address, [NotNullWhen(true)] out Uri? uri)
    {
        if (!address.Contains("://"))
        {
            address = "ss14://" + address;
        }

        if (!Uri.TryCreate(address, UriKind.Absolute, out uri))
        {
            return false;
        }

        if (uri.Scheme != SchemeSs14 && uri.Scheme != SchemeSs14s)
        {
            return false;
        }

        return true;
    }

    /// <summary>
    ///     Gets the <c>http://</c> or <c>https://</c> API address for a server address.
    /// </summary>
    [Pure]
    public static Uri GetServerApiAddress(Uri serverAddress)
    {
        var dataScheme = serverAddress.Scheme switch
        {
            "ss14" => Uri.UriSchemeHttp,
            "ss14s" => Uri.UriSchemeHttps,
            _ => throw new ArgumentException($"Wrong URI scheme: {serverAddress.Scheme}")
        };

        var builder = new UriBuilder(serverAddress)
        {
            Scheme = dataScheme,
        };

        // No port specified.
        // Default port for ss14:// is 1212, for ss14s:// it's 443 (HTTPS)
        if (serverAddress.IsDefaultPort && serverAddress.Scheme == SchemeSs14)
        {
            builder.Port = Global.DefaultServerPort;
        }

        if (!builder.Path.EndsWith('/'))
        {
            builder.Path += "/";
        }

        return builder.Uri;
    }

    /// <summary>
    ///     Gets the <c>/status</c> HTTP address for a server address.
    /// </summary>
    [Pure]
    public static Uri GetServerStatusAddress(string serverAddress)
    {
        return GetServerStatusAddress(ParseSs14Uri(serverAddress));
    }

    /// <summary>
    ///     Gets the <c>/status</c> HTTP address for an ss14 uri.
    /// </summary>
    [Pure]
    public static Uri GetServerStatusAddress(Uri serverAddress)
    {
        return new Uri(GetServerApiAddress(serverAddress), "status");
    }

    /// <summary>
    ///     Gets the <c>/info</c> HTTP address for a server address.
    /// </summary>
    [Pure]
    public static Uri GetServerInfoAddress(string serverAddress)
    {
        return GetServerInfoAddress(ParseSs14Uri(serverAddress));
    }

    /// <summary>
    ///     Gets the <c>/info</c> HTTP address for an ss14 uri.
    /// </summary>
    [Pure]
    public static Uri GetServerInfoAddress(Uri serverAddress)
    {
        return new Uri(GetServerApiAddress(serverAddress), "info");
    }

    /// <summary>
    ///     Gets the <c>/client.zip</c> HTTP address for a server address.
    ///     This is not necessarily the actual client ZIP address.
    /// </summary>
    [Pure]
    public static Uri GetServerSelfhostedClientZipAddress(string serverAddress)
    {
        return GetServerSelfhostedClientZipAddress(ParseSs14Uri(serverAddress));
    }

    /// <summary>
    ///     Gets the <c>/client.zip</c> HTTP address for an ss14 uri.
    ///     This is not necessarily the actual client ZIP address.
    /// </summary>
    [Pure]
    public static Uri GetServerSelfhostedClientZipAddress(Uri serverAddress)
    {
        return new Uri(GetServerApiAddress(serverAddress), "client.zip");
    }
}
