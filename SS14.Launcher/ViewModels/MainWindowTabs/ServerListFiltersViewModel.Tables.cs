using System.Collections.Generic;
using static SS14.Launcher.Api.ServerApi;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed partial class ServerListFiltersViewModel
{
    private static readonly Dictionary<string, string> RegionNamesLoc = new()
    {
        // @formatter:off
        { Tags.RegionAfricaCentral,       "region-africa-central"        },
        { Tags.RegionAfricaNorth,         "region-africa-north"          },
        { Tags.RegionAfricaSouth,         "region-africa-south"          },
        { Tags.RegionAntarctica,          "region-antarctica"            },
        { Tags.RegionAsiaEast,            "region-asia-east"             },
        { Tags.RegionAsiaNorth,           "region-asia-north"            },
        { Tags.RegionAsiaSouthEast,       "region-asia-south-east"       },
        { Tags.RegionCentralAmerica,      "region-central-america"       },
        { Tags.RegionEuropeEast,          "region-europe-east"           },
        { Tags.RegionEuropeWest,          "region-europe-west"           },
        { Tags.RegionGreenland,           "region-greenland"             },
        { Tags.RegionIndia,               "region-india"                 },
        { Tags.RegionMiddleEast,          "region-middle-east"           },
        { Tags.RegionMoon,                "region-the-moon"              },
        { Tags.RegionNorthAmericaCentral, "region-north-america-central" },
        { Tags.RegionNorthAmericaEast,    "region-north-america-east"    },
        { Tags.RegionNorthAmericaWest,    "region-north-america-west"    },
        { Tags.RegionOceania,             "region-oceania"               },
        { Tags.RegionSouthAmericaEast,    "region-south-america-east"    },
        { Tags.RegionSouthAmericaSouth,   "region-south-america-south"   },
        { Tags.RegionSouthAmericaWest,    "region-south-america-west"    },
        // @formatter:on
    };

    private static readonly Dictionary<string, string> RegionNamesShortLoc = new()
    {
        // @formatter:off
        { Tags.RegionAfricaCentral,       "region-short-africa-central"        },
        { Tags.RegionAfricaNorth,         "region-short-africa-north"          },
        { Tags.RegionAfricaSouth,         "region-short-africa-south"          },
        { Tags.RegionAntarctica,          "region-short-antarctica"            },
        { Tags.RegionAsiaEast,            "region-short-asia-east"             },
        { Tags.RegionAsiaNorth,           "region-short-asia-north"            },
        { Tags.RegionAsiaSouthEast,       "region-short-asia-south-east"       },
        { Tags.RegionCentralAmerica,      "region-short-central-america"       },
        { Tags.RegionEuropeEast,          "region-short-europe-east"           },
        { Tags.RegionEuropeWest,          "region-short-europe-west"           },
        { Tags.RegionGreenland,           "region-short-greenland"             },
        { Tags.RegionIndia,               "region-short-india"                 },
        { Tags.RegionMiddleEast,          "region-short-middle-east"           },
        { Tags.RegionMoon,                "region-short-the-moon"              },
        { Tags.RegionNorthAmericaCentral, "region-short-north-america-central" },
        { Tags.RegionNorthAmericaEast,    "region-short-north-america-east"    },
        { Tags.RegionNorthAmericaWest,    "region-short-north-america-west"    },
        { Tags.RegionOceania,             "region-short-oceania"               },
        { Tags.RegionSouthAmericaEast,    "region-short-south-america-east"    },
        { Tags.RegionSouthAmericaSouth,   "region-short-south-america-south"   },
        { Tags.RegionSouthAmericaWest,    "region-short-south-america-west"    },
        // @formatter:on
    };

    private static readonly Dictionary<string, string> RolePlayNames = new()
    {
        // @formatter:off
        { Tags.RolePlayNone,   "filters-rp-none-desc"   },
        { Tags.RolePlayLow,    "filters-rp-low-desc"    },
        { Tags.RolePlayMedium, "filters-rp-medium-desc" },
        { Tags.RolePlayHigh,   "filters-rp-high-desc"   },
        // @formatter:on
    };

    private static readonly Dictionary<string, string> RolePlayNamesShort = new()
    {
        // @formatter:off
        { Tags.RolePlayNone,   "filters-rp-none"   },
        { Tags.RolePlayLow,    "filters-rp-low"    },
        { Tags.RolePlayMedium, "filters-rp-medium" },
        { Tags.RolePlayHigh,   "filters-rp-high"   },
        // @formatter:on
    };

    private static readonly Dictionary<string, int> RolePlaySortOrder = new()
    {
        // @formatter:off
        { Tags.RolePlayNone,   0 },
        { Tags.RolePlayLow,    1 },
        { Tags.RolePlayMedium, 2 },
        { Tags.RolePlayHigh,   3 },
        // @formatter:on
    };
}
