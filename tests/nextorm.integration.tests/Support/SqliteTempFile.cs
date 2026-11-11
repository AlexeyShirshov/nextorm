using Microsoft.Data.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Deletes a throwaway SQLite database file on hosts that keep an open database locked (Windows).
/// <para>
/// The unit suites disable pooling in their own connection strings. The EF Core bridge instead borrows
/// EF's connection, and a bridged context can keep that native handle alive until the plan-cache entries
/// that reference it are collected, so a failed delete is retried once after the finalizers run.
/// </para>
/// </summary>
internal static class SqliteTempFile
{
    public static void Delete(string path)
    {
        if (!File.Exists(path))
            return;

        SqliteConnection.ClearAllPools();
        try
        {
            File.Delete(path);
            return;
        }
        catch (IOException)
        {
            // The borrowed connection has not been collected yet; fall through to the retry below.
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        SqliteConnection.ClearAllPools();
        File.Delete(path);
    }
}
