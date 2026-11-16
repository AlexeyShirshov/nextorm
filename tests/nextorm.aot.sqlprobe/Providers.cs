using System.Data.Common;
using ClickHouse.Driver.ADO;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.MariaDb;
using NextORM.MySql;
using NextORM.Postgres;
using NextORM.SqlServer;
using Npgsql;

namespace NextORM.AotSqlProbe;

/// <summary>
/// One provider the probe can run against. The probe owns its schema and seed (raw ADO, not nextorm), so a
/// shape never depends on a dataset image: the DDL is the provider's own dialect, the rows are identical
/// everywhere. A provider whose engine cannot execute a shape reports it as unsupported instead of failing,
/// and the ledger records that cell as not covered rather than silently supported.
/// </summary>
internal abstract class ProbeProvider
{
    /// <summary>The provider key accepted by <c>--provider</c>.</summary>
    public abstract string Name { get; }

    /// <summary>Creates the data context under test with the default options.</summary>
    /// <param name="connectionString">The live connection string.</param>
    /// <returns>The context every shape runs through.</returns>
    public IDataContext CreateContext(string connectionString) => CreateContext(connectionString, new DataContextBuilder());

    /// <summary>Creates the data context under test with explicit options.</summary>
    /// <param name="connectionString">The live connection string.</param>
    /// <param name="builder">The options the context is built with.</param>
    /// <returns>A context of the provider's own type.</returns>
    public abstract IDataContext CreateContext(string connectionString, DataContextBuilder builder);

    /// <summary>Creates the raw ADO connection used to seed the schema.</summary>
    /// <param name="connectionString">The live connection string.</param>
    /// <returns>A closed connection of the provider's own driver.</returns>
    public abstract DbConnection CreateConnection(string connectionString);

    /// <summary>The schema and seed, in execution order.</summary>
    public abstract IReadOnlyList<string> Schema { get; }

    /// <summary>
    /// Whether the engine can execute the shape at all. A shape the engine does not support is reported as
    /// unsupported, never as a failure, so a red run always means a real defect.
    /// </summary>
    /// <param name="shape">The shape name.</param>
    /// <returns><see langword="true"/> when the engine can run it.</returns>
    public virtual bool Supports(string shape) => true;

    /// <summary>Resolves a provider key.</summary>
    /// <param name="name">The <c>--provider</c> value, lower-case.</param>
    /// <returns>The provider, or <see langword="null"/> when the key is unknown.</returns>
    public static ProbeProvider? Create(string name) => name switch
    {
        "mysql" => new MySqlProbe(),
        "mariadb" => new MariaDbProbe(),
        "postgres" or "postgresql" => new PostgresProbe(),
        "sqlserver" or "mssql" => new SqlServerProbe(),
        "clickhouse" => new ClickHouseProbe(),
        _ => null,
    };

    /// <summary>The provider keys <c>--provider</c> accepts.</summary>
    public static string Accepted => "mysql|mariadb|postgres|sqlserver|clickhouse";

    /// <summary>
    /// The seed rows shared by every dialect: three customers and six orders, so aggregates, grouping,
    /// `HAVING` and the windows all have something to compute.
    /// </summary>
    private protected static IReadOnlyList<string> SeedRows { get; } =
    [
        "insert into aot_customer (id, name, country) values (1, 'Alpha', 'RU'), (2, 'Beta', 'DE'), (3, 'Gamma', 'RU')",
        "insert into aot_order (id, customer_id, amount, order_date) values " +
        "(1, 1, 10.50, '2024-01-02 03:04:05'), " +
        "(2, 1, 99.99, '2024-01-03 04:05:06'), " +
        "(3, 2, 5.00, '2024-01-04 05:06:07'), " +
        "(4, 2, 60.00, '2024-02-05 06:07:08'), " +
        "(5, 3, 120.25, '2024-02-06 07:08:09'), " +
        "(6, 3, 0.75, '2024-03-07 08:09:10')",
    ];

