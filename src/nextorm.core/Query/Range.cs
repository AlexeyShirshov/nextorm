using System.Collections.Generic;
using System.Globalization;

namespace NextORM.Core;

/// <summary>
/// A provider-agnostic range value: a contiguous interval of <typeparamref name="T"/> whose bounds may
/// be inclusive or exclusive and may be unbounded on either side, plus the empty range. It mirrors the
/// invariants of the PostgreSQL range types without taking a dependency on a driver; the PostgreSQL
/// provider maps it to <c>int4range</c>/<c>int8range</c>/<c>numrange</c>/<c>tsrange</c>/
/// <c>tstzrange</c>/<c>daterange</c>.
/// </summary>
/// <remarks>
/// <see cref="Lower"/> and <see cref="Upper"/> are the declared bounds and are only meaningful when the
/// matching <see cref="LowerInfinite"/>/<see cref="UpperInfinite"/> flag is <see langword="false"/>;
/// an unbounded side, the empty range and a <c>default(Range&lt;T&gt;)</c> value all have
/// <see langword="null"/> bounds. The explicit flags (rather than a nullable <typeparamref name="T"/>
/// alone) keep an unbounded side distinguishable from a real bound that happens to be the default value.
/// PostgreSQL reports both <c>lower_inf</c> and <c>upper_inf</c> as <see langword="false"/> for the
/// <c>'empty'</c> literal, which <see cref="LowerInfinite"/> and <see cref="UpperInfinite"/> mirror.
/// </remarks>
/// <typeparam name="T">
/// The bound type: <see cref="int"/>, <see cref="long"/>, <see cref="decimal"/>,
/// <see cref="System.DateTime"/>, <see cref="System.DateTimeOffset"/> or <see cref="System.DateOnly"/>.
/// </typeparam>
public readonly struct Range<T> : IEquatable<Range<T>>
    where T : struct, IComparable<T>
{
    /// <summary>
    /// Creates a range with the PostgreSQL default bounds <c>[lower, upper)</c> (lower inclusive, upper
    /// exclusive).
    /// </summary>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    public Range(T lower, T upper)
        : this(lower, upper, lowerInclusive: true, upperInclusive: false)
    {
    }

    /// <summary>Creates a range from explicit bounds and bound inclusivity.</summary>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    /// <param name="lowerInclusive">Whether the lower bound is part of the range.</param>
    /// <param name="upperInclusive">Whether the upper bound is part of the range.</param>
    public Range(T lower, T upper, bool lowerInclusive, bool upperInclusive)
        : this(lower, upper, lowerInclusive, upperInclusive, lowerInfinite: false, upperInfinite: false)
    {
    }

    /// <summary>Creates a range from explicit bounds, bound inclusivity and unbounded sides.</summary>
    /// <param name="lower">The lower bound; ignored when <paramref name="lowerInfinite"/> is true.</param>
    /// <param name="upper">The upper bound; ignored when <paramref name="upperInfinite"/> is true.</param>
    /// <param name="lowerInclusive">Whether the lower bound is part of the range.</param>
    /// <param name="upperInclusive">Whether the upper bound is part of the range.</param>
    /// <param name="lowerInfinite">Whether the lower side is unbounded.</param>
    /// <param name="upperInfinite">Whether the upper side is unbounded.</param>
    public Range(T lower, T upper, bool lowerInclusive, bool upperInclusive, bool lowerInfinite, bool upperInfinite)
        : this(lower, upper, lowerInclusive, upperInclusive, lowerInfinite, upperInfinite, isEmpty: false)
    {
    }

    /// <summary>
    /// Creates a range from the full set of PostgreSQL range characteristics. Used by providers to
    /// materialize a value read from the database; prefer the shorter constructors when authoring one.
    /// </summary>
    /// <param name="lower">The lower bound, or <see langword="null"/> when the lower side is unbounded.</param>
    /// <param name="upper">The upper bound, or <see langword="null"/> when the upper side is unbounded.</param>
    /// <param name="lowerInclusive">Whether the lower bound is part of the range.</param>
    /// <param name="upperInclusive">Whether the upper bound is part of the range.</param>
    /// <param name="lowerInfinite">Whether the lower side is unbounded.</param>
    /// <param name="upperInfinite">Whether the upper side is unbounded.</param>
    /// <param name="isEmpty">Whether this is the empty range; its bounds are then ignored.</param>
    public Range(T? lower, T? upper, bool lowerInclusive, bool upperInclusive, bool lowerInfinite, bool upperInfinite, bool isEmpty)
    {
        if (isEmpty)
        {
            Lower = null;
            Upper = null;
            LowerInclusive = false;
            UpperInclusive = false;
            _lowerInfinite = true;
            _upperInfinite = true;
            _isEmpty = true;
            return;
        }

        _lowerInfinite = lowerInfinite || lower is null;
        _upperInfinite = upperInfinite || upper is null;
        Lower = _lowerInfinite ? null : lower;
        Upper = _upperInfinite ? null : upper;
        LowerInclusive = _lowerInfinite ? false : lowerInclusive;
        UpperInclusive = _upperInfinite ? false : upperInclusive;

        _isEmpty = !_lowerInfinite && !_upperInfinite
            && (Comparer<T>.Default.Compare(Lower.GetValueOrDefault(), Upper.GetValueOrDefault()) > 0
                || (Comparer<T>.Default.Compare(Lower.GetValueOrDefault(), Upper.GetValueOrDefault()) == 0 && !(LowerInclusive && UpperInclusive)));
    }

    private readonly bool _lowerInfinite;
    private readonly bool _upperInfinite;
    private readonly bool _isEmpty;

    /// <summary>The lower bound, or <see langword="null"/> when there is no finite lower bound (unbounded or empty).</summary>
    public T? Lower { get; }

    /// <summary>The upper bound, or <see langword="null"/> when there is no finite upper bound (unbounded or empty).</summary>
    public T? Upper { get; }

    /// <summary>Whether the lower bound is part of the range. Always <see langword="false"/> when unbounded or empty.</summary>
    public bool LowerInclusive { get; }

    /// <summary>Whether the upper bound is part of the range. Always <see langword="false"/> when unbounded or empty.</summary>
    public bool UpperInclusive { get; }

    /// <summary>Whether the lower side is unbounded (PostgreSQL <c>lower_inf</c>). Always <see langword="false"/> for the empty range.</summary>
    public bool LowerInfinite => !IsEmpty && (_lowerInfinite || IsUninitialized);

    /// <summary>Whether the upper side is unbounded (PostgreSQL <c>upper_inf</c>). Always <see langword="false"/> for the empty range.</summary>
    public bool UpperInfinite => !IsEmpty && (_upperInfinite || IsUninitialized);

    /// <summary>Whether this is the empty range (PostgreSQL <c>isempty</c>, the <c>'empty'</c> literal).</summary>
    public bool IsEmpty => _isEmpty || IsUninitialized;

    private bool IsUninitialized => Lower is null && Upper is null && !_lowerInfinite && !_upperInfinite;

    /// <summary>The empty range: it contains no point and overlaps nothing.</summary>
    public static Range<T> Empty { get; } = new(null, null, lowerInclusive: false, upperInclusive: false, lowerInfinite: true, upperInfinite: true, isEmpty: true);

    /// <summary>Returns whether this range and <paramref name="other"/> have the same PostgreSQL characteristics.</summary>
    /// <param name="other">The range to compare with.</param>
    /// <returns><see langword="true"/> when the two ranges are equal.</returns>
    public bool Equals(Range<T> other)
    {
        if (IsEmpty || other.IsEmpty)
            return IsEmpty == other.IsEmpty;

        return LowerInfinite == other.LowerInfinite
            && UpperInfinite == other.UpperInfinite
            && (LowerInfinite || Nullable.Equals(Lower, other.Lower))
            && (UpperInfinite || Nullable.Equals(Upper, other.Upper))
            && LowerInclusive == other.LowerInclusive
            && UpperInclusive == other.UpperInclusive;
    }

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is Range<T> other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        if (IsEmpty)
            return 17;

        var hash = new HashCode();
        hash.Add(LowerInfinite);
        hash.Add(UpperInfinite);
        if (!LowerInfinite)
            hash.Add(Lower);
        if (!UpperInfinite)
            hash.Add(Upper);
        hash.Add(LowerInclusive);
        hash.Add(UpperInclusive);
        return hash.ToHashCode();
    }

    /// <summary>Returns whether two ranges have the same PostgreSQL characteristics.</summary>
    /// <param name="left">The left range.</param>
    /// <param name="right">The right range.</param>
    /// <returns><see langword="true"/> when the two ranges are equal.</returns>
    public static bool operator ==(Range<T> left, Range<T> right) => left.Equals(right);

    /// <summary>Returns whether two ranges differ.</summary>
    /// <param name="left">The left range.</param>
    /// <param name="right">The right range.</param>
    /// <returns><see langword="true"/> when the two ranges are not equal.</returns>
    public static bool operator !=(Range<T> left, Range<T> right) => !left.Equals(right);

    /// <inheritdoc/>
    public override string ToString()
    {
        if (IsEmpty)
            return "empty";

        var lower = LowerInfinite
            ? "("
            : (LowerInclusive ? "[" : "(") + string.Create(CultureInfo.InvariantCulture, $"{Lower.GetValueOrDefault()}");
        var upper = UpperInfinite
            ? ")"
            : string.Create(CultureInfo.InvariantCulture, $"{Upper.GetValueOrDefault()}") + (UpperInclusive ? "]" : ")");
        return lower + "," + upper;
    }
}
