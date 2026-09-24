using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Runs the ClickHouse specific integration tests. A server configured through
/// <see cref="ClickHouseContainer.ConnectionStringVariable"/> wins; otherwise a disposable
/// Testcontainers instance is started, so the suite runs out of the box wherever a container
/// runtime is reachable. When neither is available the tests are skipped rather than failed.
/// </summary>
internal sealed class ClickHouseTestProvider : ITestProvider
{
    public static readonly ClickHouseTestProvider Instance = new();

    private static readonly object SeedGate = new();
    private static bool _seeded;

    public string Name => "clickhouse";

    public bool IsAvailable => ClickHouseContainer.IsAvailable;

    public bool SupportsFullJoin => true;

    // ClickHouse AVG returns Float64 even over an integer column, so the fractional result is kept.
    public bool SupportsFractionalAverage => true;

    // ClickHouse has INTERSECT ALL / EXCEPT ALL.
    public bool SupportsIntersectExceptAll => true;

    // ClickHouse implements varSamp / varPop.
    public bool SupportsVarianceAggregates => true;

    // The shared TVF test targets SQLite's json_each, which has no ClickHouse equivalent.
    public bool SupportsTableValuedFunctions => false;
    public bool EnforcesScalarSubqueryCardinality => true;
    public bool SupportsApply => false;
    public bool SupportsInsertReturning => false;
    public bool SupportsIgnoreDuplicates => false;
    public bool SupportsTruncate => true;
    public bool SupportsDeleteJoin => false;
    public bool SupportsCreateTableAsSelect => true;
    public bool SupportsTemporaryCreateTableAsSelect => false;
    public bool SupportsTransactions => false;
    public string TableValuedFunctionSkipReason => "The shared table-valued function test uses SQLite's json_each; no portable equivalent is configured for ClickHouse.";

    public string SkipReason => ClickHouseContainer.Failure ?? "ClickHouse is not available.";

    public IDataContext CreateContext() =>
        new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());

    public void EnsureSeeded()
    {
        if (_seeded) return;

        lock (SeedGate)
        {
            if (_seeded) return;

            using var ctx = new ClickHouseDataContext(ClickHouseContainer.ConnectionString, new DataContextBuilder());
            using var conn = ctx.GetConnection();
            conn.Open();

            // The ClickHouse driver sends a single statement per command, so the script is executed
            // statement by statement rather than as one batch.
            foreach (var statement in SeedStatements)
            {
                using var cmd = conn.CreateCommand();
                cmd.CommandText = statement;
                cmd.ExecuteNonQuery();
            }

            _seeded = true;
        }
    }

    private static readonly string[] SeedStatements =
    [
        "drop table if exists simple_entity",
        "drop table if exists complex_entity",
        "drop table if exists wide_entity",

        """
        create table simple_entity (id Int32) engine = Memory
        """,
        "insert into simple_entity (id) values (1),(2),(3),(4),(5),(6),(7),(8),(9),(10)",

        """
        create table wide_entity (id Int32, regionid UInt64, note String) engine = Memory
        """,
        "insert into wide_entity (id, regionid, note) values (1, 10, 'a'), (2, 20, 'b'), (3, 30, 'c')",

        """
        create table complex_entity
        (
            id Int64,
            nullableint Nullable(Int32),
            somestring Nullable(String),
            tinyval UInt8,
            small Nullable(Int16),
            r Nullable(Float32),
            d Nullable(Float64),
            m Nullable(Decimal(38, 10)),
            dt Nullable(DateTime),
            onlydate Date,
            b Nullable(Bool),
            requiredstring String
        ) engine = Memory
        """,
        """
        insert into complex_entity (id, nullableint, somestring, tinyval, small, r, d, m, dt, onlydate, b, requiredstring) values
            (1, null, 'dadfasd', 2, 3, 4, 5, 6, '2023-01-01 10:00:00', '2023-01-01', true, 'sdf'),
            (2, 1, 'xxx', 2, 3, 4, 5, 6, '2023-01-01 00:00:00', '2023-01-01', false, 'asdfgoi'),
            (3, 1, null, 2, 3, null, 5, 6, '2023-01-01 00:00:00', '2023-01-01', false, '34mfs')
        """,

        "drop table if exists array_entity",
        """
        create table array_entity
        (
            id Int32,
            tags Array(String),
            nums Array(Int32)
        ) engine = Memory
        """,
        """
        insert into array_entity (id, tags, nums) values
            (1, ['a', 'b', 'c'], [3, 1, 2]),
            (2, ['b'], [10, 20]),
            (3, [], [])
        """,

        "drop table if exists event_entity",
        """
        create table event_entity
        (
            id Int32,
            ts DateTime,
            event Int32
        ) engine = Memory
        """,
        """
        insert into event_entity (id, ts, event) values
            (1, '2023-01-01 00:00:00', 1),
            (2, '2023-01-01 00:01:00', 2),
            (3, '2023-01-01 00:02:00', 3),
            (4, '2023-01-01 00:03:00', 2)
        """,

        "drop table if exists uint64_entity",
        """
        create table uint64_entity
        (
            id UInt64,
            value UInt64,
            maybe Nullable(UInt64)
        ) engine = Memory
        """,
        """
        insert into uint64_entity (id, value, maybe) values
            (1, 18446744073709551615, 18446744073709551615),
            (2, 0, NULL),
            (3, 42, 7)
        """,

        "drop table if exists json_entity",
        """
        create table json_entity
        (
            id Int32,
            doc JSON
        ) engine = Memory
        """,
        """
        insert into json_entity (id, doc) values
            (1, '{"name":"alice","age":30,"nested":{"x":1}}'),
            (2, '{"flag":true,"list":[1,2,3]}')
        """,

        "drop table if exists tuple_entity",
        """
        create table tuple_entity
        (
            id Int32,
            pair Tuple(Int32, String)
        ) engine = Memory
        """,
        """
        insert into tuple_entity (id, pair) values
            (1, (7, 'seven')),
            (2, (9, 'nine'))
        """,

        "drop table if exists insert_entity",
        """
        create table insert_entity
        (
            id Int64 DEFAULT 0,
            name Nullable(String),
            age Int32
        ) engine = Memory
        """
    ];
}
