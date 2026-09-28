using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Integration.Tests;

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

    public bool SupportsFullJoin => true;

    public bool SupportsFractionalAverage => true;

    // PostgreSQL implements INTERSECT ALL / EXCEPT ALL.
    public bool SupportsIntersectExceptAll => true;

    // PostgreSQL implements var/variance and varp/var_pop.
    public bool SupportsVarianceAggregates => true;

    // The shared TVF test targets SQLite's json_each; PostgreSQL would need its own json_each
    // (jsonb/json return types differ), so it is skipped for now.
    public bool SupportsTableValuedFunctions => false;
    public bool EnforcesScalarSubqueryCardinality => true;
    public bool SupportsApply => true;
    public bool SupportsInsertReturning => true;
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
    public bool SupportsLobStreaming => true;
    public bool SupportsLobDataReader => true;
    public bool SupportsZeroColumnResult => true;
    public string TableValuedFunctionSkipReason => "The shared table-valued function test uses SQLite's json_each; no portable equivalent is configured for PostgreSQL.";

    public string SkipReason => PostgresContainer.Failure ?? "PostgreSQL is not available.";

    public IDataContext CreateContext() =>
        new PostgresDataContext(PostgresContainer.ConnectionString, new DataContextBuilder());

    public void EnsureSeeded()
    {
        if (_seeded) return;

        lock (SeedGate)
        {
            if (_seeded) return;

            using var ctx = new PostgresDataContext(PostgresContainer.ConnectionString, new DataContextBuilder());
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
        drop table if exists binary_entity;
        drop table if exists pg_oid_entity;
        drop table if exists lob_entity;
        drop table if exists simple_entity;
        drop table if exists insert_entity;
        drop table if exists merge_entity;
        drop table if exists delete_entity;
        drop table if exists dynamic_entity;
        drop table if exists eager_note;
        drop table if exists eager_child;
        drop table if exists eager_parent;
        drop table if exists query_filter_target;
        drop table if exists query_filter_entity;

        create table simple_entity (id integer primary key);
        insert into simple_entity (id) select generate_series(1, 10);

        create table complex_entity
        (
            id bigint primary key,
            nullableint integer,
            somestring varchar(100),
            tinyval smallint not null,
            small smallint,
            r real,
            d double precision,
            m numeric,
            dt timestamp,
            onlydate date not null,
            b boolean,
            requiredstring text not null
        );
        insert into complex_entity (id, nullableint, somestring, tinyval, small, r, d, m, dt, onlydate, b, requiredstring) values
            (1, null, 'dadfasd', 2, 3, 4, 5, 6, timestamp '2023-01-01 10:00:00', date '2023-01-01', true, 'sdf'),
            (2, 1, 'xxx', 2, 3, 4, 5, 6, timestamp '2023-01-01 00:00:00', date '2023-01-01', false, 'asdfgoi'),
            (3, 1, null, 2, 3, null, 5, 6, timestamp '2023-01-01 00:00:00', date '2023-01-01', false, '34mfs');

        create table binary_entity
        (
            id integer primary key,
            data bytea
        );
        insert into binary_entity (id, data) values
            (1, decode('01020304', 'hex')),
            (2, null);

        -- oid/cid are 32-bit unsigned system types (CLR uint in Npgsql); nextorm binds uint as xid,
        -- so these columns are read directly and filtered only through an explicit bigint cast.
        create table pg_oid_entity
        (
            id integer primary key,
            oid_value oid,
            cid_value cid
        );
        insert into pg_oid_entity (id, oid_value, cid_value) values
            (1, 42::oid, '7'::cid);

        -- 8 MiB of 0xAB and 8 MiB of 'x'; repeat() avoids embedding the payload in the seed script.
        create table lob_entity
        (
            id integer primary key,
            data bytea,
            body text
        );
        insert into lob_entity (id, data, body) values
            (1, decode(repeat('ab', 8388608), 'hex'), repeat('x', 8388608));

        create table insert_entity
        (
            id bigint generated by default as identity primary key,
            name varchar(100),
            age integer
        );

        create table merge_entity
        (
            id integer primary key,
            name varchar(100),
            age integer
        );

        create table delete_entity
        (
            id integer primary key,
            name varchar(100),
            age integer
        );

        -- Dynamic-columns write fixtures (#104): the defaulted "seeded" column makes a key omitted from
        -- the store distinguishable from a key bound to a value.
        create table dynamic_entity
        (
            id integer primary key,
            name varchar(100),
            alpha varchar(100),
            beta varchar(100),
            seeded varchar(100) default 'defaulted'
        );

        create table eager_parent
        (
            id integer primary key,
            name varchar(100)
        );

        create table eager_child
        (
            id integer primary key,
            parent_id integer not null,
            name varchar(100)
        );

        create table eager_note
        (
            id integer primary key,
            parent_id integer not null,
            text varchar(100)
        );

        -- Global query filter fixtures (#108 D6): the filtered source table and the INSERT ... SELECT
        -- target table. Both carry the tenant/soft-delete columns the shared suite filters on.
        create table query_filter_entity
        (
            id integer primary key,
            tenant_id integer not null,
            is_deleted boolean not null,
            name varchar(100)
        );

        create table query_filter_target
        (
            id integer generated by default as identity primary key,
            tenant_id integer,
            is_deleted boolean not null,
            name varchar(100)
        );
        """;
}
