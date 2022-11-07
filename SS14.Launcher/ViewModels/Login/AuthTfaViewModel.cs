using System;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.ViewModels.Login;

public sealed class AuthTfaViewModel : BaseLoginViewModel
{
    private readonly AuthApi.AuthenticateRequest _request;
    private readonly LoginManager _loginMgr;
    private readonly AuthApi _authApi;
    private readonly DataManager _cfg;

    [Reactive] public string Code { get; set; } = "";

    [Reactive] public bool IsInputValid { get; private set; }

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

        this.WhenAnyValue(x => x.Code)
            .Subscribe(s => { IsInputValid = CheckInputValid(s); });
    }

    private static bool CheckInputValid(string code)
    {
        var trimmed = code.AsSpan().Trim();
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
