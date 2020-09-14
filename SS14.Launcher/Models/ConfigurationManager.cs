using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DynamicData;
using Newtonsoft.Json;
using ReactiveUI;

namespace SS14.Launcher.Models
{
    /// <summary>
    ///     Handles storage of all permanent data,
    ///     like username, current build, favorite servers...
    /// </summary>
    public sealed class ConfigurationManager : ReactiveObject
    {
        private static readonly (string name, string addr)[] DefaultFavorites =
            {
                ("Wizard's Den [US West]", "ss14s://builds.spacestation14.io/ss14_server")
            };

        private readonly object _configWriteLock = new object();

        private readonly SourceCache<FavoriteServer, string> _favoriteServers
            = new SourceCache<FavoriteServer, string>(f => f.Address);

        private readonly SourceCache<Installation, string> _installations =
            new SourceCache<Installation, string>(i => i.ForkId);

        private bool _ignoreSave;
        private string? _userName;
        private int _nextInstallationId = 1;
        private bool _forceGLES2;

        public ConfigurationManager()
        {
            // Save when anything about the favorite servers list changes.
            _favoriteServers
                .Connect()
                .WhenAnyPropertyChanged()
                .Subscribe(_ => Save());

            _favoriteServers.Connect()
                .Subscribe(_ => Save());

            // Also the installations list.
            _installations
                .Connect()
                .WhenAnyPropertyChanged()
                .Subscribe(_ => Save());

            _installations.Connect()
                .Subscribe(_ => Save());
        }

        /// <summary>
        ///     The username used to log into servers.
        /// </summary>
        public string? UserName
        {
            get => _userName;
            set
            {
                this.RaiseAndSetIfChanged(ref _userName, value);
                Save();
            }
        }

        public IObservableCache<FavoriteServer, string> FavoriteServers => _favoriteServers;
        public IObservableCache<Installation, string> Installations => _installations;

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

        public void AddInstallation(Installation installation)
        {
            if (_favoriteServers.Lookup(installation.ForkId).HasValue)
            {
                throw new ArgumentException("An installation with that fork ID already exists.");
            }

            _installations.AddOrUpdate(installation); // Will do a save.
        }

        public void RemoveInstallation(Installation installation)
        {
            _installations.Remove(installation);
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
                    _favoriteServers.Edit(x =>
                    {
                        x.Clear();
                        x.AddOrUpdate(DefaultFavorites.Select(p => new FavoriteServer(p.name, p.addr)));
                    });
                    UserName = null;
                    return;
                }

                var data = JsonConvert.DeserializeObject<JsonData>(File.ReadAllText(path));

                UserName = data.Username;
                _nextInstallationId = data.NextInstallationId;

                _favoriteServers.Edit(a =>
                {
                    a.Clear();
                    a.AddOrUpdate(data.Favorites);
                });

                if (data.Installations != null)
                {
                    _installations.Edit(a =>
                    {
                        a.Clear();
                        a.AddOrUpdate(data.Installations);
                    });
                }

                ForceGLES2 = data.ForceGLES2 ?? false;
            }
            finally
            {
                _ignoreSave = false;
            }
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
                Username = _userName,
                ForceGLES2 = _forceGLES2,
                Favorites = _favoriteServers.Items.ToList(),
                NextInstallationId = _nextInstallationId,
                Installations = _installations.Items.ToList()
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
            return Path.Combine(UserDataDir.GetUserDataDir(), "launcher_config.json");
        }

        [Serializable]
        private sealed class JsonData
        {
            [JsonProperty(PropertyName = "username")]
            public string? Username { get; set; }

            [JsonProperty(PropertyName = "favorites")]
            public List<FavoriteServer>? Favorites { get; set; }

            [JsonProperty(PropertyName = "installations")]
            public List<Installation>? Installations { get; set; }

            [JsonProperty(PropertyName = "next_installation_id")]
            public int NextInstallationId { get; set; } = 1;

            [JsonProperty(PropertyName = "force_gles2")]
            public bool? ForceGLES2 { get; set; }
       }
    }
}
