using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using Serilog;
using Splat;
using SS14.Launcher.Api;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;

namespace SS14.Launcher.ViewModels;

public partial class AccountDropDownViewModel : ViewModelBase
{
    private readonly MainWindowViewModel _mainVm;
    private readonly DataManager _cfg;
    private readonly AuthApi _authApi;
    private readonly LoginManager _loginMgr;
    private readonly LocalizationManager _loc;

    private ObservableList<AvailableAccountViewModel> Accounts { get; }

    public bool EnableMultiAccounts => _cfg.ActuallyMultiAccounts;

    public AccountDropDownViewModel(MainWindowViewModel mainVm)
    {
        _mainVm = mainVm;
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _authApi = Locator.Current.GetRequiredService<AuthApi>();
        _loginMgr = Locator.Current.GetRequiredService<LoginManager>();
        _loc = LocalizationManager.Instance;

        Accounts = new(GetInactiveAccounts());

        _loginMgr.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is not nameof(_loginMgr.ActiveAccount))
                return;

            OnPropertyChanged(nameof(LoginText));
            OnPropertyChanged(nameof(AccountSwitchText));
            OnPropertyChanged(nameof(LogoutText));
            OnPropertyChanged(nameof(AccountControlsVisible));
            OnPropertyChanged(nameof(AccountSwitchVisible));

            Accounts.SetItems(GetInactiveAccounts());
        };

        _loginMgr.Logins.Connect().Subscribe(_ =>
        {
            OnPropertyChanged(nameof(LogoutText));
            OnPropertyChanged(nameof(AccountSwitchVisible));
        });
    }

    private IEnumerable<AvailableAccountViewModel> GetInactiveAccounts()
    {
        return _loginMgr.Logins
            .Items
            .Where(a => a != _loginMgr.ActiveAccount)
            .Select(a => new AvailableAccountViewModel(a));
    }

    public string LoginText => _loginMgr.ActiveAccount?.Username ??
                               (EnableMultiAccounts ? _loc.GetString("account-drop-down-none-selected") : _loc.GetString("account-drop-down-not-logged-in"));

    public string LogoutText => _cfg.Logins.Count == 1
        ? _loc.GetString("account-drop-down-log-out")
        : _loc.GetString("account-drop-down-log-out-of", ("name", _loginMgr.ActiveAccount?.Username));

    public bool AccountSwitchVisible => _cfg.Logins.Count > 1 || _loginMgr.ActiveAccount == null;
    public string AccountSwitchText => _loginMgr.ActiveAccount != null
        ? _loc.GetString("account-drop-down-switch-account")
        : _loc.GetString("account-drop-down-select-account");

    public bool AccountControlsVisible => _loginMgr.ActiveAccount != null;

    [ObservableProperty] private bool _isDropDownOpen;

    public async void LogoutPressed()
    {
        IsDropDownOpen = false;

        if (_loginMgr.ActiveAccount != null)
        {
            await _authApi.LogoutTokenAsync(_loginMgr.ActiveAccount.LoginInfo.Token.Token);
            _cfg.RemoveLogin(_loginMgr.ActiveAccount.LoginInfo);
        }
    }

    [UsedImplicitly]
    public void AccountButtonPressed(object account)
    {
        if (account is not LoggedInAccount loggedInAccount)
        {
            Log.Warning($"Tried to switch account but parameter was not of type {nameof(LoggedInAccount)}");
            return;
        }

        IsDropDownOpen = false;
        _mainVm.TrySwitchToAccount(loggedInAccount);
    }

    public void AddAccountPressed()
    {
        IsDropDownOpen = false;

        _loginMgr.ActiveAccount = null;
    }
}

public sealed partial class AvailableAccountViewModel : ViewModelBase
{
    [ObservableProperty] private LoggedInAccount _account;

    public string StatusText
        => Account.Username + Account.Status switch
        {
            AccountLoginStatus.Available => "",
            AccountLoginStatus.Expired => " (!)",
            _ => " (?)",
        };

    public AvailableAccountViewModel(LoggedInAccount account)
    {
        Account = account;
    }
}
