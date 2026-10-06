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
    public bool SupportsBatch => false;
    public bool SupportsStoredProcedures => false;

    /// <summary>ClickHouse emulates a table parameter with a native array expanded server-side with <c>arrayJoin(@p)</c>.</summary>
    public bool SupportsTableValuedParameters => true;
    public bool SupportsTransactions => false;
    public bool SupportsRegex => true;
    public bool SupportsLobStreaming => false;
    public string TableValuedFunctionSkipReason => "The shared table-valued function test uses SQLite's json_each; no portable equivalent is configured for ClickHouse.";

    public string SkipReason => ClickHouseContainer.Failure ?? "ClickHouse is not available.";

    public IDataContext CreateContext() => CreateContext(null);

    public IDataContext CreateContext(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        var builder = new DataContextBuilder();
        if (loggerFactory is not null)
            builder = builder.UseLoggerFactory(loggerFactory);
        return new ClickHouseDataContext(ClickHouseContainer.ConnectionString, builder);
    }

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

        // #128: a separate bare JsonObject mapping over a native JSON column. The json_entity fixture
        // above keeps IJsonEntity.Doc as a string (the existing SQL-function path); this table backs the
        // bare JsonObject projection/parameter round-trip.
        "drop table if exists json_object_entity",
        """
        create table json_object_entity
        (
            id Int32,
            doc JSON
        ) engine = Memory
        """,
        """
        insert into json_object_entity (id, doc) values
            (1, '{"name":"bob","age":25,"nested":{"x":2}}'),
            (2, '{}')
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

        "drop table if exists dynamic_entity",
        """
        create table dynamic_entity
        (
            id Int32,
            name Nullable(String),
            alpha Nullable(String),
            beta Nullable(String),
            seeded Nullable(String) DEFAULT 'defaulted'
        ) engine = Memory
        """,

        "drop table if exists insert_entity",
        """
        create table insert_entity
        (
            id Int64 DEFAULT 0,
            name Nullable(String),
            age Int32
        ) engine = Memory
        """,

        // Global query filter fixture (#108 D6): the SELECT side of the shared query filter suite.
        // ClickHouse does not derive CommonTestSuite, so the SELECT cases are re-pinned by
        // ClickHouseQueryFilterTests; Memory has no identity key, so ids are written explicitly.
        "drop table if exists query_filter_entity",
        """
        create table query_filter_entity
        (
            id Int32,
            tenant_id Int32,
            is_deleted Bool,
            name Nullable(String)
        ) engine = Memory
        """,

        // SelectWhereMax/SelectWhereMin fixtures: ClickHouse does not derive CommonTestSuite, so the
        // same deterministic data is re-pinned by ClickHouseIntegrationTests.
        "drop table if exists extrema_entity",
        """
        create table extrema_entity
        (
            id Int32,
            score Nullable(Int32),
            category Nullable(String),
            label String
        ) engine = Memory
        """,
        """
        insert into extrema_entity (id, score, category, label) values
            (1, null, 'a', 'one'),
            (2, 5, 'a', 'two'),
            (3, 9, 'a', 'three'),
            (4, 9, 'a', 'four'),
            (5, 3, 'b', 'five'),
            (6, 1, 'b', 'six'),
            (7, null, null, 'seven'),
            (8, 7, null, 'eight'),
            (9, 4, 'c', 'nine'),
            (10, 1, 'b', 'ten'),
            (11, null, 'd', 'eleven')
        """,

        // Extreme-row native/portable parity fixture (#144 D7): nullable integral extreme/group keys
        // (k1, k2, g), a nullable string payload (label) and a nullable integral payload (n).
        "drop table if exists extreme_parity_144",
        """
        create table extreme_parity_144
        (
            id Int32,
            g Nullable(Int32),
            k1 Nullable(Int32),
            k2 Nullable(Int32),
            label Nullable(String),
            n Nullable(Int32)
        ) engine = Memory
        """,
        """
        insert into extreme_parity_144 (id, g, k1, k2, label, n) values
            (1, 10, 1, 99, null, null),
            (2, 10, 1, 9, 'a-1-9', 7),
            (3, 10, 1, 9, 'a-1-9b', 8),
            (4, 10, null, 1, 'null-k1', 6),
            (5, 20, 2, 1, 'b-2-1', 1),
            (6, 20, 3, 0, 'b-3-0', 2),
            (7, null, 5, 5, 'null-group', 3),
            (8, 30, null, 4, 'null-k1-30', 4),
            (9, 30, null, null, 'allnull', 5),
            (10, 40, 1, 5, 'd-1-5', 9)
        """,

        // Alias-collision fixture (#144): mapped physical column names equal to the native renderers'
        // internal aliases (PG derived-table alias, CH source/tuple aliases).
        "drop table if exists extreme_alias_144",
        """
        create table extreme_alias_144
        (
            id Int32,
            `__nextorm_extreme` Nullable(Int32),
            `__nextorm_extreme_src` Nullable(Int32),
            `__nextorm_extreme_tuple` Nullable(Int32),
            g Nullable(Int32),
            k Nullable(Int32)
        ) engine = Memory
        """,
        """
        insert into extreme_alias_144 (id, `__nextorm_extreme`, `__nextorm_extreme_src`, `__nextorm_extreme_tuple`, g, k) values
            (1, 5, 1, 1, 10, 1),
            (2, 7, 2, 2, 10, 3),
            (3, 6, 3, 3, 20, 2)
        """,

        // Implicit-navigation reference fixtures (#148-B D8): ClickHouse does not derive
        // CommonTestSuite, so the reference-navigation cases are re-pinned by
        // ClickHouseImplicitNavigationTests over these Memory tables.
        "drop table if exists eager_link",
        "drop table if exists eager_tag",
        "drop table if exists eager_child",
        "drop table if exists eager_parent",
        "create table eager_parent (id Int32, name Nullable(String)) engine = Memory",
        "create table eager_child (id Int32, parent_id Int32, name Nullable(String)) engine = Memory",
        "create table eager_tag (id Int32, name Nullable(String)) engine = Memory",
        "create table eager_link (id Int32, parent_id Int32, child_id Int32) engine = Memory"
    ];
}
