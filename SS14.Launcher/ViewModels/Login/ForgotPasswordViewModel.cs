using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Api;
using SS14.Launcher.Localization;

namespace SS14.Launcher.ViewModels.Login;

public sealed partial class ForgotPasswordViewModel(MainWindowLoginViewModel parentVM, AuthApi authApi)
    : BaseLoginViewModel(parentVM)
{
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    [ObservableProperty] private string _email = "";
    private bool _errored;

    public async void SubmitPressed()
    {
        if (Busy)
            return;

        Busy = true;
        try
        {
            BusyText = "Sending email...";
            var errors = await authApi.ForgotPasswordAsync(Email);

            _errored = errors != null;

            if (!_errored)
            {
                // This isn't an error lol but that's what I called the control.
                OverlayControl = new AuthErrorsOverlayViewModel(this, _loc.GetString("login-forgot-success-title"), [
                    _loc.GetString("login-forgot-success-message"),
                ]);
            }
            else
            {
                OverlayControl = new AuthErrorsOverlayViewModel(this, _loc.GetString("login-forgot-error"), errors!);
            }
        }
        finally
        {
            Busy = false;
        }
    }

    public override void OverlayOk()
    {
        if (_errored)
        {
            base.OverlayOk();
        }
        else
        {
            // If the overlay was a success overlay, switch back to login.
            ParentVM.SwitchToLogin();
        }
    }
}
