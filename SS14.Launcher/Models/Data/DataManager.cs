using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Newtonsoft.Json;
using ReactiveUI;

namespace SS14.Launcher.Models.Data;

/// <summary>
///     Handles storage of all permanent data,
///     like username, current build, favorite servers...
/// </summary>
public sealed class DataManager : ReactiveObject
{
    private readonly object _configWriteLock = new object();

    private readonly SourceCache<FavoriteServer, string> _favoriteServers = new(f => f.Address);
    private readonly SourceCache<InstalledServerContent, string> _serverContent = new(i => i.ForkId);
    private readonly SourceCache<LoginInfo, Guid> _logins = new(l => l.UserId);
    // When using dynamic engine management, this is used to keep track of installed engine versions.
    private readonly SourceCache<InstalledEngineVersion, string> _engineInstallations = new(v => v.Version);

    private bool _ignoreSave = true;
    private int _nextInstallationId = 1;
    private Guid _fingerprint;
    private Guid? _selectedLogin;
    private bool _forceGLES2;
    private bool _dynamicPGO;
    private bool _hasDismissedEarlyAccessWarning;
    private bool _disableSigning;
    private bool _logClient;
    private bool _logLauncher;
    private bool _enableMultiAccounts;

    public DataManager()
    {
        // Save when anything about the favorite servers list changes.
        _favoriteServers
            .Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(_ => Save());

        _favoriteServers.Connect()
            .Subscribe(_ => Save());

        // Also the installations list.
        _serverContent
            .Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(_ => Save());

        _serverContent.Connect()
            .Subscribe(_ => Save());

        _logins.Connect()
            .Subscribe(_ => Save());

        _logins
            .Connect()
            .WhenAnyPropertyChanged()
            .Subscribe(_ => Save());

        _engineInstallations.Connect()
            .Subscribe(_ => Save());
    }

    public Guid Fingerprint => _fingerprint;

    public Guid? SelectedLoginId
    {
        get => _selectedLogin;
        set
        {
            if (value != null && !_logins.Lookup(value.Value).HasValue)
            {
                throw new ArgumentException("We are not logged in for that user ID.");
            }

            this.RaiseAndSetIfChanged(ref _selectedLogin, value, nameof(SelectedLoginId));
            Save();
        }
    }

    public IObservableCache<FavoriteServer, string> FavoriteServers => _favoriteServers;
    public IObservableCache<InstalledServerContent, string> ServerContent => _serverContent;
    public IObservableCache<LoginInfo, Guid> Logins => _logins;
    public IObservableCache<InstalledEngineVersion, string> EngineInstallations => _engineInstallations;

    /// <summary>
    ///     If true, whenever SS14 is started, the cvar will be set to force GLES2 rendering. (See Models/Connector.cs:LaunchClient)
    ///     Otherwise, it'll be set to the default fallback chain.
    /// </summary>
    public bool ForceGLES2
    {
        get => _forceGLES2;
        set
        {
            this.RaiseAndSetIfChanged(ref _forceGLES2, value);
            Save();
        }
    }

    public bool DynamicPGO
    {
        get => _dynamicPGO;
        set
        {
            this.RaiseAndSetIfChanged(ref _dynamicPGO, value);
            Save();
        }
    }

    public bool HasDismissedEarlyAccessWarning
    {
        get => _hasDismissedEarlyAccessWarning;
        set
        {
            this.RaiseAndSetIfChanged(ref _hasDismissedEarlyAccessWarning, value);
            Save();
        }
    }

    public bool DisableSigning
    {
        get => _disableSigning;
        set
        {
            this.RaiseAndSetIfChanged(ref _disableSigning, value);
            Save();
        }
    }

    public bool LogClient
    {
        get => _logClient;
        set
        {
            this.RaiseAndSetIfChanged(ref _logClient, value);
            Save();
        }
    }

    public bool LogLauncher
    {
        get => _logLauncher;
        set
        {
            this.RaiseAndSetIfChanged(ref _logLauncher, value);
            Save();
        }
    }

    public bool EnableMultiAccounts
    {
        get => _enableMultiAccounts;
        set
        {
            this.RaiseAndSetIfChanged(ref _enableMultiAccounts, value);
            Save();
        }
    }

    public bool ActuallyMultiAccounts =>
#if DEBUG
        true;
#else
            EnableMultiAccounts;
#endif

    public void AddFavoriteServer(FavoriteServer server)
    {
        if (_favoriteServers.Lookup(server.Address).HasValue)
        {
            throw new ArgumentException("A server with that address is already a favorite.");
        }

        _favoriteServers.AddOrUpdate(server);
    }

    public void RemoveFavoriteServer(FavoriteServer server)
    {
        _favoriteServers.Remove(server);
    }

    public void AddInstallation(InstalledServerContent installedServerContent)
    {
        if (_favoriteServers.Lookup(installedServerContent.ForkId).HasValue)
        {
            throw new ArgumentException("An installation with that fork ID already exists.");
        }

        _serverContent.AddOrUpdate(installedServerContent); // Will do a save.
    }

    public void RemoveInstallation(InstalledServerContent installedServerContent)
    {
        _serverContent.Remove(installedServerContent);
    }

    public void AddEngineInstallation(InstalledEngineVersion version)
    {
        _engineInstallations.AddOrUpdate(version);
    }

    public void RemoveEngineInstallation(InstalledEngineVersion version)
    {
        _engineInstallations.Remove(version);
    }

