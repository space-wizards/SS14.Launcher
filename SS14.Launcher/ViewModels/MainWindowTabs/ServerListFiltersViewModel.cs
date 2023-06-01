using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using SS14.Launcher.Utility;
using static SS14.Launcher.Api.ServerApi;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed partial class ServerListFiltersViewModel : ObservableObject
{
    private readonly DataManager _dataManager;

    private int _totalServers;
    private int _filteredServers;

    private readonly FilterListCollection _filtersLanguage = new();
    private readonly FilterListCollection _filtersRegion = new();
    private readonly FilterListCollection _filtersRolePlay = new();
    private readonly FilterListCollection _filtersEighteenPlus = new();
    private readonly FilterListCollection _filtersPlayerCount = new();
    private readonly FilterListCollection _filtersPlayerCountLimits = new();

    public ObservableCollection<ServerFilterBaseViewModel> FiltersLanguage => _filtersLanguage;
    public ObservableCollection<ServerFilterBaseViewModel> FiltersRegion => _filtersRegion;
    public ObservableCollection<ServerFilterBaseViewModel> FiltersRolePlay => _filtersRolePlay;
    public ObservableCollection<ServerFilterBaseViewModel> FiltersEighteenPlus => _filtersEighteenPlus;
    public ObservableCollection<ServerFilterBaseViewModel> FiltersPlayerCount => _filtersPlayerCount;

    public event Action? FiltersUpdated;

    public int TotalServers
    {
        get => _totalServers;
        set => SetProperty(ref _totalServers, value);
    }

    public int FilteredServers
    {
        get => _filteredServers;
        set => SetProperty(ref _filteredServers, value);
    }

    public ServerListFiltersViewModel(DataManager dataManager)
    {
        _dataManager = dataManager;

        FiltersEighteenPlus.Add(new ServerFilterViewModel("Yes", "Yes",
            new ServerFilter(ServerFilterCategory.EighteenPlus, ServerFilter.DataTrue), this));
        FiltersEighteenPlus.Add(new ServerFilterViewModel("No", "No",
            new ServerFilter(ServerFilterCategory.EighteenPlus, ServerFilter.DataFalse), this));
        FiltersPlayerCount.Add(new ServerFilterViewModel("Filter Full Servers", "Filter Full",
            new ServerFilter(ServerFilterCategory.IsServerFull, ServerFilter.DataTrue), this));

        ServerFilter maxFilter = FilterCategoryExists(ServerFilterCategory.PlayerMax) ? GetFilterByCategory(ServerFilterCategory.PlayerMax) : new ServerFilter(ServerFilterCategory.PlayerMax, ServerFilter.DataUnspecified);
        ServerFilter minFilter = FilterCategoryExists(ServerFilterCategory.PlayerMin) ? GetFilterByCategory(ServerFilterCategory.PlayerMin) : new ServerFilter(ServerFilterCategory.PlayerMin, ServerFilter.DataUnspecified);
        FiltersPlayerCount.Add(new ServerFilterIntegerViewModel("Max Player Count", "Filter Max",
            maxFilter, 1, this, min: 0));
        FiltersPlayerCount.Add(new ServerFilterIntegerViewModel("Min Player Count", "Filter Min",
            minFilter, 1, this, min: 0));
    }

    /// <summary>
    /// Update the set of visible filters, to avoid redundant servers that would match no servers.
    /// </summary>
    public void UpdatePresentFilters(IEnumerable<ServerStatusData> servers)
    {
        var filtersLanguage = new List<ServerFilterViewModel>();
        var filtersRegion = new List<ServerFilterViewModel>();
        var filtersRolePlay = new List<ServerFilterViewModel>();

        var alreadyAdded = new HashSet<ServerFilter>();

        foreach (var server in servers)
        {
            foreach (var tag in server.Tags)
            {
                if (Tags.TryRegion(tag, out var region))
                {
                    if (!RegionNamesEnglish.TryGetValue(region, out var name))
                        continue;

                    var filter = new ServerFilter(ServerFilterCategory.Region, region);
                    if (!alreadyAdded.Add(filter))
                        continue;

                    var nameShort = RegionNamesShortEnglish[region];

                    var vm = new ServerFilterViewModel(name, nameShort, filter, this);
                    filtersRegion.Add(vm);
                }
                else if (Tags.TryLanguage(tag, out var language))
                {
                    // Don't use anything except the primary tag for now.
                    var primaryTag = PrimaryLanguageTag(language);
                    var filter = new ServerFilter(ServerFilterCategory.Language, primaryTag);
                    if (!alreadyAdded.Add(filter))
                        continue;

                    CultureInfo culture;
                    try
                    {
                        culture = new CultureInfo(primaryTag);
                    }
                    catch
                    {
                        // Language doesn't exist I guess.
                        continue;
                    }

                    var name = culture.EnglishName;
                    var vm = new ServerFilterViewModel(name, name, filter, this);
                    filtersLanguage.Add(vm);
                }
                else if (Tags.TryRolePlay(tag, out var rolePlay))
                {
                    if (!RolePlayNames.TryGetValue(rolePlay, out var rpName))
                        continue;

                    var filter = new ServerFilter(ServerFilterCategory.RolePlay, rolePlay);
                    if (!alreadyAdded.Add(filter))
                        continue;

                    var vm = new ServerFilterViewModel(rpName, rpName, filter, this);
                    filtersRolePlay.Add(vm);
                }
            }
        }

        // Sort.
        filtersLanguage.Sort(ServerFilterShortNameComparer.Instance);
        filtersRegion.Sort(ServerFilterShortNameComparer.Instance);
        filtersRolePlay.Sort(ServerFilterDataOrderComparer.InstanceRolePlay);

        // Unspecified always comes last.
        filtersLanguage.Add(new ServerFilterViewModel("Unspecified", "Unspecified",
            new ServerFilter(ServerFilterCategory.Language, ServerFilter.DataUnspecified), this));
        filtersRegion.Add(new ServerFilterViewModel("Unspecified", "Unspecified",
            new ServerFilter(ServerFilterCategory.Region, ServerFilter.DataUnspecified), this));
        filtersRolePlay.Add(new ServerFilterViewModel("Unspecified", "Unspecified",
            new ServerFilter(ServerFilterCategory.RolePlay, ServerFilter.DataUnspecified), this));

        // Set.
        _filtersLanguage.SetItems(filtersLanguage);
        _filtersRegion.SetItems(filtersRegion);
        _filtersRolePlay.SetItems(filtersRolePlay);
    }

    public bool FilterExists(ServerFilter filter) => _dataManager.Filters.Contains(filter);
    public bool FilterCategoryExists(ServerFilterCategory category) => _dataManager.Filters.Where((filter) => filter.Category == category).Count() > 0;

    public ServerFilter GetFilterByCategory(ServerFilterCategory category) => _dataManager.Filters.Where((filter) => filter.Category == category).FirstOrDefault();

    public void SetFilter(ServerFilter filter, bool value)
    {
        if (_dataManager.Filters.Contains(filter) && !value)
        {
            _dataManager.Filters.Remove(filter);
            _dataManager.CommitConfig();
            FiltersUpdated?.Invoke();
        }
        else if (!_dataManager.Filters.Contains(filter) && value)
        {
            _dataManager.Filters.Add(filter);
            _dataManager.CommitConfig();
            FiltersUpdated?.Invoke();
        }
    }

    public void ReplaceFilter(ServerFilter new_filter, ServerFilter old_filter)
    {
        if (_dataManager.Filters.Contains(old_filter))
            _dataManager.Filters.Remove(old_filter);
        _dataManager.Filters.Add(new_filter);
        _dataManager.CommitConfig();
        FiltersUpdated?.Invoke();
    }

    /// <summary>
    /// Apply active filter preferences to a list, removing all servers that do not fit the criteria.
    /// </summary>
    public void ApplyFilters(List<ServerStatusData> list)
    {
        TotalServers = list.Count;

        // Precache a bunch of stuff from the filters config so we can compare servers easier.
        var categorySetLanguage = GetCategoryFilterSet(FiltersLanguage);
        var categorySetRegion = GetCategoryFilterSet(FiltersRegion);
        var categorySetRolePlay = GetCategoryFilterSet(FiltersRolePlay);
        bool isFull = FilterExists(new ServerFilter(ServerFilterCategory.IsServerFull, ServerFilter.DataTrue));

        // Precache PlayerMax & PlayerMin
        bool isValidMax = int.TryParse(GetFilterByCategory(ServerFilterCategory.PlayerMax).Data, out int player_max);
        bool isValidMin = int.TryParse(GetFilterByCategory(ServerFilterCategory.PlayerMin).Data, out int player_min);

        // Precache 18+ bool.
        bool? eighteenPlus = null;
        if (FilterExists(new ServerFilter(ServerFilterCategory.EighteenPlus, ServerFilter.DataTrue)))
        {
            eighteenPlus = true;
        }

        if (FilterExists(new ServerFilter(ServerFilterCategory.EighteenPlus, ServerFilter.DataFalse)))
        {
            // Having both
            if (eighteenPlus == true)
                eighteenPlus = null;
            else
                eighteenPlus = false;
        }

        for (var i = 0; i < list.Count; i++)
        {
            var server = list[i];
            if (DoesServerMatch(server))
                continue;

            list.RemoveSwap(i);
            i -= 1;
        }

        FilteredServers = list.Count;

        bool DoesServerMatch(ServerStatusData server)
        {
            // Max Player Count Being 0 means an infinite amount of slots
            if (isFull && server.SoftMaxPlayerCount > 0 && server.PlayerCount >= server.SoftMaxPlayerCount)
                return false;
            if (isValidMax && server.PlayerCount > player_max)
                return false;
            if (isValidMin && server.PlayerCount < player_min)
                return false;

            // 18+ checks
            if (eighteenPlus != null)
            {
                var serverEighteenPlus = server.Tags.Contains(Tags.TagEighteenPlus);
                if (eighteenPlus != serverEighteenPlus)
                    return false;
            }

            if (!CheckCategoryFilterSet(categorySetLanguage, server.Tags, Tags.TagLanguage, PrimaryLanguageTag))
                return false;

            if (!CheckCategoryFilterSet(categorySetRegion, server.Tags, Tags.TagRegion))
                return false;

            if (!CheckCategoryFilterSet(categorySetRolePlay, server.Tags, Tags.TagRolePlay))
                return false;

            return true;
        }

        HashSet<string>? GetCategoryFilterSet(IEnumerable<ServerFilterBaseViewModel> visible)
        {
            // Filters are persisted, so it's possible to get a filter that isn't visible
            // (because no servers have the tag right now),
            // but is still active (stored in the database from before).
            // As such, filter logic needs to ignore these servers from the database.

            var set = visible.Where(x => _dataManager.Filters.Contains(x.Filter))
                .Select(x => x.Filter.Data)
                .ToHashSet();

            return set.Count == 0 ? null : set;
        }

        bool CheckCategoryFilterSet(
            HashSet<string>? filterSet,
            string[] serverTags,
            string tagPrefix,
            Func<string, string>? transformTagContents = null)
        {
            if (filterSet == null)
                return true;

            var isUnspecified = true;
            foreach (var tag in serverTags)
            {
                if (!Tags.TryTagPrefix(tag, tagPrefix, out var tagValue))
                    continue;

                if (transformTagContents != null)
                    tagValue = transformTagContents(tagValue);

                isUnspecified = false;
                if (filterSet.Contains(tagValue))
                    return true;
            }

            if (isUnspecified && filterSet.Contains(ServerFilter.DataUnspecified))
                return true;

            return false;
        }
    }

    private static string PrimaryLanguageTag(string fullTag)
    {
        var primaryTagIdx = fullTag.IndexOf('-');
        return primaryTagIdx == -1 ? fullTag : fullTag[..primaryTagIdx];
    }

    private sealed class ServerFilterShortNameComparer : NotNullComparer<ServerFilterViewModel>
    {
        public static readonly ServerFilterShortNameComparer Instance = new();

        public override int Compare(ServerFilterViewModel x, ServerFilterViewModel y)
        {
            return string.Compare(x.Name, y.Name, StringComparison.CurrentCultureIgnoreCase);
        }
    }

    private sealed class ServerFilterDataOrderComparer : NotNullComparer<ServerFilterViewModel>
    {
        private readonly Dictionary<string, int> _order;

        public static readonly ServerFilterDataOrderComparer InstanceRolePlay = new(RolePlaySortOrder);

        public ServerFilterDataOrderComparer(Dictionary<string, int> order)
        {
            _order = order;
        }

        public override int Compare(ServerFilterViewModel x, ServerFilterViewModel y)
        {
            var idxX = _order[x.Filter.Data];
            var idxY = _order[y.Filter.Data];
            return idxX.CompareTo(idxY);
        }
    }

    private sealed class FilterListCollection : ObservableCollection<ServerFilterBaseViewModel>
    {
        public void SetItems(IEnumerable<ServerFilterBaseViewModel> items)
        {
            Items.Clear();

            foreach (var item in items)
            {
                Items.Add(item);
            }

            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
        }
    }
}
