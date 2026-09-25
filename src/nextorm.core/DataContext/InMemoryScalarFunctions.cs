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
