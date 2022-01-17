using System.Diagnostics;
using System.IO;
using DbUp.SQLite.Helpers;
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

        Log.Debug("Migrating content database...");

        var sw = Stopwatch.StartNew();
        var result = DbUp.DeployChanges.To
            .SQLiteDatabase(new SharedConnection(con))
            .WithScripts(DataManager.LoadMigrationScriptsList("SS14.Launcher.Models.ContentManagement.Migrations"))
            .LogToAutodetectedLog()
            .WithTransactionPerScript()
            .Build()
            .PerformUpgrade();

        if (result.Error is { } error)
            throw error;

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
