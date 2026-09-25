using System.Globalization;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the culture-invariant subset of CLR string formatting into SQL:
/// <c>string.Format(fmt, ...)</c> (which the compiler also produces for <c>$"...{x:fmt}"</c>) and
/// <c>x.ToString(fmt)</c> for numeric and date/time values. The CLR format specifier is mapped to the
/// provider's native formatting function through <see cref="ISqlDialect.StringFormats"/>; a specifier
/// the provider cannot render exactly, a non-constant format string and any culture other than
/// <see cref="CultureInfo.InvariantCulture"/> are rejected with <see cref="NotSupportedException"/>
/// rather than silently producing different SQL.
/// </summary>
internal static class StringFormatTranslator
{
    /// <summary>Translates a static <c>string.Format</c> call.</summary>
    internal static bool TryTranslateFormat(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        var args = node.Arguments;
        var offset = 0;

        if (args.Count > 0 && typeof(IFormatProvider).IsAssignableFrom(args[0].Type))
        {
            RequireInvariantProvider(args[0]);
            offset = 1;
        }

        if (args.Count <= offset)
            return false;

        if (!SqlLiteral.TryGetConstantString(args[offset], out var format))
            throw new NotSupportedException("string.Format requires a constant format string.");

        var values = CollectArguments(args, offset + 1);
        Emit(visitor, format, values);
        return true;
    }

    /// <summary>Translates <c>x.ToString(fmt)</c> for a numeric or date/time <c>x</c>.</summary>
    internal static bool TryTranslateToString(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Object is null || node.Arguments.Count != 1)
            return false;

        var target = Nullable.GetUnderlyingType(node.Object.Type) ?? node.Object.Type;
        if (!IsNumeric(target) && target != typeof(DateTime) && target != typeof(DateTimeOffset))
            return false;

        if (!SqlLiteral.TryGetConstantString(node.Arguments[0], out var format))
            throw new NotSupportedException($"{node.Object.Type.Name}.ToString requires a constant format string.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Object);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(RenderValue(visitor, node.Object, format));
        return true;
    }

    private static void Emit(BaseExpressionVisitor visitor, string format, IReadOnlyList<Expression> values)
    {
        var parts = CompositeFormat.Parse(format);

        if (visitor.IsParamMode)
        {
            // Walk the referenced values in the order they are rendered so the parameter order of the
            // projection pass matches the SQL pass, even when the format reorders or repeats them.
            for (var (i, cnt) = (0, parts.Count); i < cnt; i++)
            {
                var part = parts[i];
                if (part.Literal is null)
                    visitor.Visit(values[ResolveIndex(part, values.Count)]);
            }

            return;
        }

        visitor.NeedAliasForColumn = true;

        var pieces = new List<string>(parts.Count);
        for (var (i, cnt) = (0, parts.Count); i < cnt; i++)
        {
            var part = parts[i];
            if (part.Literal is not null)
            {
                pieces.Add(SqlLiteral.ToSqlStringLiteral(part.Literal));
                continue;
            }

            var value = values[ResolveIndex(part, values.Count)];
            var rendered = RenderValue(visitor, Unwrap(value), part.Specifier);

            if (part.Alignment != 0)
                rendered = ApplyAlignment(visitor, rendered, part.Alignment);

            pieces.Add(rendered);
        }

        if (pieces.Count == 0)
            visitor.Builder!.Append(visitor.Dialect.EmptyString);
        else if (pieces.Count == 1)
            visitor.Builder!.Append(pieces[0]);
        else
            visitor.Builder!.Append('(').Append(visitor.Dialect.MakeConcat(pieces)).Append(')');
    }

    private static int ResolveIndex(in FormatPart part, int count)
    {
        if (part.ArgumentIndex < 0 || part.ArgumentIndex >= count)
            throw new NotSupportedException($"string.Format references argument {part.ArgumentIndex}, which is out of range.");

        return part.ArgumentIndex;
    }

