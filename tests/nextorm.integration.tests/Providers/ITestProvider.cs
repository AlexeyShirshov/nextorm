using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Describes a database provider the integration suite can run against. Each provider owns the
/// schema and seed data, so the shared tests can run unchanged on every supported database.
/// </summary>
public interface ITestProvider
{
    string Name { get; }

    bool IsAvailable { get; }

    string SkipReason { get; }

    /// <summary>
    /// True when the provider implements <c>FULL JOIN</c>. MySQL/MariaDB have a right join but no
    /// full join, so the shared full-join test is skipped there.
    /// </summary>
    bool SupportsFullJoin { get; }

    /// <summary>
    /// True when AVG over an integer column keeps the fractional result. PostgreSQL and SQLite
    /// return a fractional value, while a SQL Server integer AVG is itself an integer, so the
    /// shared assertion does not apply there.
    /// </summary>
    bool SupportsFractionalAverage { get; }

    /// <summary>
    /// True when the provider implements <c>INTERSECT ALL</c> / <c>EXCEPT ALL</c>. PostgreSQL does;
    /// SQL Server has neither and SQLite has no <c>* ALL</c> variant of either operation.
    /// </summary>
    bool SupportsIntersectExceptAll { get; }

    /// <summary>
    /// True when the provider implements the sample and population variance aggregates
    /// (<c>var</c> / <c>varp</c>). PostgreSQL and SQL Server have them built in; SQLite provides
    /// them through the custom aggregates registered in <c>SQLiteFunctions</c>.
    /// </summary>
    bool SupportsVarianceAggregates { get; }

    /// <summary>
    /// True when the provider can execute the portable table-valued function used by the shared TVF
    /// tests. Only SQLite's bundled <c>json_each</c> is used, so providers without it skip.
    /// </summary>
    bool SupportsTableValuedFunctions { get; }

    /// <summary>Reason reported when <see cref="SupportsTableValuedFunctions"/> is false.</summary>
    string TableValuedFunctionSkipReason { get; }

    /// <summary>
    /// True when a scalar subquery that returns several rows raises an error. PostgreSQL, SQL Server,
    /// MySQL, MariaDB and ClickHouse do; SQLite silently returns the first row, so Single/SingleOrDefault
    /// inside a scalar subquery is rejected there.
    /// </summary>
    bool EnforcesScalarSubqueryCardinality { get; }

    /// <summary>
    /// True when the provider can render a correlated <c>CROSS/OUTER APPLY</c> source (or its
    /// <c>LATERAL</c> equivalent). SQL Server, PostgreSQL, MySQL and MariaDB can; SQLite and
    /// ClickHouse have no lateral source, so the shared correlated-apply tests are skipped there.
    /// </summary>
    bool SupportsApply { get; }

    /// <summary>
    /// True when the provider can return the written rows from an <c>INSERT</c> through its
    /// <c>RETURNING</c>/<c>OUTPUT</c> form. PostgreSQL, SQLite and SQL Server can; MySQL/MariaDB,
    /// ClickHouse and the in-memory provider cannot, so the shared returning tests are skipped there.
    /// </summary>
    bool SupportsInsertReturning { get; }

    /// <summary>
    /// True when the provider can skip conflicting rows with an <c>INSERT OR IGNORE</c>/<c>INSERT IGNORE</c>
    /// head or an <c>ON CONFLICT DO NOTHING</c> suffix. PostgreSQL, SQLite, MySQL, MariaDB and
    /// ClickHouse (where it is a no-op) do; SQL Server has no such form, so the shared ignore test is
    /// skipped there.
    /// </summary>
    bool SupportsIgnoreDuplicates { get; }

    /// <summary>
    /// True when the provider has a native <c>TRUNCATE TABLE</c>. SQL Server, PostgreSQL, MySQL and
    /// MariaDB do; SQLite has no <c>TRUNCATE</c>, so the shared truncate test is skipped there.
    /// </summary>
    bool SupportsTruncate { get; }

    /// <summary>
    /// True when the provider has a native multi-table <c>DELETE</c> (delete from a table based on a
    /// join). PostgreSQL, SQL Server, MySQL and MariaDB do; SQLite and ClickHouse have no join delete,
    /// so the shared delete-join test is skipped there.
    /// </summary>
    bool SupportsDeleteJoin { get; }

    /// <summary>
    /// True when the provider can materialise a query into a persistent table (<c>ToTable</c>).
    /// PostgreSQL, SQLite, MySQL, MariaDB, SQL Server and ClickHouse can.
    /// </summary>
    bool SupportsCreateTableAsSelect { get; }

