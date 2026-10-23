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
        nameof(MySqlFunctions.field), nameof(MySqlFunctions.elt)
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

        var args = VariadicNames.Contains(name)
            ? ArgumentFlattener.Flatten(node.Arguments, 1)
            : (IReadOnlyList<Expression>)node.Arguments;

        if (VariadicNames.Contains(name) && args.Count < 2)
            throw new NotSupportedException($"The {name} function requires at least one value argument.");

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
}
