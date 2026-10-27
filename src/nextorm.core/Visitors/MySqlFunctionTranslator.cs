using System.Globalization;
using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the MySQL/MariaDB-only functions of <see cref="MySqlFunctions"/> through the dialect's
/// <see cref="IMySqlFunctions"/> renderer. The family is recognized by the declaring type, so the
/// same-named PostgreSQL members (<c>md5</c>, <c>format</c>) still fall through to
/// <see cref="ExtendedScalarFunctionTranslator"/>. A provider that does not expose the family, or that
/// does not support a specific name, fails with a clear message instead of emitting invalid SQL.
/// </summary>
internal static class MySqlFunctionTranslator
{
    private static readonly HashSet<string> VariadicNames = new(StringComparer.Ordinal)
    {
        nameof(MySqlFunctions.field), nameof(MySqlFunctions.elt), nameof(MySqlFunctions.make_set)
    };

    /// <summary>
    /// The MariaDB sequence functions whose first argument is an object identifier: the dialect's
    /// <c>SequenceName</c> unquotes the plain token into the sequence name, so the value policy's
    /// charset-introduced hex spelling must never reach it.
    /// </summary>
    private static readonly HashSet<string> SequenceNameFunctions = new(StringComparer.Ordinal)
    {
        nameof(MySqlFunctions.next_value_for), nameof(MySqlFunctions.nextval),
        nameof(MySqlFunctions.setval), nameof(MySqlFunctions.lastval)
    };

    /// <summary>Translates a MySQL/MariaDB-only call; returns <c>false</c> when it is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType is not { } declaring
            || !typeof(MySqlFunctions).IsAssignableFrom(declaring))
            return false;

        var name = node.Method.Name;

        if (visitor.Dialect.MySqlFunctions is not { } mySql || !mySql.Supports(name))
            throw new NotSupportedException($"The {name} function is not supported by this provider.");

        if (name == nameof(MySqlFunctions.get_format))
        {
            // GET_FORMAT(DATE, 'EUR') is grammar: the first argument is a bare keyword and the second a
            // plain quoted literal, so neither may go through the generic value renderer.
            var (kind, format) = ReadGetFormat(visitor, node.Arguments);

            if (visitor.IsParamMode)
            {
                for (var (i, cnt) = (0, node.Arguments.Count); i < cnt; i++)
                    visitor.Visit(node.Arguments[i]);

                return true;
            }

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(mySql.Render(name, [kind, format]));
            return true;
        }

        var args = VariadicNames.Contains(name)
            ? ArgumentFlattener.Flatten(node.Arguments, 1)
            : (IReadOnlyList<Expression>)node.Arguments;

        if (VariadicNames.Contains(name) && args.Count < 2)
            throw new NotSupportedException($"The {name} function requires at least one value argument.");

        var hasWeightStringModifiers =
            name == nameof(MySqlFunctions.weight_string) && args.Count == 3;
        (MySqlWeightStringCast Cast, int Length) weightStringModifiers = hasWeightStringModifiers
            ? ReadWeightStringModifiers(args[1], args[2])
            : default;

        if (name == nameof(MySqlFunctions.export_set) && args.Count == 5
            && TypeFacts.UnwrapConvert(args[4]) is ConstantExpression { Value: int bitCount }
            && bitCount is < 0 or > 64)
            throw new NotSupportedException(
                $"The export_set function requires a bit count between 0 and 64 (got {bitCount}).");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return true;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            // The weight_string modifiers are grammar tokens, not values: the cast keyword and the
            // constant length are emitted as SQL text so the enum never reaches the value policy.
            if (hasWeightStringModifiers && i == 1)
            {
                rendered[i] = weightStringModifiers.Cast == MySqlWeightStringCast.Char ? "CHAR" : "BINARY";
                continue;
            }

            if (hasWeightStringModifiers && i == 2)
            {
                rendered[i] = weightStringModifiers.Length.ToString(CultureInfo.InvariantCulture);
                continue;
            }

            // grammar token: the sequence name is an identifier the dialect unquotes, not a data
            // value, so it keeps the historic plain quoted token (the value policy emitted
            // `_utf8mb4 X'…'` for it and `next value for` received the hex literal).
            rendered[i] = i == 0 && SequenceNameFunctions.Contains(name)
                && TypeFacts.UnwrapConvert(args[i]) is ConstantExpression { Value: string sequence }
                    ? visitor.RenderStringLiteral(sequence, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword)
                    : visitor.VisitToString(args[i]);
        }

        visitor.Builder!.Append(mySql.Render(name, rendered));
        return true;
    }

    private const string GetFormatError =
        "MySQL GET_FORMAT() requires a compile-time IANA format kind and format literal.";

    /// <summary>
    /// Reads the two grammar constants of <c>get_format</c>: the family keyword is emitted bare and the
    /// platform format as a plain quoted literal. Both must be compile-time constants; anything else is
    /// refused before any SQL (or argument evaluation) is emitted.
    /// </summary>
    private static (string Kind, string Format) ReadGetFormat(
        BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (args.Count != 2
            || !TryReadGetFormatKind(args[0], out var kind)
            || TypeFacts.UnwrapConvert(args[1]) is not ConstantExpression { Value: string format }
            || !IsKnownGetFormat(format))
            throw new NotSupportedException(GetFormatError);

        var keyword = kind switch
        {
            MySqlFormatKind.Date => "DATE",
            MySqlFormatKind.Time => "TIME",
            _ => "DATETIME"
        };

        return (keyword, visitor.RenderStringLiteral(
            format, SqlStringLiteralRole.GrammarToken, SqlStringGrammar.Keyword));
    }

    private static bool TryReadGetFormatKind(Expression expression, out MySqlFormatKind kind)
    {
        switch (TypeFacts.UnwrapConvert(expression))
        {
            case ConstantExpression { Value: MySqlFormatKind value } when Enum.IsDefined(value):
                kind = value;
                return true;
            case ConstantExpression { Value: int raw } when Enum.IsDefined(typeof(MySqlFormatKind), raw):
                kind = (MySqlFormatKind)raw;
                return true;
            default:
                kind = default;
                return false;
        }
    }

    /// <summary>The platform format names MySQL 8.4 documents for every <c>GET_FORMAT</c> family.</summary>
    private static bool IsKnownGetFormat(string format) =>
        format is "USA" or "JIS" or "ISO" or "EUR" or "INTERNAL";

    private static (MySqlWeightStringCast Cast, int Length) ReadWeightStringModifiers(
        Expression castExpression, Expression lengthExpression)
    {
        if (!TryReadWeightStringCast(castExpression, out var cast))
            throw new NotSupportedException(
                "The weight_string function requires a constant cast " +
                "(MySqlWeightStringCast.Char or MySqlWeightStringCast.Binary).");

        if (TypeFacts.UnwrapConvert(lengthExpression) is not ConstantExpression { Value: int length })
            throw new NotSupportedException("The weight_string function requires a constant length.");

        if (length < 1)
            throw new NotSupportedException("The weight_string function requires a length of at least 1.");

        return (cast, length);
    }

    private static bool TryReadWeightStringCast(Expression expression, out MySqlWeightStringCast cast)
    {
        switch (TypeFacts.UnwrapConvert(expression))
        {
            case ConstantExpression { Value: MySqlWeightStringCast value }:
                cast = value;
                return true;
            case ConstantExpression { Value: int raw }
                when Enum.IsDefined(typeof(MySqlWeightStringCast), raw):
                cast = (MySqlWeightStringCast)raw;
                return true;
            default:
                cast = default;
                return false;
        }
    }
}