    /// <summary>
    /// True when the provider can materialise a query into a <em>temporary</em> table
    /// (<c>ToTempTable</c>). PostgreSQL, SQLite, MySQL and MariaDB can; SQL Server has no
    /// <c>CREATE TEMPORARY TABLE ... AS SELECT</c> (a session-scoped table is <c>ToTable("#name")</c>) and
    /// ClickHouse cannot express a temporary <c>AS SELECT</c>, so the shared temp test is skipped there
    /// and the rejection test runs instead.
    /// </summary>
    bool SupportsTemporaryCreateTableAsSelect { get; }

    /// <summary>
    /// True when the provider can execute a multi-statement batch (see <c>BatchExtensions.Batch</c>) in
    /// one round trip on one session. PostgreSQL, SQLite, MySQL, MariaDB and SQL Server can; ClickHouse
    /// has no single-round-trip multi-statement guarantee, so the shared batch tests are skipped there.
    /// </summary>
    bool SupportsBatch { get; }

    /// <summary>
    /// True when the provider can execute a stored procedure through
    /// <c>ExecuteProcedure</c> (<c>CommandType.StoredProcedure</c>). SQL Server, PostgreSQL and
    /// MySQL/MariaDB can; SQLite and ClickHouse have no stored procedures, so the shared
    /// unsupported-provider test runs instead of the procedure tests.
    /// </summary>
    bool SupportsStoredProcedures { get; }

    /// <summary>
    /// True when the provider can bind a table-valued parameter
    /// (<c>ProcedureParameter.Table&lt;T&gt;(name, ...)</c>). SQL Server uses a native user-defined table
    /// type; PostgreSQL, MySQL/MariaDB and SQLite emulate it with an array/JSON document; ClickHouse does
    /// not support it.
    /// </summary>
    bool SupportsTableValuedParameters { get; }

    /// <summary>
    /// True when the provider supports ADO.NET transactions on its connection. SQLite, PostgreSQL,
    /// SQL Server and MySQL/MariaDB do; ClickHouse speaks HTTP and has no transaction, so the shared
    /// transaction tests are skipped there.
    /// </summary>
    bool SupportsTransactions { get; }

    /// <summary>
    /// True when the provider can translate a constant-pattern <c>Regex.IsMatch</c>/<c>Regex.Replace</c>
    /// into native SQL. PostgreSQL, MySQL/MariaDB, ClickHouse, SQLite and SQL Server 2025+ can.
    /// </summary>
    bool SupportsRegex { get; }

    /// <summary>
    /// True when the provider implements the streaming LOB terminals (<c>ToStream</c>/<c>ToTextReader</c>).
    /// PostgreSQL, SQL Server and SQLite do; MySQL/MariaDB, ClickHouse and the in-memory provider
    /// reject them with <see cref="NotSupportedException"/>.
    /// </summary>
    bool SupportsLobStreaming { get; }

    /// <summary>
    /// True when the provider implements the multi-column <c>ToDataReader</c>/<c>ToDataReaderAsync</c>
    /// terminal. PostgreSQL and SQL Server do through sequential access; SQLite does through the
    /// locator-free buffered result path (no <c>rowid</c> is appended to the projection). MySQL/MariaDB,
    /// ClickHouse and the in-memory provider have no sequential-access support and fail closed.
    /// </summary>
    bool SupportsLobDataReader => false;

    /// <summary>
    /// True when the provider accepts a zero-column result set (a <c>select</c> with an empty target
    /// list, as PostgreSQL allows). Only such a provider can exercise the <c>FieldCount != 1</c>
    /// guard of the LOB terminals with zero columns; the shared boundary test skips elsewhere.
    /// </summary>
    bool SupportsZeroColumnResult => false;

    /// <summary>
    /// True when the provider accepts the whole-entity read that materialises a
    /// <see cref="DynamicColumnsAttribute"/> store. The read appends the store's star after the mapped
    /// columns; a dialect that requires a qualified star (MySQL/MariaDB) qualifies it with the source
    /// alias (<c>select id, name, `t1`.* from dynamic_entity as `t1`</c>), so every provider that can
    /// address the FROM alias supports it. Genuine per-feature gaps still skip their own shared tests:
    /// for example <c>DynamicColumns_FullMerge</c> is skipped where a general multi-branch <c>MERGE</c>
    /// is unavailable.
    /// </summary>
    bool SupportsDynamicColumnsRead => true;

    /// <summary>Reason reported when <see cref="SupportsDynamicColumnsRead"/> is false.</summary>
    string DynamicColumnsReadSkipReason =>
        "This provider cannot read a dynamic-columns store.";

    void EnsureSeeded();

    IDataContext CreateContext();

    /// <summary>
    /// Creates a context that routes its diagnostics through <paramref name="loggerFactory"/>. Providers
    /// that can honour the factory override this; the default ignores it and returns the ordinary
    /// context, so a diagnostic-observation test must skip when the provider does not override.
    /// </summary>
    IDataContext CreateContext(Microsoft.Extensions.Logging.ILoggerFactory? loggerFactory) => CreateContext();
}
