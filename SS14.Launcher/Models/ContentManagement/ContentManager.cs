using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Serilog;
using SS14.Launcher.Models.Data;

namespace SS14.Launcher.Models.ContentManagement;

public sealed class ContentManager
{
    public void Initialize()
    {
        using var con = GetSqliteConnection();

        // I tried to set this from inside the migrations but didn't work, rip.
        // Anyways: enabling WAL mode here so that downloading new files doesn't lock up if your game is running.
        con.Execute("PRAGMA journal_mode=WAL");

        Log.Debug("Migrating content database...");

        var sw = Stopwatch.StartNew();
        var success = Migrator.Migrate(con, "SS14.Launcher.Models.ContentManagement.Migrations");
        if (!success)
            throw new Exception("Migrations failed!");

        Log.Debug("Did content DB migrations in {MigrationTime}", sw.Elapsed);
    }

    /// <summary>
    /// Clear ALL installed server content and try to truncate the DB.
    /// </summary>
    public void ClearAll()
    {
        Task.Run(() =>
        {
            try
            {
                using var con = GetSqliteConnection();

                using var transact = con.BeginTransaction();
                con.Execute("DELETE FROM ContentVersion");
                con.Execute("DELETE FROM Content");
                transact.Commit();

                con.Execute("VACUUM");
            }
            catch (Exception e)
            {
                Log.Error(e, "Error while truncating content DB!");
            }
        });
    }

    public static SqliteConnection GetSqliteConnection()
    {
        var con = new SqliteConnection(GetContentDbConnectionString());
        con.Open();
        return con;
    }

    private static string GetContentDbConnectionString()
    {
        return $"Data Source={LauncherPaths.PathContentDb};Mode=ReadWriteCreate";
    }
}
