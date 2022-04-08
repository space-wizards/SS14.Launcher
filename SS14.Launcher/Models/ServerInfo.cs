using Newtonsoft.Json;

namespace SS14.Launcher.Models;

/// <summary>
///     Basic data type to deserialize from the /info API endpoint on the server.
/// </summary>
public sealed class ServerInfo
{
    [JsonProperty(PropertyName = "connect_address")]
    public string? ConnectAddress { get; set; }

    [JsonProperty(PropertyName = "build")] public ServerBuildInformation? BuildInformation;
    [JsonProperty(PropertyName = "auth")] public ServerAuthInformation AuthInformation { get; set; } = default!;
}

[JsonObject(ItemRequired = Required.Always)]
public class ServerAuthInformation
{
    [JsonProperty(PropertyName = "mode")] public AuthMode Mode { get; set; }

    [JsonProperty(PropertyName = "public_key")]
    public string PublicKey { get; set; } = default!;
}

[JsonObject(ItemRequired = Required.Always)]
public class ServerBuildInformation
{
    [JsonProperty(PropertyName = "download_url")]
    public string DownloadUrl = default!;

    [JsonProperty(PropertyName = "manifest_url", Required = Required.Default)]
    public string? ManifestUrl;

    [JsonProperty(PropertyName = "manifest_download_url", Required = Required.Default)]
    public string? ManifestDownloadUrl;

    [JsonProperty(PropertyName = "engine_version")]
    public string EngineVersion = default!;

    [JsonProperty(PropertyName = "version")]
    public string Version = default!;

    [JsonProperty(PropertyName = "fork_id")]
    public string ForkId = default!;

    [JsonProperty(PropertyName = "hash", Required = Required.AllowNull)]
    public string? Hash;

    [JsonProperty(PropertyName = "manifest_hash", Required = Required.Default)]
    public string? ManifestHash;

    [JsonProperty(PropertyName = "acz", Required = Required.Default)]
    public bool Acz;
}

public enum AuthMode
{
    Optional = 0,
    Required = 1,
    Disabled = 2
}
