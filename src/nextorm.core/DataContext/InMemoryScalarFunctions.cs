using System.Text;

namespace NextORM.Core;

/// <summary>
/// The CLR implementations the in-memory provider substitutes for the cross-provider scalar functions
/// of <see cref="CommonFunctions"/>. They reproduce the SQL semantics documented on the members
/// (character-wise, truncating pads, null-propagating arithmetic) so an in-memory query and the SQL
/// providers agree.
/// </summary>
internal static class InMemoryScalarFunctions
{
    /// <summary><c>left(value, n)</c>: the first <paramref name="n"/> characters, or all but the last |n|.</summary>
    internal static string? Left(string? value, int n)
    {
        if (value is null)
            return null;

        if (n >= 0)
            return value.Length <= n ? value : value[..n];

        var keep = value.Length + n;
        return keep <= 0 ? string.Empty : value[..keep];
    }

    /// <summary><c>right(value, n)</c>: the last <paramref name="n"/> characters, or all but the first |n|.</summary>
    internal static string? Right(string? value, int n)
    {
        if (value is null)
            return null;

        if (n >= 0)
            return value.Length <= n ? value : value[^n..];

        var keep = value.Length + n;
        return keep <= 0 ? string.Empty : value[^keep..];
    }

    /// <summary><c>lpad(value, length, pad)</c>.</summary>
    internal static string? Lpad(string? value, int length, string? pad) => Pad(value, length, pad, left: true);

    /// <summary><c>rpad(value, length, pad)</c>.</summary>
    internal static string? Rpad(string? value, int length, string? pad) => Pad(value, length, pad, left: false);

    /// <summary><c>repeat(value, count)</c>.</summary>
    internal static string? Repeat(string? value, int count)
    {
        if (value is null)
            return null;

        if (count <= 0 || value.Length == 0)
            return string.Empty;

        var builder = new StringBuilder(value.Length * count);
        for (var i = 0; i < count; i++)
            builder.Append(value);

        return builder.ToString();
    }

    /// <summary><c>reverse(value)</c>.</summary>
    internal static string? Reverse(string? value)
    {
        if (value is null)
            return null;

        var chars = value.ToCharArray();
        Array.Reverse(chars);
        return new string(chars);
    }

    /// <summary><c>space(count)</c>.</summary>
    internal static string Space(int count) => count <= 0 ? string.Empty : new string(' ', count);

    /// <summary><c>concat_ws(separator, ...)</c>, skipping null arguments.</summary>
    internal static string ConcatWs(string? separator, object?[] values)
    {
        var builder = new StringBuilder();
        var first = true;

        foreach (var value in values)
        {
            if (value is null)
                continue;

            if (!first)
                builder.Append(separator);

            builder.Append(value);
            first = false;
        }

        return builder.ToString();
    }

    /// <summary><c>translate(value, from, to)</c>.</summary>
    internal static string? Translate(string? value, string? from, string? to)
    {
        if (value is null || from is null || to is null)
            return null;

        var builder = new StringBuilder(value.Length);

        foreach (var ch in value)
        {
            var index = from.IndexOf(ch);
            if (index < 0)
                builder.Append(ch);
            else if (index < to.Length)
                builder.Append(to[index]);
        }

        return builder.ToString();
    }

    /// <summary><c>ascii(value)</c>: the code point of the first character (0 for an empty string).</summary>
    internal static int? Ascii(string? value) => value is null ? null : value.Length == 0 ? 0 : value[0];

    /// <summary><c>char(code)</c>: the string with the given code point.</summary>
    internal static string Char(int code) => char.ConvertFromUtf32(code);

