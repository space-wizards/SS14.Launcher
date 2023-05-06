using System.Collections.Generic;
using static SS14.Launcher.Api.ServerApi;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed partial class ServerListFiltersViewModel
{
    private static readonly Dictionary<string, string> RegionNamesEnglish = new()
    {
        // @formatter:off
        { Tags.RegionAfricaCentral,       "Africa Central"        },
        { Tags.RegionAfricaNorth,         "Africa North"          },
        { Tags.RegionAfricaSouth,         "Africa South"          },
        { Tags.RegionAntarctica,          "Antarctica"            },
        { Tags.RegionAsiaEast,            "Asia East"             },
        { Tags.RegionAsiaNorth,           "Asia North"            },
        { Tags.RegionAsiaSouthEast,       "Asia South East"       },
        { Tags.RegionCentralAmerica,      "Central America"       },
        { Tags.RegionEuropeEast,          "Europe East"           },
        { Tags.RegionEuropeWest,          "Europe West"           },
        { Tags.RegionGreenland,           "Greenland"             },
        { Tags.RegionIndia,               "India"                 },
        { Tags.RegionMiddleEast,          "Middle East"           },
        { Tags.RegionMoon,                "The Moon"              },
        { Tags.RegionNorthAmericaCentral, "North America Central" },
        { Tags.RegionNorthAmericaEast,    "North America East"    },
        { Tags.RegionNorthAmericaWest,    "North America West"    },
        { Tags.RegionOceania,             "Oceania"               },
        { Tags.RegionSouthAmericaEast,    "South America East"    },
        { Tags.RegionSouthAmericaSouth,   "South America South"   },
        { Tags.RegionSouthAmericaWest,    "South America West"    },
        // @formatter:on
    };

    private static readonly Dictionary<string, string> RegionNamesShortEnglish = new()
    {
        // @formatter:off
        { Tags.RegionAfricaCentral,       "Africa Central"  },
        { Tags.RegionAfricaNorth,         "Africa North"    },
        { Tags.RegionAfricaSouth,         "Africa South"    },
        { Tags.RegionAntarctica,          "Antarctica"      },
        { Tags.RegionAsiaEast,            "Asia East"       },
        { Tags.RegionAsiaNorth,           "Asia North"      },
        { Tags.RegionAsiaSouthEast,       "Asia South East" },
        { Tags.RegionCentralAmerica,      "Central America" },
        { Tags.RegionEuropeEast,          "Europe East"     },
        { Tags.RegionEuropeWest,          "Europe West"     },
        { Tags.RegionGreenland,           "Greenland"       },
        { Tags.RegionIndia,               "India"           },
        { Tags.RegionMiddleEast,          "Middle East"     },
        { Tags.RegionMoon,                "The Moon"        },
        { Tags.RegionNorthAmericaCentral, "NA Central"      },
        { Tags.RegionNorthAmericaEast,    "NA East"         },
        { Tags.RegionNorthAmericaWest,    "NA West"         },
        { Tags.RegionOceania,             "Oceania"         },
        { Tags.RegionSouthAmericaEast,    "SA East"         },
        { Tags.RegionSouthAmericaSouth,   "SA South"        },
        { Tags.RegionSouthAmericaWest,    "SA West"         },
        // @formatter:on
    };

    private static readonly Dictionary<string, string> RolePlayNames = new()
    {
        // @formatter:off
        { Tags.RolePlayNone,   "None"   },
        { Tags.RolePlayLow,    "Low"    },
        { Tags.RolePlayMedium, "Medium" },
        { Tags.RolePlayHigh,   "High"   },
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
