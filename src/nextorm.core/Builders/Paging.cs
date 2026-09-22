namespace NextORM.Core;

/// <summary>
/// Limit/offset pair describing a single page request.
/// </summary>
/// <remarks>
/// Was a struct with public mutable fields; the fields are now validated properties so the
/// invariants (non-negative limit/offset) hold. See <c>docs/specs/design/API-NAMING-REVIEW.md</c>
/// findings P1-17 and P2-19.
/// </remarks>
public struct Paging
{
    private int _limit;
    private int _offset;

    /// <summary>Maximum number of rows to return; zero means "no limit".</summary>
    public int Limit
    {
        readonly get => _limit;
        set => _limit = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Limit must be non-negative.");
    }

    /// <summary>Number of leading rows to skip; zero means "from the start".</summary>
    public int Offset
    {
        readonly get => _offset;
        set => _offset = value >= 0
            ? value
            : throw new ArgumentOutOfRangeException(nameof(value), value, "Offset must be non-negative.");
    }

    /// <summary>
    /// When <c>true</c>, the page request keeps every row tied with the last row by the <c>ORDER BY</c>
    /// (<c>FETCH ... WITH TIES</c> / <c>TOP(n) WITH TIES</c>). Requires a dialect that supports it (see
    /// <see cref="ISqlDialect.SupportsWithTies"/>) and a non-zero <see cref="Limit"/>.
    /// </summary>
    public bool HasWithTies { get; set; }

    /// <summary>
    /// <c>true</c> when neither a limit nor an offset is set, so no paging clause is emitted.
    /// </summary>
    public readonly bool IsEmpty => _offset <= 0 && _limit <= 0;
    /// <summary>
    /// <c>true</c> when only a positive limit is set (offset is zero), so the provider can emit a simple
    /// <c>TOP</c>/<c>LIMIT</c> without an <c>OFFSET</c>.
    /// </summary>
    public readonly bool IsTop => _offset <= 0 && _limit > 0;
}
