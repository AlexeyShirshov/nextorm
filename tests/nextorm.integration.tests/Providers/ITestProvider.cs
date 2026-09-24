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
    /// True when the provider supports ADO.NET transactions on its connection. SQLite, PostgreSQL,
    /// SQL Server and MySQL/MariaDB do; ClickHouse speaks HTTP and has no transaction, so the shared
    /// transaction tests are skipped there.
    /// </summary>
    bool SupportsTransactions { get; }

    void EnsureSeeded();

    IDataContext CreateContext();
}
