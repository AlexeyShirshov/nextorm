namespace NextORM.Core;

/// <summary>
/// ClickHouse native <c>InsertBinaryAsync</c> options. A group with every value left
/// <see langword="null"/> requests nothing; supplying it on another provider is rejected when the target
/// provider is known.
/// </summary>
public sealed record ClickHouseBulkInsertOptions
{
    /// <summary>
    /// The maximum degree of parallelism the native insert requests, or <see langword="null"/> to preserve
    /// the configured default. Values above one combined with an active session are contradictory and are
    /// rejected by the native path.
    /// </summary>
    public int? MaxDegreeOfParallelism { get; init; }

    /// <summary>
    /// Whether the native insert uses the configured session: <see langword="null"/> preserves it,
    /// <see langword="true"/> requires an actual session id, and <see langword="false"/> explicitly requests
    /// a sessionless non-temporary insert. Defaults to <see langword="null"/>.
    /// </summary>
    public bool? UseSession { get; init; }

    /// <summary>Whether at least one value was explicitly supplied; an empty group creates no request.</summary>
    internal bool RequestsAny => MaxDegreeOfParallelism is not null || UseSession is not null;
}
