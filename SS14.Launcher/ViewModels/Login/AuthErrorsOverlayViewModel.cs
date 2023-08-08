using SS14.Launcher.Api;

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

        var err = code switch
        {
            AuthApi.AuthenticateDenyResponseCode.InvalidCredentials => "Invalid login credentials",
            AuthApi.AuthenticateDenyResponseCode.AccountUnconfirmed =>
                "The email address for this account still needs to be confirmed. " +
                "Please confirm your email address before trying to log in",

            // Never shown I hope.
            AuthApi.AuthenticateDenyResponseCode.TfaRequired => "2-factor authentication required",
            AuthApi.AuthenticateDenyResponseCode.TfaInvalid => "2-factor authentication code invalid",

            AuthApi.AuthenticateDenyResponseCode.AccountLocked =>
                "Account has been locked. Please contact us if you believe this to be in error.",
            _ => "Unknown error"
        };

        return new[] { err };
    }

    public void Ok()
    {
        ParentVm.OverlayOk();
    }
}
