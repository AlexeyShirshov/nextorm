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
    public bool SupportsTransactions => true;
    public string TableValuedFunctionSkipReason => "The shared table-valued function test uses SQLite's json_each; SQL Server exposes row-returning JSON through CROSS APPLY OPENJSON instead.";

    public string SkipReason => SqlServerContainer.Failure ?? "SQL Server is not available.";

    public IDataContext CreateContext() =>
        new SqlServerDataContext(SqlServerContainer.ConnectionString, new DataContextBuilder());

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
        drop table if exists xml_entity;
        drop table if exists simple_entity;
        drop table if exists insert_entity;
        drop table if exists merge_entity;
        drop table if exists delete_entity;

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
        """;
}