    /// <summary>
    /// The JSON seed: one document and one `NULL`, so the read path exercises both.
    /// </summary>
    private protected static string JsonSeed { get; } =
        "insert into aot_json (id, payload) values (1, '{\"name\":\"seed\",\"value\":7}'), (2, null)";

    /// <summary>
    /// The navigation and converter seed: one parent with two children and one without any (so the split
    /// loader also proves the empty collection), and three converted rows.
    /// </summary>
    private protected static IReadOnlyList<string> RelationshipSeed { get; } =
    [
        "insert into aot_parent (id, name) values (1, 'P1'), (2, 'P2')",
        "insert into aot_child (id, parent_id, name) values (10, 1, 'a'), (11, 1, 'b')",
        "insert into aot_converter (id, amount) values (1, 'v=12.5'), (2, 'v=-3'), (3, 'v=0')",
    ];
}

/// <summary>The MySQL provider.</summary>
internal sealed class MySqlProbe : ProbeProvider
{
    /// <inheritdoc/>
    public override string Name => "mysql";

    /// <inheritdoc/>
    public override IDataContext CreateContext(string connectionString, DataContextBuilder builder)
        => new MySqlDataContext(connectionString, builder);

    /// <inheritdoc/>
    public override DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);

    /// <inheritdoc/>
    public override IReadOnlyList<string> Schema { get; } =
    [
        "drop table if exists aot_order",
        "drop table if exists aot_customer",
        "create table aot_customer (id int not null primary key, name varchar(64) not null, country varchar(2) not null)",
        "create table aot_order (id int not null primary key, customer_id int not null, amount decimal(10,2) not null, order_date datetime(6) not null)",
        "drop table if exists aot_write",
        "create table aot_write (id int not null primary key, name varchar(64) not null, age int not null)",
        "drop table if exists aot_json",
        "create table aot_json (id int not null primary key, payload longtext null)",
        "drop table if exists aot_child",
        "drop table if exists aot_parent",
        "create table aot_parent (id int not null primary key, name varchar(64) not null)",
        "create table aot_child (id int not null primary key, parent_id int not null, name varchar(64) not null)",
        "drop table if exists aot_converter",
        "create table aot_converter (id int not null primary key, amount varchar(32) not null)",
        JsonSeed,
        .. RelationshipSeed,
        .. SeedRows,
    ];
}

/// <summary>The MariaDB provider.</summary>
internal sealed class MariaDbProbe : ProbeProvider
{
    /// <inheritdoc/>
    public override string Name => "mariadb";

    /// <inheritdoc/>
    public override IDataContext CreateContext(string connectionString, DataContextBuilder builder)
        => new MariaDbDataContext(connectionString, builder);

    /// <inheritdoc/>
    public override DbConnection CreateConnection(string connectionString) => new MySqlConnection(connectionString);

    /// <inheritdoc/>
    public override IReadOnlyList<string> Schema { get; } = new MySqlProbe().Schema;
}

/// <summary>The PostgreSQL provider.</summary>
internal sealed class PostgresProbe : ProbeProvider
{
    /// <inheritdoc/>
    public override string Name => "postgres";

    /// <inheritdoc/>
    public override IDataContext CreateContext(string connectionString, DataContextBuilder builder)
        => new PostgresDataContext(connectionString, builder);

    /// <inheritdoc/>
    public override DbConnection CreateConnection(string connectionString) => new NpgsqlConnection(connectionString);

    /// <inheritdoc/>
    public override IReadOnlyList<string> Schema { get; } =
    [
        "drop table if exists aot_order",
        "drop table if exists aot_customer",
        "create table aot_customer (id integer not null primary key, name varchar(64) not null, country varchar(2) not null)",
        "create table aot_order (id integer not null primary key, customer_id integer not null, amount numeric(10,2) not null, order_date timestamp not null)",
        "drop table if exists aot_write",
        "create table aot_write (id integer not null primary key, name varchar(64) not null, age integer not null)",
        "drop table if exists aot_json",
        "create table aot_json (id integer not null primary key, payload text null)",
        "drop table if exists aot_child",
        "drop table if exists aot_parent",
        "create table aot_parent (id integer not null primary key, name varchar(64) not null)",
        "create table aot_child (id integer not null primary key, parent_id integer not null, name varchar(64) not null)",
        "drop table if exists aot_converter",
        "create table aot_converter (id integer not null primary key, amount varchar(32) not null)",
        JsonSeed,
        .. RelationshipSeed,
        .. SeedRows,
    ];
}

