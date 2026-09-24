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
    public FromOptions Sample(double ratio) => Sample(ratio, 0);

    /// <summary>
    /// Reads roughly <paramref name="ratio"/> of the primary table starting at <paramref name="offset"/>
    /// with the ClickHouse <c>SAMPLE ratio OFFSET offset</c> modifier (both in <c>[0, 1]</c>). Requires a
    /// dialect that supports it (see <see cref="ISqlDialect.SupportsSample"/>).
    /// </summary>
    /// <param name="ratio">The fraction of rows to read; must be in <c>[0, 1]</c>.</param>
    /// <param name="offset">The fraction of rows to skip before sampling; must be in <c>[0, 1]</c>.</param>
    /// <returns>This instance, to allow chaining.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="ratio"/> or <paramref name="offset"/> is not finite or is outside <c>[0, 1]</c>.</exception>
    public FromOptions Sample(double ratio, double offset)
    {
        if (!double.IsFinite(ratio) || ratio is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(ratio), ratio, "Sample ratio must be a finite value in [0, 1].");

        if (!double.IsFinite(offset) || offset is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(offset), offset, "Sample offset must be a finite value in [0, 1].");

        SampleRatio = ratio;
        SampleOffset = offset;

        return this;
    }
}
