using System.Text.Json.Serialization;

namespace SS14.Launcher.Models.Data;

public sealed record InstalledEngineVersion(
    [property: JsonPropertyName("version")]
    string Version,
    [property: JsonPropertyName("signature")]
    string Signature);