    public void AddLogin(LoginInfo login)
    {
        if (_logins.Lookup(login.UserId).HasValue)
        {
            throw new ArgumentException("A login with that UID already exists.");
        }

        _logins.AddOrUpdate(login);
    }

    public void RemoveLogin(LoginInfo loginInfo)
    {
        _logins.Remove(loginInfo);

        if (loginInfo.UserId == _selectedLogin)
        {
            SelectedLoginId = null;
        }
    }

    public int GetNewInstallationId()
    {
        // Don't explicitly save.
        // If something is actually gonna use this installation ID it'll cause a save.
        return _nextInstallationId++;
    }

    /// <summary>
    ///     Loads config file from disk, or resets the loaded config to default if the config doesn't exist on disk.
    /// </summary>
    public void Load()
    {
        _ignoreSave = true;
        try
        {
            var path = GetCfgPath();

            if (!File.Exists(path))
            {
                LoadDefaultConfig();
                return;
            }

            using var changeSuppress = SuppressChangeNotifications();

            var text = File.ReadAllText(path);
            var data = JsonConvert.DeserializeObject<JsonData>(text);

            _nextInstallationId = data.NextInstallationId;

            _favoriteServers.Edit(a =>
            {
                a.Clear();
                a.AddOrUpdate(data.Favorites);
            });

            _logins.Edit(p =>
            {
                p.Clear();
                if (data.Logins != null)
                {
                    p.AddOrUpdate(data.Logins);
                }
            });

            _engineInstallations.Edit(p =>
            {
                p.Clear();

                if (data.Engines != null)
                {
                    p.AddOrUpdate(data.Engines);
                }
            });

            if (data.ServerContent != null)
            {
                _serverContent.Edit(a =>
                {
                    a.Clear();
                    a.AddOrUpdate(data.ServerContent);
                });
            }

            _fingerprint = data.Fingerprint;
            _selectedLogin = data.SelectedLogin;

            ForceGLES2 = data.ForceGLES2 ?? false;
            DynamicPGO = data.DynamicPGO ?? true;
            _hasDismissedEarlyAccessWarning = data.DismissedEarlyAccessWarning ?? false;
            _disableSigning = data.DisableSigning;
            _logClient = data.LogClient;
            _logLauncher = data.LogLauncher;
            _enableMultiAccounts = data.MultiAccounts;
        }
        finally
        {
            _ignoreSave = false;
        }

        if (_fingerprint == default)
        {
            // If we don't have a fingerprint yet this is either a fresh config or an older config.
            // Generate a fingerprint and immediately save it to disk.
            _fingerprint = Guid.NewGuid();
            Save();
        }
    }

    private void LoadDefaultConfig()
    {
        // Nop for now
    }

    private void Save()
    {
        if (_ignoreSave)
        {
            return;
        }

        var path = GetCfgPath();

        var data = JsonConvert.SerializeObject(new JsonData
        {
            SelectedLogin = _selectedLogin,
            Logins = _logins.Items.ToList(),
            ForceGLES2 = _forceGLES2,
            DynamicPGO = _dynamicPGO,
            Favorites = _favoriteServers.Items.ToList(),
            NextInstallationId = _nextInstallationId,
            Engines = _engineInstallations.Items.ToList(),
            ServerContent = _serverContent.Items.ToList(),
            Fingerprint = _fingerprint,
            DismissedEarlyAccessWarning = _hasDismissedEarlyAccessWarning,
            LogClient = _logClient,
            DisableSigning = _disableSigning,
            LogLauncher = _logLauncher,
            MultiAccounts = _enableMultiAccounts
        });

        // Save config asynchronously to avoid potential disk hangs.
        Task.Run(() =>
        {
            lock (_configWriteLock)
            {
                File.WriteAllText(path, data);
            }
        });
    }

    private static string GetCfgPath()
    {
        return Path.Combine(LauncherPaths.DirUserData, "launcher_config.json");
    }

    [Serializable]
    private sealed class JsonData
    {
        [JsonProperty(PropertyName = "selected_login")]
        public Guid? SelectedLogin { get; set; }

        [JsonProperty(PropertyName = "favorites")]
        public List<FavoriteServer>? Favorites { get; set; }

        [JsonProperty(PropertyName = "server_content")]
        public List<InstalledServerContent>? ServerContent { get; set; }

        [JsonProperty(PropertyName = "engines")]
        public List<InstalledEngineVersion>? Engines { get; set; }

        [JsonProperty(PropertyName = "logins")]
        public List<LoginInfo>? Logins { get; set; }

        [JsonProperty(PropertyName = "next_installation_id")]
        public int NextInstallationId { get; set; } = 1;

        [JsonProperty(PropertyName = "fingerprint")]
        public Guid Fingerprint { get; set; }

        [JsonProperty(PropertyName = "force_gles2")]
        public bool? ForceGLES2 { get; set; }

        [JsonProperty(PropertyName = "dynamic_pgo")]
        public bool? DynamicPGO { get; set; }

        [JsonProperty(PropertyName = "dismissed_early_access_warning")]
        public bool? DismissedEarlyAccessWarning { get; set; }

        [JsonProperty(PropertyName = "disable_signing")]
        public bool DisableSigning { get; set; }

        [JsonProperty(PropertyName = "log_client")]
        public bool LogClient { get; set; }

        [JsonProperty(PropertyName = "log_launcher")]
        public bool LogLauncher { get; set; }

        [JsonProperty(PropertyName = "multi_accounts")]
        public bool MultiAccounts { get; set; }
    }
}
