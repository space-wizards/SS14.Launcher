using System;
using System.Data;
using System.IO;
using DbUp.Engine;

namespace SS14.Launcher.Models.Data.Migrations;

public sealed class Script0001_ContentDB : IScript
{
    public string ProvideScript(Func<IDbCommand> dbCommandFactory)
    {
        Directory.Delete(LauncherPaths.DirServerContent, true);

        return "DROP TABLE ServerContent";
    }
}
