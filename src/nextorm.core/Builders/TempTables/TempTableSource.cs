namespace NextORM.Core;

/// <summary>
/// A lazy temporary-table materialisation: the query whose rows fill the table plus the statement
/// options. Created by <see cref="TempTableExtensions.AsTempTable{TResult}(QueryCommand{TResult}, CreateTableOptions?)"/>;
/// nothing is executed until a query reads the source through
/// <see cref="DataContextExtensions.From{TResult}(IDataContext, TempTableSource{TResult})"/>.
/// <para>
/// Reading it runs one batch — <c>DROP TABLE IF EXISTS</c>, <c>CREATE TEMPORARY TABLE ... AS SELECT</c>
/// and the read — on a single session, so the table is always freshly materialised and never depends on
/// connection pinning (safe under a connection-level pooler such as PgBouncer).
/// </para>
/// <para>
/// The batch runs on the host the read is routed to, and the source query is rendered under its own
/// mapping variant: scope the source like the read, so the materialisation and the read address the same
/// bucket. A source that resolves to another bucket than the read is rejected.
/// </para>
/// </summary>
/// <typeparam name="TResult">The source query's projected row type. The rows are read back through
/// <see cref="TableAlias"/> column accessors, not through this type's mapping.</typeparam>
public sealed class TempTableSource<TResult> : ITempTableSource
{
    internal TempTableSource(QueryCommand<TResult> source, string name, CreateTableOptions options)
    {
        Source = source;
        Name = name;
        Options = options;
    }

    /// <summary>The auto-generated raw (unquoted, unconventioned) temporary-table name.</summary>
    public string Name { get; }

    /// <summary>The options of the <c>CREATE TEMPORARY TABLE ... AS SELECT</c> statement.</summary>
    public CreateTableOptions Options { get; }

    internal QueryCommand<TResult> Source { get; }

    QueryCommand ITemporarySource.Source => Source;
}

/// <summary>
/// Identifies a deferred temporary object carried by a <see cref="FromExpression"/>.
/// </summary>
internal interface ITemporarySource
{
    /// <summary>The query that defines the temporary object.</summary>
    QueryCommand Source { get; }

    /// <summary>The auto-generated target name.</summary>
    string Name { get; }
}

internal interface ITempTableSource : ITemporarySource
{
    /// <summary>The materialisation options.</summary>
    CreateTableOptions Options { get; }
}
