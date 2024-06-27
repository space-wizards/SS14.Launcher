using SS14.Launcher.Api;
using SS14.Launcher.Localization;

namespace SS14.Launcher.ViewModels.Login;

public class AuthErrorsOverlayViewModel : ViewModelBase
{
    public IErrorOverlayOwner ParentVm { get; }
    public string Title { get; }
    public string[] Errors { get; }

    public AuthErrorsOverlayViewModel(IErrorOverlayOwner parentVM, string title, string[] errors)
    {
        ParentVm = parentVM;
        Title = title;
        Errors = errors;
    }

    public static string[] AuthCodeToErrors(string[] errors, AuthApi.AuthenticateDenyResponseCode code)
    {
        if (code == AuthApi.AuthenticateDenyResponseCode.UnknownError)
            return errors;

        var loc = LocalizationManager.Instance;
        var err = code switch
        {
            AuthApi.AuthenticateDenyResponseCode.InvalidCredentials => "login-error-invalid-credentials",
            AuthApi.AuthenticateDenyResponseCode.AccountUnconfirmed => "login-error-account-unconfirmed",

            // Never shown I hope.
            AuthApi.AuthenticateDenyResponseCode.TfaRequired => "login-error-account-2fa-required",
            AuthApi.AuthenticateDenyResponseCode.TfaInvalid => "login-error-account-2fa-invalid",
            AuthApi.AuthenticateDenyResponseCode.AccountLocked => "login-error-account-account-locked",
            AuthApi.AuthenticateDenyResponseCode.EmailChangeNeeded => "login-error-account-account-email-change-needed",
            AuthApi.AuthenticateDenyResponseCode.PasswdChangeNeeded => "login-error-account-account-pass-change-needed",
            _ => "login-error-unknown"
        };

        return new[] { loc.GetString(err) };
    }

    public void Ok()
    {
        ParentVm.OverlayOk();
    }
}
