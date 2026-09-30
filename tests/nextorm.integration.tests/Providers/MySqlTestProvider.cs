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
    public bool SupportsInsertReturning => false;
    public bool SupportsIgnoreDuplicates => true;
    public bool SupportsTruncate => true;
    public bool SupportsDeleteJoin => true;
    public bool SupportsCreateTableAsSelect => true;
    public bool SupportsTemporaryCreateTableAsSelect => true;
    public bool SupportsBatch => true;
    public bool SupportsStoredProcedures => true;

    /// <summary>The provider binds table-valued parameters (native SQL Server UDTT or JSON/array emulation).</summary>
    public bool SupportsTableValuedParameters => true;
    public bool SupportsTransactions => true;
    public bool SupportsRegex => true;
    public bool SupportsLobStreaming => false;
    public string TableValuedFunctionSkipReason => "The shared table-valued function test uses SQLite's json_each; MySQL exposes JSON rows through JSON_TABLE with a different shape.";

    /// <summary>
    /// MySQL rejects an unqualified <c>*</c> mixed with explicit select expressions, so the
    /// dynamic-columns read qualifies the appended star with the source alias
    /// (<c>select t1.id, `t1`.* from dynamic_entity as `t1`</c>) and the whole-entity read can execute.
    /// </summary>
    public bool SupportsDynamicColumnsRead => true;

    public string SkipReason => MySqlContainer.Failure ?? "MySQL is not available.";

    public IDataContext CreateContext() => CreateContext(null);

    public IDataContext CreateContext(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        var builder = new DataContextBuilder();
        if (loggerFactory is not null)
            builder = builder.UseLoggerFactory(loggerFactory);
        return new MySqlDataContext(MySqlContainer.ConnectionString, builder);
    }

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
        "drop table if exists insert_entity",
        "drop table if exists merge_entity",
        "drop table if exists delete_entity",
        "drop table if exists dynamic_entity",
        "drop table if exists eager_link",
        "drop table if exists eager_tag",
        "drop table if exists eager_note",
        "drop table if exists eager_child",
        "drop table if exists eager_parent",
        "drop table if exists query_filter_target",
        "drop table if exists query_filter_entity",
        "drop table if exists extrema_entity",

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
        """,

        """
        create table insert_entity
        (
            id bigint not null auto_increment primary key,
            name varchar(100) null,
            age int null
        )
        """,

        """
        create table merge_entity
        (
            id int not null primary key,
            name varchar(100) null,
            age int null
        )
        """,

        """
        create table delete_entity
        (
            id int not null primary key,
            name varchar(100) null,
            age int null
        )
        """,

        // Dynamic-columns write fixtures (#104): the defaulted "seeded" column makes a key omitted from
        // the store distinguishable from a key bound to a value.
        """
        create table dynamic_entity
        (
            id int not null primary key,
            name varchar(100) null,
            alpha varchar(100) null,
            beta varchar(100) null,
            seeded varchar(100) default 'defaulted'
        )
        """,

        """
        create table eager_parent
        (
            id int not null primary key,
            name varchar(100) null
        )
        """,

        """
        create table eager_child
        (
            id int not null primary key,
            parent_id int not null,
            name varchar(100) null
        )
        """,

        """
        create table eager_note
        (
            id int not null primary key,
            parent_id int not null,
            text varchar(100) null
        )
        """,

        """
        create table eager_tag
        (
            id int not null primary key,
            name varchar(100) null
        )
        """,

        """
        create table eager_link
        (
            id int not null primary key,
            parent_id int not null,
            child_id int not null
        )
        """,

        // Global query filter fixtures (#108 D6): the filtered source table and the INSERT ... SELECT
        // target table. Both carry the tenant/soft-delete columns the shared suite filters on.
        """
        create table query_filter_entity
        (
            id int not null primary key,
            tenant_id int not null,
            is_deleted tinyint(1) not null,
            name varchar(100) null
        )
        """,

        """
        create table query_filter_target
        (
            id int not null auto_increment primary key,
            tenant_id int null,
            is_deleted tinyint(1) not null,
            name varchar(100) null
        )
        """,

        // SelectWhereMax/SelectWhereMin fixtures (#115): a nullable comparison value (score) and a
        // nullable group key (category), with ties, a null group, an all-null group and a category
        // absent from the data (for the empty-result case).
        """
        create table extrema_entity
        (
            id int not null primary key,
            score int null,
            category varchar(50) null,
            label varchar(50) not null
        )
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
        """
    ];
}
