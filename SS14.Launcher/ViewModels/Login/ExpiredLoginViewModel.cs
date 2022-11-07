using ReactiveUI.Fody.Helpers;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.ViewModels.Login;

public class ExpiredLoginViewModel : BaseLoginViewModel
{
    private readonly DataManager _cfg;
    private readonly AuthApi _authApi;
    private readonly LoginManager _loginMgr;

    public ExpiredLoginViewModel(
        MainWindowLoginViewModel parentVm,
        DataManager cfg,
        AuthApi authApi,
        LoginManager loginMgr,
        LoggedInAccount account)
    : base(parentVm)
    {
        _cfg = cfg;
        _authApi = authApi;
        _loginMgr = loginMgr;
        Account = account;
    }

    [Reactive] public string EditingPassword { get; set; } = "";
    public LoggedInAccount Account { get; }

    public async void OnLogInButtonPressed()
    {
        if (Busy)
            return;

        Busy = true;
        try
        {
            var request = new AuthApi.AuthenticateRequest(Account.UserId, EditingPassword);
            var resp = await _authApi.AuthenticateAsync(request);

            await LoginViewModel.DoLogin(this, request, resp, _loginMgr, _authApi);

            _cfg.CommitConfig();
        }
        finally
        {
            Busy = false;
        }
    }

    public void OnLogOutButtonPressed()
    {
        _cfg.RemoveLogin(Account.LoginInfo);
        _cfg.CommitConfig();

        ParentVM.SwitchToLogin();
    }
}
