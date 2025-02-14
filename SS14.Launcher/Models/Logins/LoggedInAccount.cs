using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Models.Logins;

public abstract class LoggedInAccount : ObservableObject
{
    public string Username => LoginInfo.Username;
    public Guid UserId => LoginInfo.UserId;

    protected LoggedInAccount(LoginInfo loginInfo)
    {
        LoginInfo = loginInfo;
    }

    public LoginInfo LoginInfo { get; }

    public abstract AccountLoginStatus Status { get; }
}
