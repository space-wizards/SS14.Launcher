using System;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace SS14.Launcher.Models.Data;

public class LoginInfo : ReactiveObject
{
    [Reactive]
    public Guid UserId { get; set; }
    [Reactive]
    public string Username { get; set; } = default!;
    [Reactive]
    public LoginToken Token { get; set; }

    public override string ToString()
    {
        return $"{Username}/{UserId}";
    }
}