/// <summary>The SQL Server provider.</summary>
internal sealed class SqlServerProbe : ProbeProvider
{
    /// <inheritdoc/>
    public override string Name => "sqlserver";

    /// <inheritdoc/>
    public override IDataContext CreateContext(string connectionString, DataContextBuilder builder)
        => new SqlServerDataContext(connectionString, builder);

    /// <inheritdoc/>
    public override DbConnection CreateConnection(string connectionString) => new SqlConnection(connectionString);

    /// <inheritdoc/>
    public override IReadOnlyList<string> Schema { get; } =
    [
        "if object_id('aot_order') is not null drop table aot_order",
        "if object_id('aot_customer') is not null drop table aot_customer",
        "create table aot_customer (id int not null primary key, name nvarchar(64) not null, country nvarchar(2) not null)",
        "create table aot_order (id int not null primary key, customer_id int not null, amount decimal(10,2) not null, order_date datetime2 not null)",
        "if object_id('aot_write') is not null drop table aot_write",
        "create table aot_write (id int not null primary key, name nvarchar(64) not null, age int not null)",
        "if object_id('aot_json') is not null drop table aot_json",
        "create table aot_json (id int not null primary key, payload nvarchar(max) null)",
        "if object_id('aot_child') is not null drop table aot_child",
        "if object_id('aot_parent') is not null drop table aot_parent",
        "create table aot_parent (id int not null primary key, name nvarchar(64) not null)",
        "create table aot_child (id int not null primary key, parent_id int not null, name nvarchar(64) not null)",
        "if object_id('aot_converter') is not null drop table aot_converter",
        "create table aot_converter (id int not null primary key, amount nvarchar(32) not null)",
        JsonSeed,
        .. RelationshipSeed,
        .. SeedRows,
    ];
}

/// <summary>
/// The ClickHouse provider. Its engine has no mutable primary key and no identity columns, so the provider
/// declares the id explicitly; deletes and updates are synchronous `ALTER TABLE … DELETE/UPDATE` mutations.
/// </summary>
internal sealed class ClickHouseProbe : ProbeProvider
{
    /// <inheritdoc/>
    public override string Name => "clickhouse";

    /// <inheritdoc/>
    public override IDataContext CreateContext(string connectionString, DataContextBuilder builder)
        => new ClickHouseDataContext(connectionString, builder);

    /// <inheritdoc/>
    public override DbConnection CreateConnection(string connectionString) => new ClickHouseConnection(connectionString);

    /// <inheritdoc/>
    public override IReadOnlyList<string> Schema { get; } =
    [
        "drop table if exists aot_order",
        "drop table if exists aot_customer",
        "create table aot_customer (id Int32, name String, country String) engine = MergeTree order by id",
        "create table aot_order (id Int32, customer_id Int32, amount Decimal(10,2), order_date DateTime64(6)) engine = MergeTree order by id",
        "drop table if exists aot_write",
        "create table aot_write (id Int32, name String, age Int32) engine = MergeTree order by id",
        "drop table if exists aot_json",
        "create table aot_json (id Int32, payload Nullable(String)) engine = MergeTree order by id",
        "drop table if exists aot_child",
        "drop table if exists aot_parent",
        "create table aot_parent (id Int32, name String) engine = MergeTree order by id",
        "create table aot_child (id Int32, parent_id Int32, name String) engine = MergeTree order by id",
        "drop table if exists aot_converter",
        "create table aot_converter (id Int32, amount String) engine = MergeTree order by id",
        JsonSeed,
        .. RelationshipSeed,
        .. SeedRows,
    ];
}
