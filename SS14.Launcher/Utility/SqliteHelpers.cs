using System;
using Microsoft.Data.Sqlite;
using SQLitePCL;
using static SQLitePCL.raw;

namespace SS14.Launcher.Utility;

/// <summary>
/// Helper functions for making the raw SQLite API just a little bit easier to use.
/// </summary>
public static class SqliteHelpers
{
    public static sqlite3_stmt Prepare(this sqlite3 db, string sql)
    {
        var err = sqlite3_prepare_v2(db, sql, out var statement);
        SqliteException.ThrowExceptionForRC(err, db);
        return statement;
    }

    public static void BindBlob(this sqlite3_stmt statement, sqlite3 db, int index, ReadOnlySpan<byte> blob)
    {
        var err = sqlite3_bind_blob(statement, index, blob);
        SqliteException.ThrowExceptionForRC(err, db);
    }

    public static void BindInt(this sqlite3_stmt statement, sqlite3 db, int index, int value)
    {
        var err = sqlite3_bind_int(statement, index, value);
        SqliteException.ThrowExceptionForRC(err, db);
    }

    public static void BindInt64(this sqlite3_stmt statement, sqlite3 db, int index, long value)
    {
        var err = sqlite3_bind_int64(statement, index, value);
        SqliteException.ThrowExceptionForRC(err, db);
    }

    public static void BindString(this sqlite3_stmt statement, sqlite3 db, int index, string value)
    {
        var err = sqlite3_bind_text(statement, index, value);
        SqliteException.ThrowExceptionForRC(err, db);
    }

    public static int Step(this sqlite3_stmt statement, sqlite3 db)
    {
        var err = sqlite3_step(statement);
        SqliteException.ThrowExceptionForRC(err, db);
        return err;
    }

    public static void Reset(this sqlite3_stmt statement, sqlite3 db)
    {
        var err = sqlite3_reset(statement);
        SqliteException.ThrowExceptionForRC(err, db);
    }
}
