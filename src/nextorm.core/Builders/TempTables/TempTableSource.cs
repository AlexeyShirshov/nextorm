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

    QueryCommand ITempTableSource.Source => Source;
}

/// <summary>
/// Identifies a lazy temporary-table materialisation carried by a
/// <see cref="FromExpression"/>. The non-generic view lets the engine collect the sources that a
/// query reads and render one materialisation step per source.
/// </summary>
internal interface ITempTableSource
{
    /// <summary>The query whose rows fill the table.</summary>
    QueryCommand Source { get; }

    /// <summary>The auto-generated target table name.</summary>
    string Name { get; }

    /// <summary>The materialisation options.</summary>
    CreateTableOptions Options { get; }
}
