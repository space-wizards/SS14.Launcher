using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace SS14.Launcher.Api;

public static class ServerApi
{
    // https://docs.spacestation14.io/en/engine/http-api
    public sealed record ServerStatus(
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("players")]
        int PlayerCount,
        [property: JsonPropertyName("soft_max_players")]
        int SoftMaxPlayerCount,
        [property: JsonPropertyName("tags")] string[]? Tags);

    /// <summary>
    /// Contains definitions for standard tags returned by game servers.
    /// </summary>
    public static class Tags
    {
        // @formatter:off

        // Base tag definitions.
        public const string TagEighteenPlus = "18+";
        public const string TagRegion       = "region:";
        public const string TagLanguage     = "lang:";
        public const string TagRolePlay     = "rp:";
        public const string TagNoTagInfer   = "no_tag_infer";

        // Region tags.
        public const string RegionAfricaCentral       = "af_c";
        public const string RegionAfricaNorth         = "af_n";
        public const string RegionAfricaSouth         = "af_s";
        public const string RegionAntarctica          = "ata";
        public const string RegionAsiaEast            = "as_e";
        public const string RegionAsiaNorth           = "as_n";
        public const string RegionAsiaSouthEast       = "as_se";
        public const string RegionCentralAmerica      = "am_c";
        public const string RegionEuropeEast          = "eu_e";
        public const string RegionEuropeWest          = "eu_w";
        public const string RegionGreenland           = "grl";
        public const string RegionIndia               = "ind";
        public const string RegionMiddleEast          = "me";
        public const string RegionMoon                = "luna";
        public const string RegionNorthAmericaCentral = "am_n_c";
        public const string RegionNorthAmericaEast    = "am_n_e";
        public const string RegionNorthAmericaWest    = "am_n_w";
        public const string RegionOceania             = "oce";
        public const string RegionSouthAmericaEast    = "am_s_e";
        public const string RegionSouthAmericaSouth   = "am_s_s";
        public const string RegionSouthAmericaWest    = "am_s_w";

        // RolePlay level tags.
        public const string RolePlayNone   = "none";
        public const string RolePlayLow    = "low";
        public const string RolePlayMedium = "med";
        public const string RolePlayHigh   = "high";
        // @formatter:on

        public static bool TryRegion(string tag, [NotNullWhen(true)] out string? region)
        {
            return TryTagPrefix(tag, TagRegion, out region);
        }

        public static bool TryLanguage(string tag, [NotNullWhen(true)] out string? language)
        {
            return TryTagPrefix(tag, TagLanguage, out language);
        }

        public static bool TryRolePlay(string tag, [NotNullWhen(true)] out string? rolePlay)
        {
            return TryTagPrefix(tag, TagRolePlay, out rolePlay);
        }

        public static bool TryTagPrefix(string tag, string prefix, [NotNullWhen(true)] out string? value)
        {
            if (!tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                value = null;
                return false;
            }

            value = tag[prefix.Length..];
            return true;
        }
    }
}
