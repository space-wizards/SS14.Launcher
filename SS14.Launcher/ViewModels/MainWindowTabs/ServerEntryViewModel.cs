using System;
using System.ComponentModel;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Microsoft.Toolkit.Mvvm.Messaging;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.ServerStatus;
using static SS14.Launcher.Utility.HubUtility;

namespace SS14.Launcher.ViewModels.MainWindowTabs;

public sealed class ServerEntryViewModel : ObservableRecipient, IRecipient<FavoritesChanged>, IViewModelBase
{
    private readonly ServerStatusData _cacheData;
    private readonly IServerSource _serverSource;
    private readonly DataManager _cfg;
    private readonly MainWindowViewModel _windowVm;
    private string Address => _cacheData.Address;
    private string _fallbackName = string.Empty;
    private bool _isExpanded;

    public ServerEntryViewModel(MainWindowViewModel windowVm, ServerStatusData cacheData, IServerSource serverSource, DataManager cfg)
    {
        _cfg = cfg;
        _windowVm = windowVm;
        _cacheData = cacheData;
        _serverSource = serverSource;
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusData cacheData,
        FavoriteServer favorite,
        IServerSource serverSource,
        DataManager cfg)
        : this(windowVm, cacheData, serverSource, cfg)
    {
        Favorite = favorite;
    }

    public ServerEntryViewModel(
        MainWindowViewModel windowVm,
        ServerStatusDataWithFallbackName ssdfb,
        IServerSource serverSource,
        DataManager cfg)
        : this(windowVm, ssdfb.Data, serverSource, cfg)
    {
        FallbackName = ssdfb.FallbackName ?? "";
    }

    public void ConnectPressed()
    {
        ConnectingViewModel.StartConnect(_windowVm, Address);
    }

    public FavoriteServer? Favorite { get; }

    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            _isExpanded = value;
            CheckUpdateInfo();
        }
    }

    public string Name => Favorite?.Name ?? _cacheData.Name ?? _fallbackName;
    public string FavoriteButtonText => IsFavorite ? "Remove Favorite" : "Add Favorite";
    private bool IsFavorite => _cfg.FavoriteServers.Lookup(Address).HasValue;

    public bool ViewedInFavoritesPane { get; set; }

    public string ServerStatusString
    {
        get
        {
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    return "OFFLINE";
                case ServerStatusCode.Online:
                    var statusappend = $"";
                    if (_cacheData.PanicBunker == null)
                        statusappend += " ?";
                    else if (_cacheData.PanicBunker == true)
                        statusappend += " 🔒"; //TODO: replace this with a proper lock icon. we know Visne's currently working on iconing up the launcher in #126; they might be interested in giving this a proper icon
                    // Give a ratio for servers with a defined player count, or just a current number for those without.
                    if (_cacheData.SoftMaxPlayerCount > 0)
                    {
                        return $"{_cacheData.PlayerCount} / {_cacheData.SoftMaxPlayerCount}{statusappend}";
                    }
                    else
                    {
                        return $"{_cacheData.PlayerCount} / ∞ {statusappend}";
                    }
                case ServerStatusCode.FetchingStatus:
                    return "Fetching...";
                default:
                    throw new NotSupportedException();
            }
        }
    }

    public string Description
    {
        get
        {
            switch (_cacheData.Status)
            {
                case ServerStatusCode.Offline:
                    return "Unable to contact server";
                case ServerStatusCode.FetchingStatus:
                    return "Fetching server status...";
            }

            return _cacheData.StatusInfo switch
            {
                ServerStatusInfoCode.NotFetched => "Fetching server description...",
                ServerStatusInfoCode.Fetching => "Fetching server description...",
                ServerStatusInfoCode.Error => "Error while fetching server description",
                ServerStatusInfoCode.Fetched => _cacheData.Description ?? "No server description provided",
                _ => throw new ArgumentOutOfRangeException()
            };
        }
    }

    public bool IsOnline => _cacheData.Status == ServerStatusCode.Online;

    public string FallbackName
    {
        get => _fallbackName;
        set
        {
            SetProperty(ref _fallbackName, value);
            OnPropertyChanged(nameof(Name));
        }
    }

    public ServerStatusData CacheData => _cacheData;

    public string? FetchedFrom
    {
        get
        {
            if (_cfg.HasCustomHubs)
            {
                return _cacheData.HubAddress == null ? null : $"Fetched from {GetHubShortName(_cacheData.HubAddress)}";
            }

            return null;
        }
    }
    public bool ShowFetchedFrom => _cfg.HasCustomHubs && !ViewedInFavoritesPane;

    public void FavoriteButtonPressed()
    {
        if (IsFavorite)
        {
            // Remove favorite.
            _cfg.RemoveFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value);
        }
        else
        {
            var fav = new FavoriteServer(_cacheData.Name ?? FallbackName, Address);
            _cfg.AddFavoriteServer(fav);
        }

        _cfg.CommitConfig();
    }

    public void FavoriteRaiseButtonPressed()
    {
        if (IsFavorite)
        {
            // Usual business, raise priority
            _cfg.RaiseFavoriteServer(_cfg.FavoriteServers.Lookup(Address).Value);
        }

        _cfg.CommitConfig();
    }

    public void Receive(FavoritesChanged message)
    {
        OnPropertyChanged(nameof(FavoriteButtonText));
    }

    private void CheckUpdateInfo()
    {
        if (!IsExpanded || _cacheData.Status != ServerStatusCode.Online)
            return;

        if (_cacheData.StatusInfo is not (ServerStatusInfoCode.NotFetched or ServerStatusInfoCode.Error))
            return;

        _serverSource.UpdateInfoFor(_cacheData);
    }

    protected override void OnActivated()
    {
        base.OnActivated();

        _cacheData.PropertyChanged += OnCacheDataOnPropertyChanged;
    }

    protected override void OnDeactivated()
    {
        base.OnDeactivated();

        _cacheData.PropertyChanged -= OnCacheDataOnPropertyChanged;
    }

    private void OnCacheDataOnPropertyChanged(object? _, PropertyChangedEventArgs args)
    {
        switch (args.PropertyName)
        {
            case nameof(IServerStatusData.PlayerCount):
            case nameof(IServerStatusData.SoftMaxPlayerCount):
            case nameof(IServerStatusData.PanicBunker):
                OnPropertyChanged(nameof(ServerStatusString));
                break;

            case nameof(IServerStatusData.Status):
                OnPropertyChanged(nameof(IsOnline));
                OnPropertyChanged(nameof(ServerStatusString));
                OnPropertyChanged(nameof(Description));
                CheckUpdateInfo();
                break;

            case nameof(IServerStatusData.Name):
                OnPropertyChanged(nameof(Name));
                break;

            case nameof(IServerStatusData.Description):
            case nameof(IServerStatusData.StatusInfo):
                OnPropertyChanged(nameof(Description));
                break;
        }
    }
}
