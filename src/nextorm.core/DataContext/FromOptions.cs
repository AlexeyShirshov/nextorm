namespace NextORM.Core;

/// <summary>
/// Per-query options for the primary <c>FROM</c> source, configured at query start through
/// <see cref="DataContextExtensions.From{T}(IDataContext, Action{FromOptions}, Action{EntityMetadataBuilder{T}}?)"/>
/// (or the raw-table <c>From(string, Action&lt;FromOptions&gt;)</c> overload). The values are copied into
/// the returned builder, so the options object is not retained.
/// </summary>
public sealed class FromOptions
{
    internal TableSampleClause? TableSampleClause { get; private set; }
    internal double? SampleRatio { get; private set; }
    internal double SampleOffset { get; private set; }
    internal IReadOnlyList<string>? TableHints { get; private set; }
    internal IReadOnlyList<string>? IndexHints { get; private set; }
    internal IndexHintKind IndexHintKind { get; private set; }
    internal string? SubQueryHint { get; private set; }

    /// <summary>
    /// Reads only <paramref name="percent"/> percent of the primary table with a <c>TABLESAMPLE</c>
    /// modifier; <paramref name="seed"/> makes the sample repeatable. Requires a dialect that supports it
    /// (see <see cref="ISqlDialect.TableSample"/>).
    /// </summary>
    /// <param name="percent">The percentage of the table to sample; must be in <c>(0, 100]</c>.</param>
    /// <param name="method">The sampling algorithm.</param>
    /// <param name="seed">An optional seed that makes the sample repeatable.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="percent"/> is not finite or is outside <c>(0, 100]</c>.</exception>
    public FromOptions TableSample(double percent, TableSampleMethod method = TableSampleMethod.System, double? seed = null)
    {
        if (!double.IsFinite(percent) || percent <= 0 || percent > 100)
            throw new ArgumentOutOfRangeException(nameof(percent), percent, "TABLESAMPLE percent must be in (0, 100].");

        TableSampleClause = new TableSampleClause(method, percent, seed);

        return this;
    }

    /// <summary>
    /// Reads roughly <paramref name="ratio"/> of the primary table with the ClickHouse <c>SAMPLE</c>
    /// modifier (a value in <c>[0, 1]</c>). Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsSample"/>).
    /// </summary>
    /// <param name="ratio">The fraction of rows to read; must be in <c>[0, 1]</c>.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ratio"/> is not finite or is outside <c>[0, 1]</c>.</exception>
    internal FromOptions Sample(double ratio) => Sample(ratio, 0);

