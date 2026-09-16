using nextorm.core;
using nextorm.postgres;

namespace nextorm.integration.tests;

/// <summary>
/// Runs the integration suite against PostgreSQL. A server configured through
/// <see cref="PostgresContainer.ConnectionStringVariable"/> wins; otherwise a disposable
/// Testcontainers instance is started, so the suite runs out of the box wherever a container
/// runtime is reachable. When neither is available the tests are skipped rather than failed.
/// </summary>
internal sealed class PostgresTestProvider : ITestProvider
{
    public static readonly PostgresTestProvider Instance = new();

    private static readonly object SeedGate = new();
    private static bool _seeded;

    public string Name => "postgres";

    public bool IsAvailable => PostgresContainer.IsAvailable;

    public bool SupportsFractionalAverage => true;

    public string SkipReason => PostgresContainer.Failure ?? "PostgreSQL is not available.";

    public IDataContext CreateContext() =>
        new PostgresDbContext(PostgresContainer.ConnectionString, new DbContextBuilder());

    public void EnsureSeeded()
    {
        if (_seeded) return;

        lock (SeedGate)
        {
            if (_seeded) return;

            using var ctx = new PostgresDbContext(PostgresContainer.ConnectionString, new DbContextBuilder());
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
        drop table if exists complex_entity;
        drop table if exists simple_entity;

        create table simple_entity (id integer primary key);
        insert into simple_entity (id) select generate_series(1, 10);

        create table complex_entity
        (
            id bigint primary key,
            nullableint integer,
            somestring varchar(100),
            tinyint smallint not null,
            small smallint,
            r real,
            d double precision,
            m numeric,
            dt timestamp,
            onlydate date not null,
            b boolean,
            requiredstring text not null
        );
        insert into complex_entity (id, nullableint, somestring, tinyint, small, r, d, m, dt, onlydate, b, requiredstring) values
            (1, null, 'dadfasd', 2, 3, 4, 5, 6, timestamp '2023-01-01 10:00:00', date '2023-01-01', true, 'sdf'),
            (2, 1, 'xxx', 2, 3, 4, 5, 6, timestamp '2023-01-01 00:00:00', date '2023-01-01', false, 'asdfgoi'),
            (3, 1, null, 2, 3, null, 5, 6, timestamp '2023-01-01 00:00:00', date '2023-01-01', false, '34mfs');
        """;
}
