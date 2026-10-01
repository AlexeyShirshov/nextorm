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
    public bool SupportsIgnoreDuplicates => true;
    public bool SupportsTruncate => false;
    public bool SupportsDeleteJoin => false;
    public bool SupportsCreateTableAsSelect => true;
    public bool SupportsTemporaryCreateTableAsSelect => true;
    public bool SupportsBatch => true;
    public bool SupportsStoredProcedures => false;

    /// <summary>SQLite binds table-valued parameters through a JSON document.</summary>
    public bool SupportsTableValuedParameters => true;
    public bool SupportsTransactions => true;
    public bool SupportsRegex => true;
    public bool SupportsLobStreaming => true;
    public string TableValuedFunctionSkipReason => string.Empty;
    public string SkipReason => string.Empty;

    public IDataContext CreateContext() => CreateContext(null);

    public IDataContext CreateContext(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory)
    {
        var builder = new DataContextBuilder();
        if (loggerFactory is not null)
            builder = builder.UseLoggerFactory(loggerFactory);
        return new SqliteDataContext($"Data Source='{DatabasePath}'", builder);
    }

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

        -- 8 MiB of 0xAB and 8 MiB of 'x'. hex(zeroblob(n)) is 2n zero characters, so replacing each
        -- '00' pair produces the repeated payload; unhex turns the blob hex back into bytes.
        create table lob_entity (id integer primary key, data blob, body text);
        insert into lob_entity (id, data, body) values
            (1, unhex(replace(hex(zeroblob(8388608)), '00', 'AB')),
                replace(hex(zeroblob(8388608)), '00', 'x'));

        create table insert_entity (id integer primary key autoincrement, name text, age int);

        create table merge_entity (id integer primary key, name text, age int);

        create table delete_entity (id integer primary key, name text, age int);

        -- Dynamic-columns write fixtures (#104): the defaulted "seeded" column makes a key omitted from
        -- the store distinguishable from a key bound to a value.
        create table dynamic_entity (id integer primary key, name text, alpha text, beta text, seeded text default 'defaulted');

        create table eager_parent (id integer primary key, name text);
        create table eager_child (id integer primary key, parent_id int not null, name text);
        create table eager_note (id integer primary key, parent_id int not null, text text);

        -- Many-to-many JoinInto fixtures (#135): the tag child plus the junction linking it to a parent.
        create table eager_tag (id integer primary key, name text);
        create table eager_link (id integer primary key, parent_id int not null, child_id int not null);

        -- Global query filter fixtures (#108 D6): the filtered source table and the INSERT ... SELECT
        -- target table. Both carry the tenant/soft-delete columns the shared suite filters on.
        create table query_filter_entity (id integer primary key, tenant_id int not null, is_deleted integer not null, name text);
        create table query_filter_target (id integer primary key autoincrement, tenant_id int null, is_deleted integer not null, name text);

        -- SelectWhereMax/SelectWhereMin fixtures (#115): a nullable comparison value (score) and a
        -- nullable group key (category), with ties, a null group, an all-null group and a category
        -- absent from the data (for the empty-result case).
        create table extrema_entity (id integer primary key, score int null, category varchar(50) null, label varchar(50) not null);
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
        create table person (id integer primary key, name text);
        insert into person (id, name) values (10, 'Buyer'), (20, 'Approver'), (30, 'Unlinked');

        create table orders (id integer primary key, buyer_id integer not null, approver_id integer not null);
        insert into orders (id, buyer_id, approver_id) values (1, 10, 20), (2, 20, 10);
        """;
}
