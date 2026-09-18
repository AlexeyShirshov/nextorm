using System.Linq.Expressions;

namespace nextorm.core;

/// <summary>
/// Translates the scalar and aggregate built-ins of <see cref="NORM.NORM_SQL"/> that are not part of
/// the array/JSON surface: <c>nullif</c>, <c>greatest</c>/<c>least</c>, <c>date_trunc</c> and the
/// <c>string_agg</c>/<c>array_agg</c> aggregates (including their optional
/// <c>FILTER (WHERE ...)</c> clause).
/// <para>
/// Each provider-specific group is guarded by a dialect capability
/// (<see cref="ISqlDialect.SupportsGreatestLeast"/>, <see cref="ISqlDialect.SupportsDateTrunc"/>,
/// <see cref="ISqlDialect.SupportsStringArrayAggregates"/>, <see cref="ISqlDialect.SupportsFilter"/>)
/// so a provider that cannot express the construct fails with a clear message instead of emitting
/// invalid SQL.
/// </para>
/// </summary>
internal static class BuiltinFunctionTranslator
{
    /// <summary>Translates a built-in call; returns <c>false</c> when the call is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        switch (node.Method.Name)
        {
            case nameof(NORM.NORM_SQL.nullif) when node.Arguments.Count == 2:
                EmitNullIf(visitor, node.Arguments);
                return true;
            case nameof(NORM.NORM_SQL.greatest):
                EmitGreatestLeast(visitor, node, greatest: true);
                return true;
            case nameof(NORM.NORM_SQL.least):
                EmitGreatestLeast(visitor, node, greatest: false);
                return true;
            case nameof(NORM.NORM_SQL.date_trunc) when node.Arguments.Count == 2:
                EmitDateTrunc(visitor, node.Arguments);
                return true;
            case nameof(NORM.NORM_SQL.date_add) when node.Arguments.Count == 3:
                EmitDateAdd(visitor, node.Arguments);
                return true;
            case nameof(NORM.NORM_SQL.date_diff) when node.Arguments.Count == 3:
                EmitDateDiff(visitor, node.Arguments);
                return true;
            case nameof(NORM.NORM_SQL.end_of_month) when node.Arguments.Count == 1:
                EmitEndOfMonth(visitor, node.Arguments);
                return true;
            case nameof(NORM.NORM_SQL.date_from_parts) when node.Arguments.Count == 3:
                EmitDateFromParts(visitor, node.Arguments);
                return true;
            case nameof(NORM.NORM_SQL.contains) when node.Arguments.Count == 2:
                EmitFullText(visitor, node, "contains");
                return true;
            case nameof(NORM.NORM_SQL.freetext) when node.Arguments.Count == 2:
                EmitFullText(visitor, node, "freetext");
                return true;
            case nameof(NORM.NORM_SQL.string_agg) when node.Arguments.Count is 2 or 3:
                EmitStringAgg(visitor, node.Arguments);
                return true;
            case nameof(NORM.PG.array_agg) when node.Arguments.Count is 1 or 2:
                EmitArrayAgg(visitor, node.Arguments);
                return true;
            default:
                return false;
        }
    }

    /// <summary><c>nullif(value, other)</c>; ANSI and portable, so it is not capability-gated.</summary>
    private static void EmitNullIf(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeNullIf(visitor.VisitToString(args[0]), visitor.VisitToString(args[1])));
    }

    /// <summary><c>greatest(...)</c>/<c>least(...)</c> over the flattened <c>params</c> argument array.</summary>
    private static void EmitGreatestLeast(BaseExpressionVisitor visitor, MethodCallExpression node, bool greatest)
    {
        if (!visitor.Dialect.SupportsGreatestLeast)
            throw new NotSupportedException("The greatest/least functions are not supported by this provider.");

        var items = FlattenParams(node.Arguments);

        if (items.Count == 0)
            throw new NotSupportedException("greatest/least requires at least one argument.");

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, items.Count); i < cnt; i++)
                visitor.Visit(items[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new string[items.Count];
        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
            rendered[i] = visitor.VisitToString(items[i]);

        visitor.Builder!.Append(greatest ? visitor.Dialect.MakeGreatest(rendered) : visitor.Dialect.MakeLeast(rendered));
    }

    /// <summary><c>date_trunc(field, value)</c> with a validated constant date-part name.</summary>
    private static void EmitDateTrunc(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsDateTrunc)
            throw new NotSupportedException("The date_trunc function is not supported by this provider.");

        if (!SqlLiteral.TryGetConstantString(args[0], out var field))
            throw new NotSupportedException("The date_trunc field must be a constant string.");

        field = field.ToLowerInvariant();
        if (!visitor.Dialect.SupportsDateTruncField(field))
            throw new NotSupportedException($"'{field}' is not a supported date_trunc field for this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDateTrunc(field, visitor.VisitToString(args[1])));
    }

    /// <summary><c>date_add(field, amount, value)</c> with a validated constant date-part name.</summary>
    private static void EmitDateAdd(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsDateArithmetic)
            throw new NotSupportedException("The date arithmetic functions are not supported by this provider.");

        if (!SqlLiteral.TryGetConstantString(args[0], out var field))
            throw new NotSupportedException("The date_add field must be a constant string.");

        field = field.ToLowerInvariant();
        if (!visitor.Dialect.SupportsDateAddField(field))
            throw new NotSupportedException($"'{field}' is not a supported date_add field for this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[1]);
            visitor.Visit(args[2]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDateAdd(
            field,
            visitor.VisitToString(args[1]),
            visitor.VisitToString(args[2])));
    }

    /// <summary><c>date_diff(field, start, end)</c> with a validated constant date-part name.</summary>
    private static void EmitDateDiff(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsDateArithmetic)
            throw new NotSupportedException("The date arithmetic functions are not supported by this provider.");

        if (!SqlLiteral.TryGetConstantString(args[0], out var field))
            throw new NotSupportedException("The date_diff field must be a constant string.");

        field = field.ToLowerInvariant();
        if (!visitor.Dialect.SupportsDateDiffField(field))
            throw new NotSupportedException($"'{field}' is not a supported date_diff field for this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[1]);
            visitor.Visit(args[2]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDateDiff(
            field,
            visitor.VisitToString(args[1]),
            visitor.VisitToString(args[2])));
    }

    /// <summary><c>end_of_month(value)</c>.</summary>
    private static void EmitEndOfMonth(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsDateArithmetic)
            throw new NotSupportedException("The date arithmetic functions are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeEndOfMonth(visitor.VisitToString(args[0])));
    }

    /// <summary><c>date_from_parts(year, month, day)</c>.</summary>
    private static void EmitDateFromParts(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsDateArithmetic)
            throw new NotSupportedException("The date arithmetic functions are not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            visitor.Visit(args[2]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDateFromParts(
            visitor.VisitToString(args[0]),
            visitor.VisitToString(args[1]),
            visitor.VisitToString(args[2])));
    }

    /// <summary>
    /// <c>contains(column, search)</c>/<c>freetext(column, search)</c>. The predicate is a T-SQL
    /// condition; when it is projected as a value the dialect materialises it as a bit scalar.
    /// </summary>
    private static void EmitFullText(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        if (!visitor.Dialect.SupportsFullText)
            throw new NotSupportedException("The full-text predicates (contains/freetext) are not supported by this provider.");

        var args = node.Arguments;

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var column = visitor.VisitToString(args[0]);
        var search = visitor.VisitToString(args[1]);

        visitor.Builder!.Append(visitor.Dialect.MakeBooleanPredicate(
            visitor.Dialect.MakeFullText(name, column, search),
            visitor.IsPredicateContext));
    }

    private static void EmitStringAgg(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsStringAgg)
            throw new NotSupportedException("The string_agg/array_agg aggregates are not supported by this provider.");

        var filter = args.Count == 3 ? args[2] : null;
        RequireFilterSupport(visitor, filter);

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            if (filter is not null) AggregateFilter.Append(visitor, filter);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeStringAgg(visitor.VisitToString(args[0]), visitor.VisitToString(args[1])));

        if (filter is not null) AggregateFilter.Append(visitor, filter);
    }

    private static void EmitArrayAgg(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsArrayAgg)
            throw new NotSupportedException("The string_agg/array_agg aggregates are not supported by this provider.");

        var filter = args.Count == 2 ? args[1] : null;
        RequireFilterSupport(visitor, filter);

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            if (filter is not null) AggregateFilter.Append(visitor, filter);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeArrayAgg(visitor.VisitToString(args[0])));

        if (filter is not null) AggregateFilter.Append(visitor, filter);
    }

    private static void RequireFilterSupport(BaseExpressionVisitor visitor, Expression? filter)
    {
        if (filter is not null && !visitor.Dialect.SupportsFilter)
            throw new NotSupportedException("The FILTER clause is not supported by this provider.");
    }

    /// <summary>
    /// Flattens a <c>params</c> argument list. The C# compiler wraps the arguments of a <c>params</c>
    /// call in a single <see cref="NewArrayExpression"/>, which is turned back into the items.
    /// </summary>
    private static IReadOnlyList<Expression> FlattenParams(IReadOnlyList<Expression> args)
        => args.Count == 1 && args[0] is NewArrayExpression { Expressions: var expressions }
            ? expressions
            : args;
}
