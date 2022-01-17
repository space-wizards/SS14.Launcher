using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;

namespace SS14.Launcher.Models.Data;

// Yeah I used to use DbUp for this, then I realized it's a complete disaster of a library and I threw it in the bin.
// This is easy enough to do anyways.

/// <summary>
/// Utility class to do SQLite database migrations.
/// </summary>
public static class Migrator
{
    public static void Migrate(SqliteConnection connection, string scriptPrefix)
    {
        connection.
    }

    internal static void LoadMigrationScriptsList(UpgradeConfiguration cfg, string prefix)
    {
        cfg.sc

        foreach (var type in assembly.GetTypes())
        {
            if (type.Namespace == prefix && type.Name.StartsWith("Script"))
                return
        }
    }

    private static IEnumerable<SqlScript> MigrationSqlScriptList(string prefix)
    {
        var assembly = typeof(DataManager).Assembly;
        foreach (var resourceName in assembly.GetManifestResourceNames())
        {
            if (!resourceName.EndsWith(".sql") || !resourceName.StartsWith(prefix))
                continue;

            var index = resourceName.LastIndexOf('.', resourceName.Length - 5, resourceName.Length - 4);
            index += 1;

            var name = resourceName[index..^4];
            yield return new LazySqlScript(name, () =>
            {
                using var reader = new StreamReader(assembly.GetManifestResourceStream(resourceName)!);

                return reader.ReadToEnd();
            });
        }
    }

}
