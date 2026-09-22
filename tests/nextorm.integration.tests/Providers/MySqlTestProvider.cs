using NextORM.Core;
using NextORM.MySql;

namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the integration suite against MySQL 8. A server configured through
/// <see cref="MySqlContainer.ConnectionStringVariable"/> wins; otherwise a disposable
/// Testcontainers instance is started, so the suite runs out of the box wherever a container
/// runtime is reachable. When neither is available the tests are skipped rather than failed.
/// </summary>
internal sealed class MySqlTestProvider : ITestProvider
{
    public static readonly MySqlTestProvider Instance = new();

    private static readonly object SeedGate = new();
    private static bool _seeded;

    public string Name => "mysql";

    public bool IsAvailable => MySqlContainer.IsAvailable;

    // MySQL has a right join but no full join.
    public bool SupportsFullJoin => false;

    // MySQL AVG over an integer column returns a decimal, so the fractional result is kept.
    public bool SupportsFractionalAverage => true;

    // MySQL 8.0.31 has INTERSECT/EXCEPT, but not the ALL variants.
    public bool SupportsIntersectExceptAll => false;

    // MySQL implements stddev/stddev_pop/variance/var_pop.
    public bool SupportsVarianceAggregates => true;

    // The shared TVF test targets SQLite's json_each; MySQL's JSON_TABLE has a different shape,
    // so it is skipped for now.
    public bool SupportsTableValuedFunctions => false;
    public bool EnforcesScalarSubqueryCardinality => true;
    public bool SupportsApply => true;
    public string TableValuedFunctionSkipReason => "The shared table-valued function test uses SQLite's json_each; MySQL exposes JSON rows through JSON_TABLE with a different shape.";

    public string SkipReason => MySqlContainer.Failure ?? "MySQL is not available.";

    public IDataContext CreateContext() =>
        new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder());

    public void EnsureSeeded()
    {
        if (_seeded) return;

        lock (SeedGate)
        {
            if (_seeded) return;

            using var ctx = new MySqlDataContext(MySqlContainer.ConnectionString, new DataContextBuilder());
            using var conn = ctx.GetConnection();
            conn.Open();

            foreach (var statement in SeedStatements)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = statement;
                cmd.ExecuteNonQuery();
            }

            _seeded = true;
        }
    }

    // Mirrors the schema and rows of the other providers. MySQL specifics: tinyint(1) for
    // booleans, datetime instead of timestamp (no session time zone conversion) and varbinary
    // for binary data.
    private static readonly string[] SeedStatements =
    [
        "drop table if exists binary_entity",
        "drop table if exists complex_entity",
        "drop table if exists simple_entity",

        "create table simple_entity (id int not null primary key)",

        "insert into simple_entity (id) values (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)",

        """
        create table complex_entity
        (
            id bigint not null primary key,
            nullableint int null,
            somestring varchar(100) null,
            tinyval tinyint not null,
            small smallint null,
            r float null,
            d double null,
            m decimal(18, 2) null,
            dt datetime null,
            onlydate date not null,
            b tinyint(1) null,
            requiredstring varchar(100) not null
        )
        """,

        """
        insert into complex_entity (id, nullableint, somestring, tinyval, small, r, d, m, dt, onlydate, b, requiredstring) values
            (1, null, 'dadfasd', 2, 3, 4, 5, 6, '2023-01-01 10:00:00', '2023-01-01', true, 'sdf'),
            (2, 1, 'xxx', 2, 3, 4, 5, 6, '2023-01-01 00:00:00', '2023-01-01', false, 'asdfgoi'),
            (3, 1, null, 2, 3, null, 5, 6, '2023-01-01 00:00:00', '2023-01-01', false, '34mfs')
        """,

        "create table binary_entity (id int not null primary key, data varbinary(16) null)",

        "insert into binary_entity (id, data) values (1, x'01020304'), (2, null)",

        "drop table if exists uint64_entity",
        """
        create table uint64_entity
        (
            id bigint unsigned not null,
            value bigint unsigned not null,
            maybe bigint unsigned null
        )
        """,
        """
        insert into uint64_entity (id, value, maybe) values
            (1, 18446744073709551615, 18446744073709551615),
            (2, 0, null),
            (3, 42, 7)
        """
    ];
}