    /// <summary><c>hex(X)</c>: the upper-case hexadecimal rendering of the value as bytes.</summary>
    internal static string? Hex(object? value)
    {
        var bytes = value switch
        {
            null => null,
            byte[] blob => blob,
            string text => Encoding.UTF8.GetBytes(text),
            _ => Encoding.UTF8.GetBytes(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
        };

        return bytes is null ? null : Convert.ToHexString(bytes);
    }

    /// <summary><c>unhex(X)</c>: the BLOB decoded from the hexadecimal string.</summary>
    internal static byte[]? Unhex(string? value) => value is null ? null : Convert.FromHexString(value);

    /// <summary><c>octet_length(X)</c>: the number of bytes in the encoded value.</summary>
    internal static int? OctetLength(object? value) => value switch
    {
        null => null,
        byte[] blob => blob.Length,
        string text => Encoding.UTF8.GetByteCount(text),
        _ => Encoding.UTF8.GetByteCount(Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty)
    };

    /// <summary><c>bit_length(X)</c>: the number of bits in the encoded value (8 × the octet length).</summary>
    internal static int? BitLength(string? value) => value is null ? null : Encoding.UTF8.GetByteCount(value) * 8;

    /// <summary><c>cot(X)</c>: the cotangent of <paramref name="x"/>.</summary>
    internal static double? Cot(double? x) => x is null ? null : 1.0 / Math.Tan(x.Value);

    /// <summary><c>unicode(X)</c>: the code point of the first character (null for null or empty).</summary>
    internal static int? Unicode(string? value) => string.IsNullOrEmpty(value) ? null : char.ConvertToUtf32(value, 0);

    /// <summary><c>char(X1, ..., XN)</c>: the string built from the given code points.</summary>
    internal static string CharFromCodes(int[] codes)
    {
        var builder = new StringBuilder(codes.Length);
        foreach (var code in codes)
            builder.Append(char.ConvertFromUtf32(code));

        return builder.ToString();
    }

    /// <summary><c>typeof(X)</c>: the storage class name of the value.</summary>
    internal static string TypeOf(object? value) => value switch
    {
        null => "null",
        byte[] => "blob",
        string => "text",
        bool or byte or sbyte or short or ushort or int or uint or long or ulong => "integer",
        float or double or decimal => "real",
        _ => "text"
    };

    /// <summary>SQLite <c>acos(X)</c>.</summary>
    internal static double? Acos(double? x) => x is null ? null : Math.Acos(x.Value);

    /// <summary>SQLite <c>acosh(X)</c>.</summary>
    internal static double? Acosh(double? x) => x is null ? null : Math.Acosh(x.Value);

    /// <summary>SQLite <c>asin(X)</c>.</summary>
    internal static double? Asin(double? x) => x is null ? null : Math.Asin(x.Value);

    /// <summary>SQLite <c>asinh(X)</c>.</summary>
    internal static double? Asinh(double? x) => x is null ? null : Math.Asinh(x.Value);

    /// <summary>SQLite <c>atan(X)</c>.</summary>
    internal static double? Atan(double? x) => x is null ? null : Math.Atan(x.Value);

    /// <summary>SQLite <c>atan2(Y, X)</c>.</summary>
    internal static double? Atan2(double? y, double? x) => y is null || x is null ? null : Math.Atan2(y.Value, x.Value);

    /// <summary>SQLite <c>atanh(X)</c>.</summary>
    internal static double? Atanh(double? x) => x is null ? null : Math.Atanh(x.Value);

    /// <summary>SQLite <c>cosh(X)</c>.</summary>
    internal static double? Cosh(double? x) => x is null ? null : Math.Cosh(x.Value);

    /// <summary>SQLite <c>degrees(X)</c>.</summary>
    internal static double? Degrees(double? x) => x is null ? null : x.Value * (180.0 / Math.PI);

    /// <summary>SQLite <c>log10(X)</c>.</summary>
    internal static double? Log10(double? x) => x is null ? null : Math.Log10(x.Value);

    /// <summary>SQLite <c>log2(X)</c>.</summary>
    internal static double? Log2(double? x) => x is null ? null : Math.Log2(x.Value);

    /// <summary>SQLite <c>mod(X, Y)</c>.</summary>
    internal static double? Mod(double? x, double? y) => x is null || y is null ? null : x.Value % y.Value;

    /// <summary>SQLite <c>pi()</c>.</summary>
    internal static double? Pi() => Math.PI;

    /// <summary>SQLite <c>radians(X)</c>.</summary>
    internal static double? Radians(double? x) => x is null ? null : x.Value * (Math.PI / 180.0);

    /// <summary>SQLite <c>sinh(X)</c>.</summary>
    internal static double? Sinh(double? x) => x is null ? null : Math.Sinh(x.Value);

    /// <summary>SQLite <c>tanh(X)</c>.</summary>
    internal static double? Tanh(double? x) => x is null ? null : Math.Tanh(x.Value);

    /// <summary><c>isempty(range)</c>.</summary>
    internal static bool RangeIsEmpty<T>(Range<T> range) where T : struct, IComparable<T> => range.IsEmpty;

    /// <summary><c>lower(range)</c>: the lower bound, or null when unbounded.</summary>
    internal static T? RangeLower<T>(Range<T> range) where T : struct, IComparable<T>
        => range.LowerInfinite ? null : range.Lower;

    /// <summary><c>upper(range)</c>: the upper bound, or null when unbounded.</summary>
    internal static T? RangeUpper<T>(Range<T> range) where T : struct, IComparable<T>
        => range.UpperInfinite ? null : range.Upper;

    /// <summary><c>lower_inc(range)</c>.</summary>
    internal static bool RangeLowerInc<T>(Range<T> range) where T : struct, IComparable<T> => range.LowerInclusive;

    /// <summary><c>upper_inc(range)</c>.</summary>
    internal static bool RangeUpperInc<T>(Range<T> range) where T : struct, IComparable<T> => range.UpperInclusive;

    /// <summary><c>lower_inf(range)</c>.</summary>
    internal static bool RangeLowerInf<T>(Range<T> range) where T : struct, IComparable<T> => range.LowerInfinite;

    /// <summary><c>upper_inf(range)</c>.</summary>
    internal static bool RangeUpperInf<T>(Range<T> range) where T : struct, IComparable<T> => range.UpperInfinite;

    /// <summary><c>range @&gt; value</c>.</summary>
    internal static bool RangeContainsValue<T>(Range<T> range, T value) where T : struct, IComparable<T>
    {
        if (range.IsEmpty)
            return false;

        if (!range.LowerInfinite)
        {
            var lower = Comparer<T>.Default.Compare(value, range.Lower!.Value);
            if (lower < 0 || (lower == 0 && !range.LowerInclusive))
                return false;
        }

        if (!range.UpperInfinite)
        {
            var upper = Comparer<T>.Default.Compare(value, range.Upper!.Value);
            if (upper > 0 || (upper == 0 && !range.UpperInclusive))
                return false;
        }

        return true;
    }

    /// <summary><c>a &amp;&amp; b</c>: whether the ranges share at least one point.</summary>
    internal static bool RangeOverlaps<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty || b.IsEmpty)
            return false;

