using nextorm.core;
using nextorm.sqlserver;

namespace nextorm.integration.tests;

/// <summary>
/// Runs the integration suite against Microsoft SQL Server. A server configured through
/// <see cref="SqlServerContainer.ConnectionStringVariable"/> wins; otherwise a disposable
/// Testcontainers instance is started, so the suite runs out of the box wherever a container
/// runtime is reachable. When neither is available the tests are skipped rather than failed.
/// </summary>
internal sealed class SqlServerTestProvider : ITestProvider
{
    public static readonly SqlServerTestProvider Instance = new();

    private static readonly object SeedGate = new();
    private static bool _seeded;

    public string Name => "sqlserver";

    public bool IsAvailable => SqlServerContainer.IsAvailable;

    // T-SQL evaluates AVG over an integer column as an integer, so 5.5 becomes 5.
    public bool SupportsFractionalAverage => false;

    public string SkipReason => SqlServerContainer.Failure ?? "SQL Server is not available.";

    public IDataContext CreateContext() =>
        new SqlServerDbContext(SqlServerContainer.ConnectionString, new DbContextBuilder());

    public void EnsureSeeded()
    {
        if (_seeded) return;

        lock (SeedGate)
        {
            if (_seeded) return;

            using var ctx = new SqlServerDbContext(SqlServerContainer.ConnectionString, new DbContextBuilder());
            using var conn = ctx.GetConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = SeedSql;
            cmd.ExecuteNonQuery();

            _seeded = true;
        }
    }

    // Mirrors the schema and rows of the other providers. T-SQL specifics: bit for booleans,
    // datetime2/date instead of timestamp, float for double and no generate_series.
    private const string SeedSql =
        """
        drop table if exists complex_entity;
        drop table if exists simple_entity;

        create table simple_entity (id int not null primary key);

        insert into simple_entity (id) values (1),(2),(3),(4),(5),(6),(7),(8),(9),(10);

        create table complex_entity
        (
            id bigint not null primary key,
            nullableint int null,
            somestring varchar(100) null,
            tinyint tinyint not null,
            small smallint null,
            r real null,
            d float null,
            m decimal(18, 2) null,
            dt datetime2 null,
            onlydate date not null,
            b bit null,
            requiredstring varchar(100) not null
        );

        insert into complex_entity (id, nullableint, somestring, tinyint, small, r, d, m, dt, onlydate, b, requiredstring) values
            (1, null, 'dadfasd', 2, 3, 4, 5, 6, '2023-01-01T10:00:00', '2023-01-01', 1, 'sdf'),
            (2, 1, 'xxx', 2, 3, 4, 5, 6, '2023-01-01T00:00:00', '2023-01-01', 0, 'asdfgoi'),
            (3, 1, null, 2, 3, null, 5, 6, '2023-01-01T00:00:00', '2023-01-01', 0, '34mfs');
        """;
}
