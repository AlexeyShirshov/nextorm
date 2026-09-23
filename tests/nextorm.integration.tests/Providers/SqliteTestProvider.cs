using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the integration suite against a throwaway SQLite database created in the temp folder.
/// The schema and the seed rows mirror the data the suite was originally written against.
/// </summary>
internal sealed class SqliteTestProvider : ITestProvider
{
    public static readonly SqliteTestProvider Instance = new();

    private static readonly object SeedGate = new();
    private static bool _seeded;

    public static string DatabasePath { get; } =
        Path.Combine(Path.GetTempPath(), $"nextorm.integration.{Environment.ProcessId}.db");

    public string Name => "sqlite";
    public bool IsAvailable => true;
    public bool SupportsFullJoin => true;
    public bool SupportsFractionalAverage => true;
    // SQLite has no INTERSECT ALL / EXCEPT ALL.
    public bool SupportsIntersectExceptAll => false;
    // SQLite gets all four aggregates from the custom implementations in SQLiteFunctions.
    public bool SupportsVarianceAggregates => true;
    // The bundled SQLite has JSON1 enabled, so json_each(...) is available as a row-returning function.
    public bool SupportsTableValuedFunctions => true;
    public bool EnforcesScalarSubqueryCardinality => false;
    public bool SupportsApply => false;
    public bool SupportsInsertReturning => true;
    public bool SupportsTruncate => false;
    public bool SupportsDeleteJoin => false;
    public string TableValuedFunctionSkipReason => string.Empty;
    public string SkipReason => string.Empty;

    public IDataContext CreateContext() =>
        new SqliteDataContext($"Data Source='{DatabasePath}'", new DataContextBuilder());

    public void EnsureSeeded()
    {
        if (_seeded) return;

        lock (SeedGate)
        {
            if (_seeded) return;

            if (File.Exists(DatabasePath))
                File.Delete(DatabasePath);

            using var ctx = new SqliteDataContext($"Data Source='{DatabasePath}'", new DataContextBuilder());
            using var conn = ctx.GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = SeedSql;
            cmd.ExecuteNonQuery();

            _seeded = true;
        }
    }

    private const string SeedSql =
        """
        create table simple_entity (id integer primary key);
        insert into simple_entity (id) values (1),(2),(3),(4),(5),(6),(7),(8),(9),(10);

        create table complex_entity
        (
            id integer primary key,
            nullableint int null,
            somestring varchar(100),
            tinyval tinyint not null,
            small smallint null,
            r real,
            d double,
            m numeric,
            dt datetime,
            onlydate date not null,
            b boolean,
            requiredstring text not null
        );
        insert into complex_entity (id, nullableint, somestring, tinyval, small, r, d, m, dt, onlydate, b, requiredstring) values
            (1, null, 'dadfasd', 2, 3, 4, 5, 6, '2023-01-01 10:00:00', '2023-01-01', 1, 'sdf'),
            (2, 1, 'xxx', 2, 3, 4, 5, 6, '2023-01-01 00:00:00', '2023-01-01', 0, 'asdfgoi'),
            (3, 1, null, 2, 3, null, 5, 6, '2023-01-01 00:00:00', '2023-01-01', 0, '34mfs');

        create table binary_entity (id integer primary key, data blob);
        insert into binary_entity (id, data) values (1, X'01020304'), (2, null);

        create table insert_entity (id integer primary key autoincrement, name text, age int);

        create table merge_entity (id integer primary key, name text, age int);

        create table delete_entity (id integer primary key, name text, age int);
        """;
}
