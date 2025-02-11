using System.Threading.Tasks;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Api;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.ViewModels.Login;

public partial class LoginViewModel : BaseLoginViewModel
{
    private readonly AuthApi _authApi;
    private readonly LoginManager _loginMgr;
    private readonly DataManager _dataManager;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    [ObservableProperty] private string _username = "";
    [ObservableProperty] private string _password = "";
    [ObservableProperty] private bool _isInputValid;

    public LoginViewModel(MainWindowLoginViewModel parentVm, AuthApi authApi,
        LoginManager loginMgr, DataManager dataManager) : base(parentVm)
    {
        BusyText = _loc.GetString("login-login-busy-logging-in");
        _authApi = authApi;
        _loginMgr = loginMgr;
        _dataManager = dataManager;

        PropertyChanged += (_, e) =>
        {
            switch (e)
            {
                case { PropertyName: nameof(Username) }:
                case { PropertyName: nameof(Password) }:
                    IsInputValid = !string.IsNullOrEmpty(Username) && !string.IsNullOrEmpty(Password);
                    break;
            }
        };
    }

    public async void OnLogInButtonPressed()
    {
        if (!IsInputValid || Busy)
        {
            return;
        }

        Busy = true;
        try
        {
            var request = new AuthApi.AuthenticateRequest(Username, Password);
            var resp = await _authApi.AuthenticateAsync(request);

            await DoLogin(this, request, resp, _loginMgr, _authApi);

            _dataManager.CommitConfig();
        }
        finally
        {
            Busy = false;
        }
    }

    public static async Task<bool> DoLogin<T>(
        T vm,
        AuthApi.AuthenticateRequest request,
        AuthenticateResult resp,
        LoginManager loginMgr,
        AuthApi authApi)
        where T : BaseLoginViewModel, IErrorOverlayOwner
    {
        var loc = LocalizationManager.Instance;
        if (resp.IsSuccess)
        {
            var loginInfo = resp.LoginInfo;
            var oldLogin = loginMgr.Logins.Lookup(loginInfo.UserId);
            if (oldLogin.HasValue)
            {
                // Already had this login, apparently.
                // Thanks user.
                //
                // Log the OLD token out since we don't need two of them.
                // This also has the upside of re-available-ing the account
                // if the user used the main login prompt on an account we already had, but as expired.

                await authApi.LogoutTokenAsync(oldLogin.Value.LoginInfo.Token.Token);
                loginMgr.ActiveAccountId = loginInfo.UserId;
                loginMgr.UpdateToNewToken(loginMgr.ActiveAccount!, loginInfo.Token);
                return true;
            }

            loginMgr.AddFreshLogin(loginInfo);
            loginMgr.ActiveAccountId = loginInfo.UserId;
            return true;
        }

        if (resp.Code == AuthApi.AuthenticateDenyResponseCode.TfaRequired)
        {
            vm.ParentVM.SwitchToAuthTfa(request);
            return false;
        }

        var errors = AuthErrorsOverlayViewModel.AuthCodeToErrors(resp.Errors, resp.Code);
        vm.OverlayControl = new AuthErrorsOverlayViewModel(vm, loc.GetString("login-login-error-title"), errors);
        return false;
    }

    public void RegisterPressed()
    {
        // Registration is purely via website now, sorry.
        Helpers.OpenUri(ConfigConstants.AccountRegisterUrl);
    }

    public void ResendConfirmationPressed()
    {
        // Registration is purely via website now, sorry.
        Helpers.OpenUri(ConfigConstants.AccountResendConfirmationUrl);
    }
}
