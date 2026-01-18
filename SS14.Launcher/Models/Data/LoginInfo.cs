using System;
using Microsoft.Toolkit.Mvvm.ComponentModel;

namespace SS14.Launcher.Models.Data;

public partial class LoginInfo : ObservableObject
{
    public Guid UserId;
    public string? Username;
    [ObservableProperty] private LoginToken _token;

    public override string ToString()
    {
        return $"{Username}/{UserId}";
    }
}