    /// <summary>
    /// Renders one formatted value. With no specifier the value is emitted as-is; with a numeric or
    /// date/time specifier the provider's native formatting function is used.
    /// </summary>
    private static string RenderValue(BaseExpressionVisitor visitor, Expression value, string? specifier)
    {
        if (string.IsNullOrEmpty(specifier))
            return visitor.VisitToString(value);

        var target = Nullable.GetUnderlyingType(value.Type) ?? value.Type;

        if (IsNumeric(target))
        {
            var formats = RequireFormats(visitor);
            var (specifierLetter, precision) = ParseNumberSpecifier(specifier);

            if (!formats.SupportsNumber(specifierLetter))
                throw new NotSupportedException($"The numeric format specifier '{specifierLetter}' is not supported by this provider.");

            return formats.RenderNumber(visitor.VisitToString(value), specifierLetter, precision);
        }

        if (target == typeof(DateTime) || target == typeof(DateTimeOffset))
        {
            var formats = RequireFormats(visitor);

            if (!formats.SupportsDateFormat(specifier))
                throw new NotSupportedException($"The date/time format string '{specifier}' is not supported by this provider; only the tokens yyyy yy MM dd HH mm ss with the separators - / . : and space are portable.");

            return formats.RenderDate(visitor.VisitToString(value), specifier);
        }

        throw new NotSupportedException($"A format specifier is not supported for type {target.Name}.");
    }

    private static string ApplyAlignment(BaseExpressionVisitor visitor, string rendered, int alignment)
    {
        var width = Math.Abs(alignment);
        return visitor.Dialect.MakePad(rendered, width.ToString(CultureInfo.InvariantCulture), SqlLiteral.ToSqlStringLiteral(" "), left: alignment > 0);
    }

    private static IStringFormatFunctions RequireFormats(BaseExpressionVisitor visitor) =>
        visitor.Dialect.StringFormats
            ?? throw new NotSupportedException("CLR format specifiers (string.Format/x.ToString(format)) are not supported by this provider.");

    private static (char Specifier, int Precision) ParseNumberSpecifier(string specifier)
    {
        var letter = char.ToUpperInvariant(specifier[0]);
        if (letter is not ('N' or 'F' or 'D' or 'X'))
            throw new NotSupportedException($"The numeric format specifier '{specifier}' is not supported; only N, F, D and X are portable.");

        if (specifier.Length == 1)
        {
            // CLR defaults: N/F use two fractional digits, D/X use the natural digit count.
            var defaultPrecision = letter is 'N' or 'F' ? 2 : -1;
            return (letter, defaultPrecision);
        }

        for (var i = 1; i < specifier.Length; i++)
        {
            if (!char.IsAsciiDigit(specifier[i]))
                throw new NotSupportedException($"The numeric format specifier '{specifier}' is not supported.");
        }

        return (letter, int.Parse(specifier.AsSpan(1)));
    }

    private static void RequireInvariantProvider(Expression provider)
    {
        provider = Unwrap(provider);

        if (provider is ConstantExpression { Value: null })
            return;

        if (provider is ConstantExpression { Value: CultureInfo culture } && ReferenceEquals(culture, CultureInfo.InvariantCulture))
            return;

        throw new NotSupportedException("string.Format is only supported with CultureInfo.InvariantCulture; the current/other cultures have no portable SQL form.");
    }

    private static IReadOnlyList<Expression> CollectArguments(IReadOnlyList<Expression> args, int start)
    {
        // A params object[] call is wrapped in a single NewArrayExpression by the compiler; the fixed
        // overloads pass their arguments individually.
        if (args.Count - start == 1 && args[start] is NewArrayExpression { Expressions: var items })
            return items;

        var values = new List<Expression>(args.Count - start);
        for (var i = start; i < args.Count; i++)
            values.Add(args[i]);

        return values;
    }

    private static Expression Unwrap(Expression expression) =>
        expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? unary.Operand
            : expression;

    private static bool IsNumeric(Type type) =>
        type == typeof(byte) || type == typeof(sbyte)
        || type == typeof(short) || type == typeof(ushort)
        || type == typeof(int) || type == typeof(uint)
        || type == typeof(long) || type == typeof(ulong)
        || type == typeof(float) || type == typeof(double)
        || type == typeof(decimal);
}
