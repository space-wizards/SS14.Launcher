using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Threading;
using DynamicData;
using ReactiveUI;
using Serilog;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Models.Logins;

// This is different from DataManager in that this class actually manages logic more complex than raw storage.
// Checking and refreshing tokens, marking accounts as "need signing in again", etc...
public sealed class LoginManager : ReactiveObject
{
    // TODO: If the user tries to connect to a server or such
    // on the split second interval that the launcher does a token refresh
    // (once a week, if you leave it open for long).
    // there is a possibility the token used by said action will be invalid because it's actively being replaced
    // oh well.
    // Do I really care to fix that?

    private readonly DataManager _cfg;
    private readonly AuthApi _authApi;

    private IDisposable? _timer;

    private Guid? _activeLoginId;

    private readonly IObservableCache<ActiveLoginData, Guid> _logins;

    public Guid? ActiveAccountId
    {
        get => _activeLoginId;
        set
        {
            if (value != null)
            {
                var lookup = _logins.Lookup(value.Value);

                if (!lookup.HasValue)
                {
                    throw new ArgumentException("We do not have a login with that ID.");
                }
            }

            this.RaiseAndSetIfChanged(ref _activeLoginId, value);
            this.RaisePropertyChanged(nameof(ActiveAccount));
            _cfg.SelectedLoginId = value;
        }
    }

    public LoggedInAccount? ActiveAccount
    {
        get => _activeLoginId == null ? null : _logins.Lookup(_activeLoginId.Value).Value;
        set => ActiveAccountId = value?.UserId;
    }

    public IObservableCache<LoggedInAccount, Guid> Logins { get; }

    public LoginManager(DataManager cfg, AuthApi authApi)
    {
        _cfg = cfg;
        _authApi = authApi;

        _logins = _cfg.Logins
            .Connect()
            .Transform(p => new ActiveLoginData(p))
            .OnItemRemoved(p =>
            {
                if (p.LoginInfo.UserId == _activeLoginId)
                {
                    ActiveAccount = null;
                }
            })
            .AsObservableCache();

        Logins = _logins
            .Connect()
            .Transform((data, guid) => (LoggedInAccount) data)
            .AsObservableCache();
    }

    public async Task Initialize()
    {
        // Set up timer so that if the user leaves their launcher open for a month or something
        // their tokens don't expire.
        _timer = DispatcherTimer.Run(() =>
        {
            async void Impl()
            {
                await RefreshAllTokens();
            }

            Impl();
            return true;
        }, ConfigConstants.TokenRefreshInterval, DispatcherPriority.Background);

        // Refresh all tokens we got.
        await RefreshAllTokens();
    }

    private async Task RefreshAllTokens()
    {
        Log.Debug("Refreshing all tokens.");

        const int delayStart = 2;
        const int delayValue = 200;

        await Task.WhenAll(_logins.Items.Select(async (l, i) =>
        {
            if (l.Status == AccountLoginStatus.Expired)
            {
                // Literally don't even bother we already know it's dead and the user has to solve it.
                Log.Debug("Token for {login} is already expired", l.LoginInfo);
                return;
            }

            if (l.LoginInfo.Token.IsTimeExpired())
            {
                // Oh hey, time expiry.
                Log.Debug("Token for {login} expired due to time", l.LoginInfo);
                l.SetStatus(AccountLoginStatus.Expired);
                return;
            }

            if (i > delayStart)
                await Task.Delay(delayValue * (i - delayStart));

            try
            {
                await UpdateSingleAccountStatus(l);
            }
            catch (AuthApiException e)
            {
                // TODO: Maybe retry to refresh tokens sooner if an error occured.
                // Ignore, I guess.
                Log.Warning(e, "AuthApiException while trying to refresh token for {login}", l.LoginInfo);
            }
        }));
    }

    public void AddFreshLogin(LoginInfo info)
    {
        _cfg.AddLogin(info);

        _logins.Lookup(info.UserId).Value.SetStatus(AccountLoginStatus.Available);
    }

    public void UpdateToNewToken(LoggedInAccount account, LoginToken token)
    {
        var cast = (ActiveLoginData) account;
        cast.SetStatus(AccountLoginStatus.Available);
        account.LoginInfo.Token = token;
    }

    /// <exception cref="AuthApiException">Thrown if an API error occured.</exception>
    public Task UpdateSingleAccountStatus(LoggedInAccount account)
    {
        return UpdateSingleAccountStatus((ActiveLoginData) account);
    }

    private async Task UpdateSingleAccountStatus(ActiveLoginData data)
    {
        if (data.LoginInfo.Token.ShouldRefresh())
        {
            Log.Debug("Refreshing token for {login}", data.LoginInfo);
            // If we need to refresh the token anyways we'll just
            // implicitly do the "is it still valid" with the refresh request.
            var newTokenHopefully = await _authApi.RefreshTokenAsync(data.LoginInfo.Token.Token);
            if (newTokenHopefully == null)
            {
                // Token expired or whatever?
                data.SetStatus(AccountLoginStatus.Expired);
                Log.Debug("Token for {login} expired while refreshing it", data.LoginInfo);
            }
            else
            {
                Log.Debug("Refreshed token for {login}", data.LoginInfo);
                data.LoginInfo.Token = newTokenHopefully.Value;
                data.SetStatus(AccountLoginStatus.Available);
            }
        }
        else if (data.Status == AccountLoginStatus.Unsure)
        {
            var valid = await _authApi.CheckTokenAsync(data.LoginInfo.Token.Token);
            Log.Debug("Token for {login} still valid? {valid}", data.LoginInfo, valid);
            data.SetStatus(valid ? AccountLoginStatus.Available : AccountLoginStatus.Expired);
        }
    }

    private sealed class ActiveLoginData : LoggedInAccount
    {
        private AccountLoginStatus _status;

        public ActiveLoginData(LoginInfo info) : base(info)
        {
        }

        public override AccountLoginStatus Status => _status;

        public void SetStatus(AccountLoginStatus status)
        {
            this.RaiseAndSetIfChanged(ref _status, status, nameof(Status));
            Log.Debug("Setting status for login {account} to {status}", LoginInfo, status);
        }
    }
}
