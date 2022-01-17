using System;
using System.Data;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SS14.Launcher.Models.Data.Migrations;

public sealed class Script0002_ContentDB : Migrator.IMigrationScript
{
    public string Up(SqliteConnection connection)
    {
        if (Directory.Exists(LauncherPaths.DirServerContent))
            Directory.Delete(LauncherPaths.DirServerContent, true);

        return "DROP TABLE ServerContent";
    }
}
