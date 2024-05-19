using System;
using Avalonia.Threading;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using SS14.Launcher.Api;
using SS14.Launcher.Localization;
using SS14.Launcher.Models.Data;
using SS14.Launcher.Models.Logins;

namespace SS14.Launcher.ViewModels.Login;

public class RegisterNeedsConfirmationViewModel : BaseLoginViewModel
{
    private const int TimeoutSeconds = 5;

    private readonly AuthApi _authApi;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    private readonly string _loginUsername;
    private readonly string _loginPassword;
    private readonly LoginManager _loginMgr;
    private readonly DataManager _dataManager;

    public bool ConfirmButtonEnabled => TimeoutSecondsLeft == 0;

    public string ConfirmButtonText
    {
        get
        {
            var text = _loc.GetString("login-resend-button-confirm");
            if (TimeoutSecondsLeft != 0)
            {
                text = $"{text} ({TimeoutSecondsLeft})";
            }

            return text;
        }
    }

    [Reactive] private int TimeoutSecondsLeft { get; set; }

    public RegisterNeedsConfirmationViewModel(
        MainWindowLoginViewModel parentVm,
        AuthApi authApi, string username, string password, LoginManager loginMgr, DataManager dataManager)
        : base(parentVm)
    {
        BusyText = _loc.GetString("login-resend-busy");
        _authApi = authApi;

        _loginUsername = username;
        _loginPassword = password;
        _loginMgr = loginMgr;
        _dataManager = dataManager;

        this.WhenAnyValue(p => p.TimeoutSecondsLeft)
            .Subscribe(_ =>
            {
                this.RaisePropertyChanged(nameof(ConfirmButtonText));
                this.RaisePropertyChanged(nameof(ConfirmButtonEnabled));
            });
    }

    public override void Activated()
    {
        TimeoutSecondsLeft = TimeoutSeconds;
        DispatcherTimer.Run(TimerTick, TimeSpan.FromSeconds(1));
    }

    private bool TimerTick()
    {
        TimeoutSecondsLeft -= 1;
        return TimeoutSecondsLeft != 0;
    }

    public async void ConfirmButtonPressed()
    {
        if (Busy)
            return;

        Busy = true;

        try
        {
            var request = new AuthApi.AuthenticateRequest(_loginUsername, _loginPassword);
            var resp = await _authApi.AuthenticateAsync(request);

            await LoginViewModel.DoLogin(this, request, resp, _loginMgr, _authApi);

            _dataManager.CommitConfig();
        }
        finally
        {
            Busy = false;
        }
    }
}
