using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.ViewModels.Login;

public sealed partial class AuthTfaViewModel : BaseLoginViewModel
{
    private readonly AuthApi.AuthenticateRequest _request;
    private readonly LoginManager _loginMgr;
    private readonly AuthApi _authApi;
    private readonly DataManager _cfg;

    [ObservableProperty] private string _code = "";
    [ObservableProperty] private bool _isInputValid;

    public AuthTfaViewModel(
        MainWindowLoginViewModel parentVm,
        AuthApi.AuthenticateRequest request,
        LoginManager loginMgr,
        AuthApi authApi,
        DataManager cfg) : base(parentVm)
    {
        _request = request;
        _loginMgr = loginMgr;
        _authApi = authApi;
        _cfg = cfg;

        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(Code))
                IsInputValid = CheckInputValid();
        };
    }

    private bool CheckInputValid()
    {
        var trimmed = Code.AsSpan().Trim();
        if (trimmed.Length != 6)
            return false;

        foreach (var chr in trimmed)
        {
            if (!char.IsDigit(chr))
                return false;
        }

        return true;
    }

    public async void ConfirmTfa()
    {
        if (Busy)
            return;

        var tfaLogin = _request with { TfaCode = Code.Trim() };

        Busy = true;
        try
        {
            var resp = await _authApi.AuthenticateAsync(tfaLogin);

            await LoginViewModel.DoLogin(this, tfaLogin, resp, _loginMgr, _authApi);

            _cfg.CommitConfig();
        }
        finally
        {
            Busy = false;
        }
    }

    public void RecoveryCode()
    {
        // I don't want to implement recovery code stuff, so if you need them,
        // bloody use them to disable your authenticator app online.
        Helpers.OpenUri(ConfigConstants.AccountManagementUrl);
    }

    public void Cancel()
    {
        ParentVM.SwitchToLogin();
    }
}
