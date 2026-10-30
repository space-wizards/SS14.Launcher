using SS14.Launcher.Api;
using SS14.Launcher.Localization;

namespace SS14.Launcher.ViewModels.Login;

public class AuthErrorsOverlayViewModel : ViewModelBase
{
    public IErrorOverlayOwner ParentVm { get; }
    public string Title { get; }
    public string[] Errors { get; }

    public ErrorCode? Code { get; }

    public string CodeText => Code is { } code
        ? LocalizationManager.Instance.GetString("error-code-label", ("code", code.ToString()))
          + "\n" + LocalizationManager.Instance.GetString("error-code-help")
        : "";

    public AuthErrorsOverlayViewModel(
        IErrorOverlayOwner parentVM,
        string title,
        string[] errors,
        ErrorCode? code = null)
    {
        ParentVm = parentVM;
        Title = title;
        Errors = errors;
        Code = code;
    }

    public static ErrorCode DenyCodeToErrorCode(AuthApi.AuthenticateDenyResponseCode code)
    {
        return code switch
        {
            AuthApi.AuthenticateDenyResponseCode.InvalidCredentials => ErrorCodes.AuthInvalidCredentials,
            AuthApi.AuthenticateDenyResponseCode.AccountUnconfirmed => ErrorCodes.AuthUnconfirmed,
            AuthApi.AuthenticateDenyResponseCode.TfaRequired => ErrorCodes.AuthTfaRequired,
            AuthApi.AuthenticateDenyResponseCode.TfaInvalid => ErrorCodes.AuthTfaInvalid,
            AuthApi.AuthenticateDenyResponseCode.AccountLocked => ErrorCodes.AuthLocked,
            _ => ErrorCodes.AuthUnknownDeny
        };
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
            _ => "login-error-unknown"
        };

        return new[] { loc.GetString(err) };
    }

    public void Ok()
    {
        ParentVm.OverlayOk();
    }
}
