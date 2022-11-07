using System.Text.Json.Serialization;

namespace SS14.Launcher.Models;

/// <summary>
///     Basic data type to deserialize from the /info API endpoint on the server.
/// </summary>
public sealed class ServerInfo
{
    [JsonPropertyName("connect_address")]
    public string? ConnectAddress { get; set; }

    [JsonInclude, JsonPropertyName("build")] public ServerBuildInformation? BuildInformation;
    [JsonPropertyName("auth")] public ServerAuthInformation AuthInformation { get; set; } = default!;
}

public class ServerAuthInformation
{
    [JsonPropertyName("mode")] public AuthMode Mode { get; set; }

    [JsonPropertyName("public_key")]
    public string PublicKey { get; set; } = default!;
}

public class ServerBuildInformation
{
    [JsonInclude, JsonPropertyName("download_url")]
    public string DownloadUrl = default!;

    [JsonInclude, JsonPropertyName("manifest_url")]
    public string? ManifestUrl;

    [JsonInclude, JsonPropertyName("manifest_download_url")]
    public string? ManifestDownloadUrl;

    [JsonInclude, JsonPropertyName("engine_version")]
    public string EngineVersion = default!;

    [JsonInclude, JsonPropertyName("version")]
    public string Version = default!;

    [JsonInclude, JsonPropertyName("fork_id")]
    public string ForkId = default!;

    [JsonInclude, JsonPropertyName("hash")]
    public string? Hash;

    [JsonInclude, JsonPropertyName("manifest_hash")]
    public string? ManifestHash;

    [JsonInclude, JsonPropertyName("acz")]
    public bool Acz;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuthMode
{
    Optional = 0,
    Required = 1,
    Disabled = 2
}
