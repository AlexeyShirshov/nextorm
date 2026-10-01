using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.AliasTests;

/// <summary>
/// Creates a throwaway SQLite file database with one <c>orders</c> row whose buyer and approver are
/// two different <c>person</c> rows. A single order row makes buyer and approver ids differ, so a
/// slot mix-up cannot pass by accidentally returning the same multiset.
/// </summary>
internal static class AliasSqliteDatabase
{
    public const int BuyerId = 10;
    public const int ApproverId = 20;

    public static (string Path, SqliteDataContext Context, SqlRecordingInterceptor Sql) CreateContext()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-alias-tests-{Guid.NewGuid():N}.db");
        using (var connection = new SqliteConnection($"Data Source={path}"))
        {
            connection.Open();
            using var cmd = connection.CreateCommand();
            cmd.CommandText =
                "create table orders (Id integer primary key, BuyerId integer, ApproverId integer);" +
                "create table person (Id integer primary key, Name text);" +
                "insert into orders (Id, BuyerId, ApproverId) values (1, 10, 20);" +
                "insert into person (Id, Name) values (10, 'Buyer'), (20, 'Approver');";
            cmd.ExecuteNonQuery();
        }

        var sql = new SqlRecordingInterceptor();
        var context = new SqliteDataContext($"Data Source={path}", new DataContextBuilder().AddInterceptor(sql));
        return (path, context, sql);
    }
}
