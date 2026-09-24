namespace NextORM.Core;

/// <summary>
/// How a provider attaches a filter to an aggregate. The style is one concept — a predicate over the
/// rows an aggregate sees — rendered differently by each provider family. A dialect opts into filtering
/// by returning anything other than <see cref="None"/> from
/// <see cref="ISqlDialect.AggregateFilterStyle"/>.
/// </summary>
public enum AggregateFilterStyle
{
    /// <summary>
    /// The provider cannot filter an aggregate. Filtering overloads such as
    /// <c>SqlFunctions.Sql.count(Expression&lt;Func&lt;bool&gt;&gt;)</c> are rejected with
    /// <see cref="System.NotSupportedException"/>. This is the safe default (MySQL/MariaDB, SQL Server).
    /// </summary>
    None,

    /// <summary>
    /// The provider renders the standard ANSI <c>FILTER (WHERE predicate)</c> clause after the
    /// aggregate, for example <c>sum(id) filter (where id &gt; 10)</c> (PostgreSQL, SQLite).
    /// </summary>
    AnsiFilter,

    /// <summary>
    /// The provider renders the filter as the ClickHouse <c>-If</c> combinator, for example
    /// <c>sumIf(id, id &gt; 10)</c> or <c>countIf(id &gt; 10)</c> (ClickHouse). The combinator takes the
    /// aggregate's value arguments followed by the predicate; the predicate comes last.
    /// </summary>
    IfCombinator,
}