        if (!a.UpperInfinite && !b.LowerInfinite)
        {
            var upperLower = Comparer<T>.Default.Compare(a.Upper!.Value, b.Lower!.Value);
            if (upperLower < 0 || (upperLower == 0 && !(a.UpperInclusive && b.LowerInclusive)))
                return false;
        }

        if (!b.UpperInfinite && !a.LowerInfinite)
        {
            var upperLower = Comparer<T>.Default.Compare(b.Upper!.Value, a.Lower!.Value);
            if (upperLower < 0 || (upperLower == 0 && !(b.UpperInclusive && a.LowerInclusive)))
                return false;
        }

        return true;
    }

    /// <summary><c>outer @&gt; inner</c>: whether <paramref name="outer"/> contains every point of <paramref name="inner"/>.</summary>
    internal static bool RangeContainsRange<T>(Range<T> outer, Range<T> inner) where T : struct, IComparable<T>
    {
        if (inner.IsEmpty)
            return true;

        if (outer.IsEmpty)
            return false;

        if ((inner.LowerInfinite && !outer.LowerInfinite) || (inner.UpperInfinite && !outer.UpperInfinite))
            return false;

        if (!inner.LowerInfinite && !outer.LowerInfinite)
        {
            var lower = Comparer<T>.Default.Compare(outer.Lower!.Value, inner.Lower!.Value);
            if (lower > 0 || (lower == 0 && !outer.LowerInclusive && inner.LowerInclusive))
                return false;
        }

        if (!inner.UpperInfinite && !outer.UpperInfinite)
        {
            var upper = Comparer<T>.Default.Compare(outer.Upper!.Value, inner.Upper!.Value);
            if (upper < 0 || (upper == 0 && !outer.UpperInclusive && inner.UpperInclusive))
                return false;
        }

        return true;
    }

    /// <summary><c>inner &lt;@ outer</c>.</summary>
    internal static bool RangeContainedBy<T>(Range<T> inner, Range<T> outer) where T : struct, IComparable<T>
        => RangeContainsRange(outer, inner);

    /// <summary><c>a + b</c>: the union of two ranges, which must overlap or be adjacent.</summary>
    internal static Range<T> RangeUnion<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty)
            return b;

        if (b.IsEmpty)
            return a;

        if (!RangeOverlaps(a, b) && !RangeAdjacent(a, b))
            throw new NotSupportedException(
                "The PostgreSQL range union operator requires the ranges to overlap or be adjacent; the result of range union would not be a single range.");

        var lower = CompareLowerBound(a, b) <= 0 ? a : b;
        var upper = CompareUpperBound(a, b) >= 0 ? a : b;
        return CombineBounds(lower, upper);
    }

    /// <summary><c>a * b</c>: the intersection of two ranges, empty when they are disjoint.</summary>
    internal static Range<T> RangeIntersection<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty || b.IsEmpty)
            return Range<T>.Empty;

        var lower = CompareLowerBound(a, b) >= 0 ? a : b;
        var upper = CompareUpperBound(a, b) <= 0 ? a : b;

        if (!lower.LowerInfinite && !upper.UpperInfinite)
        {
            var cmp = Comparer<T>.Default.Compare(lower.Lower!.Value, upper.Upper!.Value);
            if (cmp > 0 || (cmp == 0 && !(lower.LowerInclusive && upper.UpperInclusive)))
                return Range<T>.Empty;
        }

        return CombineBounds(lower, upper);
    }

    /// <summary><c>a - b</c>: the difference of two ranges, which must stay a single range.</summary>
    internal static Range<T> RangeDifference<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty || b.IsEmpty)
            return a;

        if (RangeContainsRange(b, a))
            return Range<T>.Empty;

        if (!RangeOverlaps(a, b))
            return a;

        if (CompareLowerBound(a, b) >= 0)
        {
            return new Range<T>(
                b.Upper, a.Upper, !b.UpperInclusive, a.UpperInclusive,
                b.UpperInfinite, a.UpperInfinite, isEmpty: false);
        }

        if (CompareUpperBound(a, b) <= 0)
        {
            return new Range<T>(
                a.Lower, b.Lower, a.LowerInclusive, !b.LowerInclusive,
                a.LowerInfinite, b.LowerInfinite, isEmpty: false);
        }

        throw new NotSupportedException(
            "The PostgreSQL range difference operator requires the result to be a single range; the result of range difference would not be contiguous.");
    }

    /// <summary><c>a -|- b</c>: whether the ranges touch without overlapping.</summary>
    internal static bool RangeAdjacent<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty || b.IsEmpty)
            return false;

        return BoundsAdjacent(a, b) || BoundsAdjacent(b, a);
    }

    /// <summary><c>a &lt;&lt; b</c>: whether <paramref name="a"/> is strictly left of <paramref name="b"/>.</summary>
    internal static bool RangeStrictlyLeftOf<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty || b.IsEmpty || a.UpperInfinite || b.LowerInfinite)
            return false;

        var cmp = Comparer<T>.Default.Compare(a.Upper!.Value, b.Lower!.Value);
        return cmp < 0 || (cmp == 0 && !(a.UpperInclusive && b.LowerInclusive));
    }

    /// <summary><c>a &gt;&gt; b</c>: whether <paramref name="a"/> is strictly right of <paramref name="b"/>.</summary>
    internal static bool RangeStrictlyRightOf<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty || b.IsEmpty || a.LowerInfinite || b.UpperInfinite)
            return false;

        var cmp = Comparer<T>.Default.Compare(a.Lower!.Value, b.Upper!.Value);
        return cmp > 0 || (cmp == 0 && !(a.LowerInclusive && b.UpperInclusive));
    }

    /// <summary><c>a &amp;&lt; b</c>: whether <paramref name="a"/> does not extend to the right of <paramref name="b"/>.</summary>
    internal static bool RangeNotExtendRightOf<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
        => !a.IsEmpty && !b.IsEmpty && CompareUpperBound(a, b) <= 0;

    /// <summary><c>a &amp;&gt; b</c>: whether <paramref name="a"/> does not extend to the left of <paramref name="b"/>.</summary>
    internal static bool RangeNotExtendLeftOf<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
        => !a.IsEmpty && !b.IsEmpty && CompareLowerBound(a, b) >= 0;

    /// <summary>Builds a range from a lower-bound and an upper-bound source range.</summary>
    private static Range<T> CombineBounds<T>(Range<T> lower, Range<T> upper) where T : struct, IComparable<T>
        => new(
            lower.LowerInfinite ? null : lower.Lower,
            upper.UpperInfinite ? null : upper.Upper,
            lower.LowerInclusive,
            upper.UpperInclusive,
            lower.LowerInfinite,
            upper.UpperInfinite,
            isEmpty: false);

    /// <summary>Orders two lower bounds; an unbounded lower sorts first and an inclusive bound sorts before an exclusive one at the same value.</summary>
    private static int CompareLowerBound<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.LowerInfinite && b.LowerInfinite)
            return 0;

        if (a.LowerInfinite)
            return -1;

        if (b.LowerInfinite)
            return 1;

        var cmp = Comparer<T>.Default.Compare(a.Lower!.Value, b.Lower!.Value);
        if (cmp != 0)
            return cmp < 0 ? -1 : 1;

        if (a.LowerInclusive == b.LowerInclusive)
            return 0;

        return a.LowerInclusive ? -1 : 1;
    }

    /// <summary>Orders two upper bounds; an unbounded upper sorts last and an inclusive bound sorts after an exclusive one at the same value.</summary>
    private static int CompareUpperBound<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.UpperInfinite && b.UpperInfinite)
            return 0;

        if (a.UpperInfinite)
            return 1;

        if (b.UpperInfinite)
            return -1;

        var cmp = Comparer<T>.Default.Compare(a.Upper!.Value, b.Upper!.Value);
        if (cmp != 0)
            return cmp < 0 ? -1 : 1;

        if (a.UpperInclusive == b.UpperInclusive)
            return 0;

        return a.UpperInclusive ? 1 : -1;
    }

    /// <summary>Returns whether the upper bound of <paramref name="a"/> touches the lower bound of <paramref name="b"/>.</summary>
    private static bool BoundsAdjacent<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
        => !a.UpperInfinite && !b.LowerInfinite
            && Comparer<T>.Default.Compare(a.Upper!.Value, b.Lower!.Value) == 0
            && a.UpperInclusive != b.LowerInclusive;

    /// <summary>Builds a range from bounds and an optional PostgreSQL bounds string (<c>[)</c>, <c>[]</c>, <c>(]</c>, <c>()</c>).</summary>
    internal static Range<T> RangeCtor<T>(T? lower, T? upper, string? bounds) where T : struct, IComparable<T>
    {
        var lowerInclusive = true;
        var upperInclusive = false;
        if (bounds is { Length: 2 })
        {
            lowerInclusive = bounds[0] == '[';
            upperInclusive = bounds[1] == ']';
        }

        return new Range<T>(lower, upper, lowerInclusive, upperInclusive, lower is null, upper is null, isEmpty: false);
    }

    /// <summary>The PostgreSQL empty-range literal of the bound type.</summary>
    internal static Range<T> RangeEmpty<T>() where T : struct, IComparable<T> => Range<T>.Empty;

    /// <summary><c>multirange(range)</c>: a canonical multirange with just the given range.</summary>
    internal static Range<T>[] RangeMultiFromRange<T>(Range<T> range) where T : struct, IComparable<T>
        => Normalize([range]);

    /// <summary><c>range_merge(a, b)</c>: the smallest range that includes both ranges, without a contiguity requirement.</summary>
    internal static Range<T> RangeMerge<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        if (a.IsEmpty)
            return b;

        if (b.IsEmpty)
            return a;

        var lower = CompareLowerBound(a, b) <= 0 ? a : b;
        var upper = CompareUpperBound(a, b) >= 0 ? a : b;
        return CombineBounds(lower, upper);
    }

    /// <summary><c>range_merge(multirange)</c>: the smallest range that includes the whole multirange.</summary>
    internal static Range<T> RangeMergeMulti<T>(Range<T>[] multirange) where T : struct, IComparable<T>
    {
        var normalized = Normalize(multirange);
        return normalized.Length == 0 ? Range<T>.Empty : RangeMerge(normalized[0], normalized[^1]);
    }

    /// <summary><c>range_agg</c>: the canonical multirange of every range in the group.</summary>
    internal static Range<T>[] RangeAgg<T>(IEnumerable<Range<T>> ranges) where T : struct, IComparable<T>
    {
        var list = new List<Range<T>>();
        foreach (var range in ranges)
            list.Add(range);

        return Normalize(list);
    }

    /// <summary><c>range_intersect_agg</c>: the intersection of every range in the group.</summary>
    internal static Range<T> RangeIntersectAgg<T>(IEnumerable<Range<T>> ranges) where T : struct, IComparable<T>
    {
        var hasValue = false;
        var accumulator = Range<T>.Empty;
        foreach (var range in ranges)
        {
            accumulator = hasValue ? RangeIntersection(accumulator, range) : range;
            hasValue = true;
        }

        return hasValue ? accumulator : Range<T>.Empty;
    }

    /// <summary><c>multirange &amp;&amp; multirange</c>.</summary>
    internal static bool RangeMultiOverlaps<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        foreach (var x in a)
            foreach (var y in b)
                if (RangeOverlaps(x, y))
                    return true;

        return false;
    }

    /// <summary><c>multirange &amp;&amp; range</c>.</summary>
    internal static bool RangeMultiOverlapsRange<T>(Range<T>[] a, Range<T> b) where T : struct, IComparable<T>
    {
        foreach (var x in a)
            if (RangeOverlaps(x, b))
                return true;

        return false;
    }

    /// <summary><c>range &amp;&amp; multirange</c>.</summary>
    internal static bool RangeRangeOverlapsMulti<T>(Range<T> a, Range<T>[] b) where T : struct, IComparable<T>
        => RangeMultiOverlapsRange(b, a);

    /// <summary><c>multirange @&gt; multirange</c>.</summary>
    internal static bool RangeMultiContainsMulti<T>(Range<T>[] outer, Range<T>[] inner) where T : struct, IComparable<T>
    {
        foreach (var i in inner)
            if (!RangeMultiContainsRange(outer, i))
                return false;

        return true;
    }

    /// <summary><c>multirange @&gt; range</c>.</summary>
    internal static bool RangeMultiContainsRange<T>(Range<T>[] outer, Range<T> inner) where T : struct, IComparable<T>
    {
        foreach (var o in outer)
            if (RangeContainsRange(o, inner))
                return true;

        return false;
    }

    /// <summary><c>range @&gt; multirange</c>.</summary>
    internal static bool RangeRangeContainsMulti<T>(Range<T> outer, Range<T>[] inner) where T : struct, IComparable<T>
    {
        foreach (var i in inner)
            if (!RangeContainsRange(outer, i))
                return false;

        return true;
    }

    /// <summary><c>multirange @&gt; value</c>.</summary>
    internal static bool RangeMultiContainsValue<T>(Range<T>[] multirange, T value) where T : struct, IComparable<T>
    {
        foreach (var range in multirange)
            if (RangeContainsValue(range, value))
                return true;

        return false;
    }

    /// <summary><c>multirange &lt;@ multirange</c>.</summary>
    internal static bool RangeMultiContainedByMulti<T>(Range<T>[] inner, Range<T>[] outer) where T : struct, IComparable<T>
        => RangeMultiContainsMulti(outer, inner);

    /// <summary><c>multirange &lt;@ range</c>.</summary>
    internal static bool RangeMultiContainedByRange<T>(Range<T>[] inner, Range<T> outer) where T : struct, IComparable<T>
    {
        foreach (var i in inner)
            if (!RangeContainsRange(outer, i))
                return false;

        return true;
    }

    /// <summary><c>range &lt;@ multirange</c>.</summary>
    internal static bool RangeContainedByMulti<T>(Range<T> inner, Range<T>[] outer) where T : struct, IComparable<T>
        => RangeMultiContainsRange(outer, inner);

    /// <summary><c>a + b</c>: the multirange union (the operands need not overlap or be adjacent).</summary>
    internal static Range<T>[] RangeMultiUnion<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        var list = new List<Range<T>>(a.Length + b.Length);
        list.AddRange(a);
        list.AddRange(b);
        return Normalize(list);
    }

    /// <summary><c>a * b</c>: the multirange intersection.</summary>
    internal static Range<T>[] RangeMultiIntersection<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        var list = new List<Range<T>>();
        foreach (var x in a)
            foreach (var y in b)
            {
                var intersection = RangeIntersection(x, y);
                if (!intersection.IsEmpty)
                    list.Add(intersection);
            }

        return Normalize(list);
    }

    /// <summary><c>a - b</c>: the multirange difference.</summary>
    internal static Range<T>[] RangeMultiDifference<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        var result = new List<Range<T>>();
        foreach (var x in a)
        {
            var pieces = new List<Range<T>> { x };
            foreach (var y in b)
            {
                var next = new List<Range<T>>();
                foreach (var piece in pieces)
                    next.AddRange(RangeDifferenceParts(piece, y));

                pieces = next;
                if (pieces.Count == 0)
                    break;
            }

            result.AddRange(pieces);
        }

        return Normalize(result);
    }

    /// <summary><c>a &lt;&lt; b</c>: true when the whole first multirange is strictly left of the second.</summary>
    internal static bool RangeMultiStrictlyLeftOf<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return na.Length > 0 && nb.Length > 0 && RangeStrictlyLeftOf(na[^1], nb[0]);
    }

    /// <summary><c>a &gt;&gt; b</c>: true when the whole first multirange is strictly right of the second.</summary>
    internal static bool RangeMultiStrictlyRightOf<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return na.Length > 0 && nb.Length > 0 && RangeStrictlyRightOf(na[0], nb[^1]);
    }

    /// <summary><c>a &amp;&lt; b</c>: true when the first multirange does not extend to the right of the second.</summary>
    internal static bool RangeMultiNotExtendRightOf<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return na.Length > 0 && nb.Length > 0 && RangeNotExtendRightOf(na[^1], nb[^1]);
    }

    /// <summary><c>a &amp;&gt; b</c>: true when the first multirange does not extend to the left of the second.</summary>
    internal static bool RangeMultiNotExtendLeftOf<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return na.Length > 0 && nb.Length > 0 && RangeNotExtendLeftOf(na[0], nb[0]);
    }

    /// <summary><c>a -|- b</c>: true when the multiranges touch without overlapping.</summary>
    internal static bool RangeMultiAdjacent<T>(Range<T>[] a, Range<T>[] b) where T : struct, IComparable<T>
    {
        if (a.Length == 0 || b.Length == 0)
            return false;

        var adjacent = false;
        foreach (var x in a)
            foreach (var y in b)
            {
                if (RangeOverlaps(x, y))
                    return false;

                if (RangeAdjacent(x, y))
                    adjacent = true;
            }

        return adjacent;
    }

    /// <summary><c>isempty(multirange)</c>.</summary>
    internal static bool RangeMultiIsEmpty<T>(Range<T>[] multirange) where T : struct, IComparable<T> => multirange.Length == 0;

    /// <summary><c>lower(multirange)</c>.</summary>
    internal static T? RangeMultiLower<T>(Range<T>[] multirange) where T : struct, IComparable<T>
        => TryGetFirstRange(multirange, out var first) ? RangeLower(first) : null;

    /// <summary><c>upper(multirange)</c>.</summary>
    internal static T? RangeMultiUpper<T>(Range<T>[] multirange) where T : struct, IComparable<T>
        => TryGetLastRange(multirange, out var last) ? RangeUpper(last) : null;

    /// <summary><c>lower_inc(multirange)</c>.</summary>
    internal static bool RangeMultiLowerInc<T>(Range<T>[] multirange) where T : struct, IComparable<T>
        => TryGetFirstRange(multirange, out var first) && RangeLowerInc(first);

    /// <summary><c>upper_inc(multirange)</c>.</summary>
    internal static bool RangeMultiUpperInc<T>(Range<T>[] multirange) where T : struct, IComparable<T>
        => TryGetLastRange(multirange, out var last) && RangeUpperInc(last);

    /// <summary><c>lower_inf(multirange)</c>.</summary>
    internal static bool RangeMultiLowerInf<T>(Range<T>[] multirange) where T : struct, IComparable<T>
        => TryGetFirstRange(multirange, out var first) && RangeLowerInf(first);

    /// <summary><c>upper_inf(multirange)</c>.</summary>
    internal static bool RangeMultiUpperInf<T>(Range<T>[] multirange) where T : struct, IComparable<T>
        => TryGetLastRange(multirange, out var last) && RangeUpperInf(last);

    /// <summary>Finds the non-empty range with the smallest lower bound (the first of the canonical form).</summary>
    private static bool TryGetFirstRange<T>(Range<T>[] multirange, out Range<T> first) where T : struct, IComparable<T>
    {
        first = default;
        var found = false;
        foreach (var range in multirange)
        {
            if (range.IsEmpty)
                continue;

            if (!found || CompareLowerBound(range, first) < 0)
            {
                first = range;
                found = true;
            }
        }

        return found;
    }

    /// <summary>Finds the non-empty range with the largest upper bound (the last of the canonical form).</summary>
    private static bool TryGetLastRange<T>(Range<T>[] multirange, out Range<T> last) where T : struct, IComparable<T>
    {
        last = default;
        var found = false;
        foreach (var range in multirange)
        {
            if (range.IsEmpty)
                continue;

            if (!found || CompareUpperBound(range, last) > 0)
            {
                last = range;
                found = true;
            }
        }

        return found;
    }

    /// <summary>Canonicalizes a multirange: drops empty ranges, sorts by the lower bound and merges overlapping or adjacent ranges.</summary>
    private static Range<T>[] Normalize<T>(IReadOnlyList<Range<T>> ranges) where T : struct, IComparable<T>
    {
        var sorted = new List<Range<T>>(ranges.Count);
        foreach (var range in ranges)
            if (!range.IsEmpty)
                sorted.Add(range);

        sorted.Sort(static (x, y) => CompareLowerBound(x, y));

        var merged = new List<Range<T>>(sorted.Count);
        foreach (var range in sorted)
        {
            if (merged.Count == 0)
            {
                merged.Add(range);
                continue;
            }

            var last = merged[^1];
            if (RangeOverlaps(last, range) || RangeAdjacent(last, range))
                merged[^1] = RangeUnion(last, range);
            else
                merged.Add(range);
        }

        return [.. merged];
    }

    /// <summary>Splits <c>a - b</c> into zero, one or two ranges (the multi-range subtraction can split a range).</summary>
    private static List<Range<T>> RangeDifferenceParts<T>(Range<T> a, Range<T> b) where T : struct, IComparable<T>
    {
        var parts = new List<Range<T>>(2);

        if (a.IsEmpty)
            return parts;

        if (b.IsEmpty)
        {
            parts.Add(a);
            return parts;
        }

        if (RangeContainsRange(b, a))
            return parts;

        if (!RangeOverlaps(a, b))
        {
            parts.Add(a);
            return parts;
        }

        if (CompareLowerBound(a, b) >= 0)
        {
            parts.Add(new Range<T>(b.Upper, a.Upper, !b.UpperInclusive, a.UpperInclusive, b.UpperInfinite, a.UpperInfinite, false));
            return parts;
        }

        if (CompareUpperBound(a, b) <= 0)
        {
            parts.Add(new Range<T>(a.Lower, b.Lower, a.LowerInclusive, !b.LowerInclusive, a.LowerInfinite, b.LowerInfinite, false));
            return parts;
        }

        parts.Add(new Range<T>(a.Lower, b.Lower, a.LowerInclusive, !b.LowerInclusive, a.LowerInfinite, b.LowerInfinite, false));
        parts.Add(new Range<T>(b.Upper, a.Upper, !b.UpperInclusive, a.UpperInclusive, b.UpperInfinite, a.UpperInfinite, false));
        return parts;
    }

    private static string? Pad(string? value, int length, string? pad, bool left)
    {
        if (value is null || pad is null)
            return null;

        if (length <= 0)
            return string.Empty;

        if (value.Length >= length)
            return value[..length];

        if (pad.Length == 0)
            return value;

        var needed = length - value.Length;
        var builder = new StringBuilder(needed);
        while (builder.Length < needed)
            builder.Append(pad);

        var padding = builder.ToString(0, needed);
        return left ? padding + value : value + padding;
    }
}
