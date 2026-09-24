using System.Text;

namespace NextORM.Core;

/// <summary>
/// One segment of a parsed CLR composite format string
/// (<c>string.Format</c>/<c>$"...{x:fmt}"</c>). A literal segment has <see cref="Literal"/> set and
/// <see cref="ArgumentIndex"/> <c>-1</c>; a placeholder segment has <see cref="ArgumentIndex"/> set and
/// an optional <see cref="Alignment"/>/<see cref="Specifier"/>.
/// </summary>
internal readonly record struct FormatPart(string? Literal, int ArgumentIndex, int Alignment, string? Specifier);

/// <summary>
/// Parses the subset of a CLR composite format string used by <c>string.Format</c> into literal
/// and placeholder segments. It understands <c>{{</c>/<c>}}</c> escaping, an argument index, an optional
/// alignment (<c>{0,-10}</c>) and an optional format specifier (<c>{0:N2}</c>). Malformed input is
/// rejected with <see cref="NotSupportedException"/> so a query never silently renders a mangled
/// format.
/// </summary>
internal static class CompositeFormat
{
    internal static IReadOnlyList<FormatPart> Parse(string format)
    {
        var parts = new List<FormatPart>();
        var literal = new StringBuilder();
        var i = 0;

        while (i < format.Length)
        {
            var c = format[i];

            if (c == '{')
            {
                if (i + 1 < format.Length && format[i + 1] == '{')
                {
                    literal.Append('{');
                    i += 2;
                    continue;
                }

                FlushLiteral(parts, literal);
                i = ParsePlaceholder(format, i, parts);
                continue;
            }

            if (c == '}')
            {
                if (i + 1 < format.Length && format[i + 1] == '}')
                {
                    literal.Append('}');
                    i += 2;
                    continue;
                }

                throw new NotSupportedException("A '}' in a format string must be escaped as '}}'.");
            }

            literal.Append(c);
            i++;
        }

        FlushLiteral(parts, literal);
        return parts;
    }

    private static void FlushLiteral(List<FormatPart> parts, StringBuilder literal)
    {
        if (literal.Length == 0)
            return;

        parts.Add(new FormatPart(literal.ToString(), -1, 0, null));
        literal.Clear();
    }

    private static int ParsePlaceholder(string format, int start, List<FormatPart> parts)
    {
        var i = start + 1;

        var indexStart = i;
        while (i < format.Length && char.IsAsciiDigit(format[i])) i++;

        if (i == indexStart)
            throw new NotSupportedException("A format string placeholder must start with an argument index.");

        var index = int.Parse(format.AsSpan(indexStart, i - indexStart));

        var alignment = 0;
        if (i < format.Length && format[i] == ',')
        {
            i++;
            while (i < format.Length && format[i] == ' ') i++;

            var negative = i < format.Length && format[i] == '-';
            if (negative) i++;

            var alignStart = i;
            while (i < format.Length && char.IsAsciiDigit(format[i])) i++;

            if (i == alignStart)
                throw new NotSupportedException("A format string alignment must be an integer.");

            alignment = int.Parse(format.AsSpan(alignStart, i - alignStart));
            if (negative) alignment = -alignment;
        }

        string? specifier = null;
        if (i < format.Length && format[i] == ':')
        {
            i++;
            var specStart = i;
            while (i < format.Length && format[i] != '}') i++;
            specifier = format.Substring(specStart, i - specStart);
        }

        if (i >= format.Length || format[i] != '}')
            throw new NotSupportedException("A format string placeholder is missing its closing '}'.");

        parts.Add(new FormatPart(null, index, alignment, specifier));
        return i + 1;
    }
}
