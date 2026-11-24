namespace NextORM.Core;

/// <summary>
/// A deferred, automatically named temporary view. A query that reads it through
/// <see cref="TempViewExtensions.From{TResult}(IDataContext, TempViewSource{TResult})"/>
/// creates and reads the view in one batch without materialising its rows.
/// </summary>
/// <typeparam name="TResult">The defining query's projected row type.</typeparam>
public sealed class TempViewSource<TResult> : ITemporarySource
{
    internal TempViewSource(QueryCommand<TResult> source)
    {
        Source = source;
        Name = "__nextorm_view_" + Guid.NewGuid().ToString("N");
    }

    /// <summary>The generated, unquoted temporary-view name.</summary>
    public string Name { get; }

    internal QueryCommand<TResult> Source { get; }

    QueryCommand ITemporarySource.Source => Source;
}
