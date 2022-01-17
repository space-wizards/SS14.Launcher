using System;
using System.Diagnostics;
using System.IO;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Models.ContentManagement;

public sealed class ContentManager
{
    public void Initialize()
    {
        var con = GetSqliteConnection();
        con.Open();

        // I tried to set this from inside the migrations but didn't work, rip.
        // Anyways: enabling WAL mode here so that downloading new files doesn't lock up if your game is running.
        con.Execute("PRAGMA journal_mode=WAL");

        Log.Debug("Migrating content database...");

        var sw = Stopwatch.StartNew();
        var success = Migrator.Migrate(con, "SS14.Launcher.Models.ContentManagement.Migrations");
        if (!success)
            throw new Exception("Migrations failed!");

        Log.Debug("Did migrations in {MigrationTime}", sw.Elapsed);
    }

    public void ClearAll()
    {

    }

    private static string GetContentDbConnectionString()
    {
        var path = Path.Combine(LauncherPaths.DirLocalData, "content.db");
        return $"Data Source={path};Mode=ReadWriteCreate";
    }

    private static SqliteConnection GetSqliteConnection()
    {
        return new SqliteConnection(GetContentDbConnectionString());
    }
}
