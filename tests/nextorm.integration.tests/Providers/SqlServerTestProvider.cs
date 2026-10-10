using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Integration.Tests;

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

    public bool SupportsFullJoin => true;

    // T-SQL evaluates AVG over an integer column as an integer, so 5.5 becomes 5.
    public bool SupportsFractionalAverage => false;

    // SQL Server has no INTERSECT ALL / EXCEPT ALL.
    public bool SupportsIntersectExceptAll => false;

    // SQL Server implements VAR and VARP natively.
    public bool SupportsVarianceAggregates => true;

    // The shared TVF test targets SQLite's json_each. SQL Server's OPENJSON is applied with CROSS
    // APPLY rather than used as a FROM table-valued function, so the test is skipped there.
    public bool SupportsTableValuedFunctions => false;
    public bool EnforcesScalarSubqueryCardinality => true;
    public bool SupportsApply => true;
    public bool SupportsInsertReturning => true;
    public bool SupportsIgnoreDuplicates => false;
    public bool SupportsTruncate => true;
    public bool SupportsDeleteJoin => true;
    public bool SupportsCreateTableAsSelect => true;
    public bool SupportsTemporaryCreateTableAsSelect => false;
    public bool SupportsBatch => true;
    public bool SupportsStoredProcedures => true;

    /// <summary>The provider binds table-valued parameters (native SQL Server UDTT or JSON/array emulation).</summary>
    public bool SupportsTableValuedParameters => true;
    public bool SupportsTransactions => true;
    public bool SupportsRegex => true;
    public bool SupportsLobStreaming => true;
    public bool SupportsLobDataReader => true;
    public string TableValuedFunctionSkipReason => "The shared table-valued function test uses SQLite's json_each; SQL Server exposes row-returning JSON through CROSS APPLY OPENJSON instead.";

    public string SkipReason => SqlServerContainer.Failure ?? "SQL Server is not available.";

    public IDataContext CreateContext() => CreateContext(null);

    public IDataContext CreateContext(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        var builder = new DataContextBuilder();
        if (loggerFactory is not null)
            builder = builder.UseLoggerFactory(loggerFactory);
        return new SqlServerDataContext(SqlServerContainer.ConnectionString, builder);
    }

    public void EnsureSeeded()
    {
        if (_seeded) return;

        lock (SeedGate)
        {
            if (_seeded) return;

            using var ctx = new SqlServerDataContext(SqlServerContainer.ConnectionString, new DataContextBuilder());
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
        drop table if exists binary_entity;
        drop table if exists enum_entity;
        drop table if exists lob_entity;
        drop table if exists xml_entity;
        drop table if exists simple_entity;
        drop table if exists insert_entity;
        drop table if exists merge_entity;
        drop table if exists delete_entity;
        drop table if exists dynamic_entity;
        drop table if exists eager_link;
        drop table if exists eager_tag;
        drop table if exists eager_note;
        drop table if exists eager_child;
        drop table if exists eager_parent;
        drop table if exists query_filter_target;
        drop table if exists query_filter_entity;
        drop table if exists extrema_entity;
        drop table if exists orders;
        drop table if exists person;

        create table simple_entity (id int not null primary key);

        insert into simple_entity (id) values (1),(2),(3),(4),(5),(6),(7),(8),(9),(10);

        drop table if exists pivot_entity;

        create table pivot_entity (id int not null primary key, q1 int null, q2 int null);

        insert into pivot_entity (id, q1, q2) values (1, 10, 20), (2, 30, 40);

        create table complex_entity
        (
            id bigint not null primary key,
            nullableint int null,
            somestring varchar(100) null,
            tinyval tinyint not null,
            small smallint null,
            r real null,
            d float null,
            m decimal(18, 2) null,
            dt datetime2 null,
            onlydate date not null,
            b bit null,
            requiredstring varchar(100) not null
        );

        insert into complex_entity (id, nullableint, somestring, tinyval, small, r, d, m, dt, onlydate, b, requiredstring) values
            (1, null, 'dadfasd', 2, 3, 4, 5, 6, '2023-01-01T10:00:00', '2023-01-01', 1, 'sdf'),
            (2, 1, 'xxx', 2, 3, 4, 5, 6, '2023-01-01T00:00:00', '2023-01-01', 0, 'asdfgoi'),
            (3, 1, null, 2, 3, null, 5, 6, '2023-01-01T00:00:00', '2023-01-01', 0, '34mfs');

        create table binary_entity
        (
            id int not null primary key,
            data varbinary(max) null
        );

        insert into binary_entity (id, data) values (1, 0x01020304), (2, null);

        -- D178 (#178) enum storage: an enum is persisted as its underlying integer.
        create table enum_entity
        (
            id int not null primary key,
            state int not null,
            nullable_state int null
        );

        insert into enum_entity (id, state, nullable_state) values (1, 7, null), (2, -3, 7), (3, 0, -3);

        -- 8 MiB of 0xAB and 8 MiB of N'x'. REPLICATE converts a binary argument to varchar(max),
        -- so the blob is assembled by doubling an 8-byte seed (20 doublings -> 8,388,608 bytes).
        create table lob_entity
        (
            id int not null primary key,
            data varbinary(max) null,
            body nvarchar(max) null
        );

        declare @lob varbinary(max) = 0xABABABABABABABAB;
        set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob;
        set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob;
        set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob;
        set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob;
        set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob; set @lob = @lob + @lob;

        insert into lob_entity (id, data, body) values
            (1, @lob, replicate(cast(N'x' as nvarchar(max)), 8388608));

        create table xml_entity
        (
            id int not null primary key,
            payload xml null
        );

        insert into xml_entity (id, payload) values
            (1, N'<root><item id="1">alpha</item><item id="2">beta</item></root>');

        create table insert_entity
        (
            id bigint identity(1,1) primary key,
            name nvarchar(100) null,
            age int null
        );

        create table merge_entity
        (
            id int not null primary key,
            name nvarchar(100) null,
            age int null
        );

        create table delete_entity
        (
            id int not null primary key,
            name nvarchar(100) null,
            age int null
        );

        -- Dynamic-columns write fixtures (#104): the defaulted "seeded" column makes a key omitted from
        -- the store distinguishable from a key bound to a value.
        create table dynamic_entity
        (
            id int not null primary key,
            name nvarchar(100) null,
            alpha nvarchar(100) null,
            beta nvarchar(100) null,
            seeded nvarchar(100) default N'defaulted'
        );

        create table eager_parent
        (
            id int not null primary key,
            name nvarchar(100) null
        );

        create table eager_child
        (
            id int not null primary key,
            parent_id int not null,
            name nvarchar(100) null
        );

        create table eager_note
        (
            id int not null primary key,
            parent_id int not null,
            text nvarchar(100) null
        );

        create table eager_tag
        (
            id int not null primary key,
            name nvarchar(100) null
        );

        create table eager_link
        (
            id int not null primary key,
            parent_id int not null,
            child_id int not null
        );

        -- Global query filter fixtures (#108 D6): the filtered source table and the INSERT ... SELECT
        -- target table. Both carry the tenant/soft-delete columns the shared suite filters on.
        create table query_filter_entity
        (
            id int not null primary key,
            tenant_id int not null,
            is_deleted bit not null,
            name nvarchar(100) null
        );

        create table query_filter_target
        (
            id int identity(1,1) primary key,
            tenant_id int null,
            is_deleted bit not null,
            name nvarchar(100) null
        );

        -- SelectWhereMax/SelectWhereMin fixtures (#115): a nullable comparison value (score) and a
        -- nullable group key (category), with ties, a null group, an all-null group and a category
        -- absent from the data (for the empty-result case).
        create table extrema_entity
        (
            id int not null primary key,
            score int null,
            category nvarchar(50) null,
            label nvarchar(50) not null
        );
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
            (11, null, 'd', 'eleven');

        -- Join-alias fixtures (#113): one order whose buyer and approver are two different people,
        -- plus an unlinked person so RIGHT/FULL alias joins have an unmatched row to return.
        create table person (id int not null primary key, name nvarchar(100) null);
        insert into person (id, name) values (10, N'Buyer'), (20, N'Approver'), (30, N'Unlinked');

        create table orders (id int not null primary key, buyer_id int not null, approver_id int not null);
        insert into orders (id, buyer_id, approver_id) values (1, 10, 20), (2, 20, 10);
        """;
}
