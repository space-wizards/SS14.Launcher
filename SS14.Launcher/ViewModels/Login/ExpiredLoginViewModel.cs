using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.ViewModels.Login;

public partial class ExpiredLoginViewModel (
    MainWindowLoginViewModel parentVm,
    DataManager cfg,
    AuthApi authApi,
    LoginManager loginMgr,
    LoggedInAccount account)
    : BaseLoginViewModel(parentVm)
{
    [ObservableProperty] private string _password = "";
    public LoggedInAccount Account { get; } = account;

    public async void OnLogInButtonPressed()
    {
        if (Busy)
            return;

        Busy = true;
        try
        {
            var request = new AuthApi.AuthenticateRequest(Account.UserId, Password);
            var resp = await authApi.AuthenticateAsync(request);

            await LoginViewModel.DoLogin(this, request, resp, loginMgr, authApi);

            cfg.CommitConfig();
        }
        finally
        {
            Busy = false;
        }
    }

    public void OnLogOutButtonPressed()
    {
        cfg.RemoveLogin(Account.LoginInfo);
        cfg.CommitConfig();

        ParentVM.SwitchToLogin();
    }
}
