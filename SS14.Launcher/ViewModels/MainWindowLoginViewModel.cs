using System.Diagnostics;
using ReactiveUI;
using Splat;
using SS14.Launcher.Api;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;
using SS14.Launcher.Utility;
using SS14.Launcher.ViewModels.Login;

namespace SS14.Launcher.ViewModels;

public class MainWindowLoginViewModel : ViewModelBase
{
    private readonly DataManager _cfg;
    private readonly AuthApi _authApi;
    private readonly LoginManager _loginMgr;
    private BaseLoginViewModel _screen;

    public LanguageSelectorViewModel LanguageSelector { get; } = new();

    public BaseLoginViewModel Screen
    {
        get => _screen;
        set
        {
            this.RaiseAndSetIfChanged(ref _screen, value);
            value.Activated();
        }
    }

    public MainWindowLoginViewModel()
    {
        _cfg = Locator.Current.GetRequiredService<DataManager>();
        _authApi = Locator.Current.GetRequiredService<AuthApi>();
        _loginMgr = Locator.Current.GetRequiredService<LoginManager>();

        _screen = default!;
        SwitchToLogin();
    }

    public void SwitchToLogin()
    {
        Screen = new LoginViewModel(this, _authApi, _loginMgr, _cfg);
    }

    public void SwitchToExpiredLogin(LoggedInAccount account)
    {
        Screen = new ExpiredLoginViewModel(this, _cfg, _authApi, _loginMgr, account);
    }

    public void SwitchToRegister()
    {
        Screen = new RegisterViewModel(this, _cfg, _authApi, _loginMgr);
    }

    public void SwitchToForgotPassword()
    {
        Screen = new ForgotPasswordViewModel(this, _authApi);
    }

    public void SwitchToAuthTfa(AuthApi.AuthenticateRequest request)
    {
        Screen = new AuthTfaViewModel(this, request, _loginMgr, _authApi, _cfg);
    }

    public void SwitchToResendConfirmation()
    {
        Screen = new ResendConfirmationViewModel(this, _authApi);
    }

    public void SwitchToRegisterNeedsConfirmation(string username, string password)
    {
        Screen = new RegisterNeedsConfirmationViewModel(this, _authApi, username, password, _loginMgr, _cfg);
    }

    public void OpenLogDirectory()
    {
        Process.Start(new ProcessStartInfo
        {
            UseShellExecute = true,
            FileName = LauncherPaths.DirLogs
        });
    }
}