    /// <summary>
    /// Reads roughly <paramref name="ratio"/> of the primary table starting at <paramref name="offset"/>
    /// with the ClickHouse <c>SAMPLE ratio OFFSET offset</c> modifier (both in <c>[0, 1]</c>). Requires a
    /// dialect that supports it (see <see cref="ISqlDialect.SupportsSample"/>).
    /// </summary>
    /// <param name="ratio">The fraction of rows to read; must be in <c>[0, 1]</c>.</param>
    /// <param name="offset">The fraction of rows to skip before sampling; must be in <c>[0, 1]</c>.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ratio"/> or <paramref name="offset"/> is not finite or is outside <c>[0, 1]</c>.</exception>
    internal FromOptions Sample(double ratio, double offset)
    {
        if (!double.IsFinite(ratio) || ratio is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Sample ratio must be a finite value in [0, 1].");

        if (!double.IsFinite(offset) || offset is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Sample offset must be a finite value in [0, 1].");

        SampleRatio = ratio;
        SampleOffset = offset;

        return this;
    }

    /// <summary>
    /// Attaches table-level hints to the primary physical table, for example
    /// <c>ctx.From&lt;IComplexEntity&gt;(o =&gt; o.WithTableHint("nolock"))</c> which renders
    /// <c>from complex_entity with (nolock)</c>. Requires a dialect that supports table hints (see
    /// <see cref="ISqlDialect.SupportsTableHints"/>); the hints are rendered verbatim, so only use
    /// trusted values. Null/blank entries are ignored: a call that supplies no usable hint leaves the
    /// options unchanged.
    /// </summary>
    /// <param name="hints">The table hints to apply to the primary source (for example <c>nolock</c>).</param>
    /// <returns>This instance, to allow chaining.</returns>
    public FromOptions WithTableHint(params string[] hints)
    {
        var names = hints is { Length: > 0 }
            ? hints.Where(h => !string.IsNullOrWhiteSpace(h)).ToArray()
            : [];

        if (names.Length > 0)
            TableHints = names;

        return this;
    }

    /// <summary>
    /// Attaches an index hint to the primary physical table, asking the planner to consider
    /// <paramref name="indexes"/> (<c>USE INDEX</c> on MySQL/MariaDB, <c>INDEXED BY</c> on SQLite,
    /// <c>WITH (INDEX(...))</c> on SQL Server). Requires a dialect that supports index hints (see
    /// <see cref="ISqlDialect.IndexHints"/>); a dialect without a native form rejects the command with
    /// <see cref="NotSupportedException"/>. The names are emitted verbatim, so only use trusted values.
    /// Use the overload taking an <see cref="IndexHintKind"/> to force or ignore the indexes.
    /// </summary>
    /// <param name="indexes">The index names to hint; never empty.</param>
    /// <returns>This instance, to allow chaining.</returns>
    public FromOptions WithIndex(params string[] indexes)
        => WithIndex(IndexHintKind.Use, indexes);

    /// <summary>
    /// Attaches an index hint with an explicit <paramref name="kind"/> to the primary physical table.
    /// With <see cref="IndexHintKind.Ignore"/> and no names, SQLite renders <c>NOT INDEXED</c>. Requires
    /// a dialect that supports index hints (see <see cref="ISqlDialect.IndexHints"/>). Null/blank names
    /// are ignored: a call with no usable name is a no-op unless <paramref name="kind"/> is
    /// <see cref="IndexHintKind.Ignore"/>.
    /// </summary>
    /// <param name="kind">Whether to use, force or ignore the indexes.</param>
    /// <param name="indexes">The index names to hint; may be empty for <see cref="IndexHintKind.Ignore"/>.</param>
    /// <returns>This instance, to allow chaining.</returns>
    public FromOptions WithIndex(IndexHintKind kind, params string[] indexes)
    {
        var names = indexes is { Length: > 0 }
            ? indexes.Where(i => !string.IsNullOrWhiteSpace(i)).ToArray()
            : [];

        if (names.Length > 0)
        {
            IndexHints = names;
            IndexHintKind = kind;
        }
        else if (kind == IndexHintKind.Ignore)
        {
            IndexHints = [];
            IndexHintKind = kind;
        }

        return this;
    }

    /// <summary>
    /// Tells the SQLite planner not to use any index (<c>NOT INDEXED</c>), or asks other dialects to
    /// ignore every named index. Requires a dialect that supports index hints (see
    /// <see cref="ISqlDialect.IndexHints"/>).
    /// </summary>
    /// <returns>This instance, to allow chaining.</returns>
    public FromOptions WithoutIndex()
        => WithIndex(IndexHintKind.Ignore);

    /// <summary>
    /// Attaches a provider-specific hint to a derived-table source (for example a PostgreSQL
    /// <c>pg_hint_plan</c> or MySQL optimizer hint), for example
    /// <c>ctx.From(subquery, o =&gt; o.WithSubQueryHint("NestLoop(t1)"))</c>. Only a
    /// <c>From(...)</c> overload whose source is an explicit subquery can carry it; the SQL Server
    /// dialect rejects it because a query hint cannot be appended to a subselect.
    /// </summary>
    /// <param name="hint">The subquery hint text; must be non-empty.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentException"><paramref name="hint"/> is null, empty or whitespace.</exception>
    public FromOptions WithSubQueryHint(string hint)
    {
        if (string.IsNullOrWhiteSpace(hint))
            throw new ArgumentException("A subquery hint must be a non-empty string.", nameof(hint));

        SubQueryHint = hint;
        return this;
    }
}
