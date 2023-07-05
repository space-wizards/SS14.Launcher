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
    private readonly FilterListCollection _filtersHub = new();

    public ObservableCollection<ServerFilterViewModel> FiltersLanguage => _filtersLanguage;
    public ObservableCollection<ServerFilterViewModel> FiltersRegion => _filtersRegion;
    public ObservableCollection<ServerFilterViewModel> FiltersRolePlay => _filtersRolePlay;
    public ObservableCollection<ServerFilterViewModel> FiltersEighteenPlus => _filtersEighteenPlus;
    public ObservableCollection<ServerFilterViewModel> FiltersHub => _filtersHub;

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
    }

    /// <summary>
    /// Update the set of visible filters, to avoid redundant filters that would match no servers.
    /// </summary>
    public void UpdatePresentFilters(IEnumerable<ServerStatusData> servers)
    {
        var filtersLanguage = new List<ServerFilterViewModel>();
        var filtersRegion = new List<ServerFilterViewModel>();
        var filtersRolePlay = new List<ServerFilterViewModel>();
        var filtersHub = new List<ServerFilterViewModel>();

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

            if (server.HubAddress is { } hub)
            {
                var filter = new ServerFilter(ServerFilterCategory.Hub, hub);
                if (!alreadyAdded.Add(filter))
                    continue;

                var shortName = Uri.TryCreate(hub, UriKind.Absolute, out var uri)
                    ? uri.Host
                    : hub;

                var vm = new ServerFilterViewModel(hub, shortName, filter, this);
                filtersHub.Add(vm);
            }
        }

        // Sort.
        filtersLanguage.Sort(ServerFilterShortNameComparer.Instance);
        filtersRegion.Sort(ServerFilterShortNameComparer.Instance);
        filtersRolePlay.Sort(ServerFilterDataOrderComparer.InstanceRolePlay);
        filtersHub.Sort(ServerFilterShortNameComparer.Instance);

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
        _filtersHub.SetItems(filtersHub);
    }

    public bool GetFilter(ServerFilter filter) => _dataManager.Filters.Contains(filter);

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
        var categorySetHub = GetCategoryFilterSet(FiltersHub);

        // Precache 18+ bool.
        bool? eighteenPlus = null;
        if (GetFilter(new ServerFilter(ServerFilterCategory.EighteenPlus, ServerFilter.DataTrue)))
        {
            eighteenPlus = true;
        }

        if (GetFilter(new ServerFilter(ServerFilterCategory.EighteenPlus, ServerFilter.DataFalse)))
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

            if (categorySetHub != null && server.HubAddress != null && !categorySetHub.Contains(server.HubAddress))
                return false;

            return true;
        }

        HashSet<string>? GetCategoryFilterSet(IEnumerable<ServerFilterViewModel> visible)
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
            return string.Compare(x.ShortName, y.ShortName, StringComparison.CurrentCultureIgnoreCase);
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

    private sealed class FilterListCollection : ObservableCollection<ServerFilterViewModel>
    {
        public void SetItems(IEnumerable<ServerFilterViewModel> items)
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
