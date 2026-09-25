using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the scalar and aggregate built-ins of <see cref="CommonFunctions"/> that are not part of
/// the array/JSON surface: <c>nullif</c>, <c>greatest</c>/<c>least</c>, the <c>iif</c>/<c>choose</c>
/// conditionals (plus the ClickHouse-only <c>multiIf</c>), the <c>date_trunc</c>/<c>date_add</c>/<c>date_diff</c>/<c>end_of_month</c>/
/// <c>date_from_parts</c> date helpers, <c>contains</c>/<c>freetext</c> and the
/// <c>string_agg</c>/<c>array_agg</c> aggregates (including their optional
/// <c>FILTER (WHERE ...)</c> clause).
/// <para>
/// Each provider-specific group is guarded by a dialect capability
/// (<see cref="ISqlDialect.SupportsGreatestLeast"/>, <see cref="ISqlDialect.Iif"/>,
/// <see cref="ISqlDialect.SupportsChoose"/>, <see cref="ISqlDialect.MultiIf"/>,
/// <see cref="ISqlDialect.SupportsDateTrunc"/>,
/// <see cref="ISqlDialect.SupportsDateArithmetic"/>, <see cref="ISqlDialect.SupportsFullText"/>,
/// <see cref="ISqlDialect.SupportsStringArrayAggregates"/>, <see cref="ISqlDialect.AggregateFilterStyle"/>)
/// so a provider that cannot express the construct fails with a clear message instead of emitting
/// invalid SQL.
/// </para>
/// </summary>
internal static class BuiltinFunctionTranslator
{
    /// <summary>Translates a built-in call; returns <c>false</c> when the call is not one of them.</summary>
    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (CrossProviderScalarTranslator.TryTranslate(visitor, node))
            return true;

        switch (node.Method.Name)
        {
            case nameof(CommonFunctions.nullif) when node.Arguments.Count == 2:
                EmitNullIf(visitor, node.Arguments);
                return true;
            case nameof(CommonFunctions.greatest):
                EmitGreatestLeast(visitor, node, greatest: true);
                return true;
            case nameof(CommonFunctions.least):
                EmitGreatestLeast(visitor, node, greatest: false);
                return true;
            case nameof(CommonFunctions.iif) when node.Arguments.Count == 3:
                EmitIif(visitor, node.Arguments);
                return true;
            case nameof(SqlServerFunctions.choose) when node.Arguments.Count == 2:
                EmitChoose(visitor, FlattenChoose(node.Arguments));
                return true;
            case nameof(ClickHouseFunctions.multi_if) when node.Arguments.Count == 1:
                EmitMultiIf(visitor, node);
                return true;
            case nameof(ClickHouseFunctions.when):
            case nameof(ClickHouseFunctions.otherwise):
                throw new NotSupportedException("The when/otherwise branches can only be used inside multi_if.");
            case nameof(CommonFunctions.date_trunc) when node.Arguments.Count == 2:
                EmitDateTrunc(visitor, node.Arguments);
                return true;
            case nameof(CommonFunctions.date_add) when node.Arguments.Count == 3:
                EmitDateAdd(visitor, node.Arguments);
                return true;
            case nameof(CommonFunctions.date_diff) when node.Arguments.Count == 3:
                EmitDateDiff(visitor, node.Arguments);
                return true;
            case nameof(CommonFunctions.date_diff_big) when node.Arguments.Count == 3:
                EmitDateDiff(visitor, node.Arguments, big: true);
                return true;
            case nameof(CommonFunctions.end_of_month) when node.Arguments.Count == 1:
                EmitEndOfMonth(visitor, node.Arguments);
                return true;
            case nameof(CommonFunctions.date_from_parts) when node.Arguments.Count == 3:
                EmitDateFromParts(visitor, node.Arguments);
                return true;
            case nameof(CommonFunctions.extract) when node.Arguments.Count == 2:
                EmitDatePart(visitor, node.Arguments, numeric: false);
                return true;
            case nameof(CommonFunctions.date_part) when node.Arguments.Count == 2:
                EmitDatePart(visitor, node.Arguments, numeric: true);
                return true;
            case nameof(CommonFunctions.contains) when node.Arguments.Count == 2:
                EmitFullText(visitor, node, "contains");
                return true;
            case nameof(CommonFunctions.freetext) when node.Arguments.Count == 2:
                EmitFullText(visitor, node, "freetext");
                return true;
            case nameof(CommonFunctions.string_agg) when node.Arguments.Count is 2 or 3:
                EmitStringAgg(visitor, node.Arguments);
                return true;
            case nameof(PostgresFunctions.array_agg) when node.Arguments.Count is 1 or 2:
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

    /// <summary>Renders the portable <c>iif</c> through the dialect's native conditional spelling.</summary>
    private static void EmitIif(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (visitor.Dialect.Iif is not { } iif)
            throw new NotSupportedException("The iif conditional function is not supported by this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            visitor.Visit(args[2]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(iif.Render(
            visitor.VisitToString(args[0]),
            visitor.VisitToString(args[1]),
            visitor.VisitToString(args[2])));
    }

    /// <summary>Renders the SQL Server-only <c>choose</c> conditional function (gated by <see cref="ISqlDialect.SupportsChoose"/>).</summary>
    private static void EmitChoose(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsChoose)
            throw new NotSupportedException("The choose conditional function is not supported by this provider.");

        SqlOperandTranslator.EmitFunction(visitor, "choose", args);
    }

    /// <summary>Flattens the <c>params</c> value array of <c>choose(index, values)</c> into positional arguments.</summary>
    private static IReadOnlyList<Expression> FlattenChoose(IReadOnlyList<Expression> args)
    {
        if (args.Count == 2 && args[1] is NewArrayExpression { Expressions: var values })
        {
            var list = new List<Expression>(values.Count + 1) { args[0] };
            list.AddRange(values);
            return list;
        }

        return args;
    }

    /// <summary>
    /// Renders the ClickHouse-only multi-branch conditional <c>multiIf(cond1, then1, ..., else)</c>
    /// (gated by <see cref="ISqlDialect.MultiIf"/>). The branches are the inline
    /// <c>when</c>/<c>otherwise</c> calls of the <c>params</c> array.
    /// </summary>
    private static void EmitMultiIf(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (visitor.Dialect.MultiIf is not { } multiIf)
            throw new NotSupportedException("The multiIf conditional function is not supported by this provider.");

        var branches = FlattenMultiIfBranches(node.Arguments[0]);

        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, branches.Count); i < cnt; i++)
            {
                if (branches[i].Condition is { } condition)
                    visitor.Visit(condition);

                visitor.Visit(branches[i].Value);
            }

            return;
        }

        visitor.NeedAliasForColumn = true;
        var rendered = new List<string>(branches.Count * 2);
        for (var (i, cnt) = (0, branches.Count); i < cnt; i++)
        {
            if (branches[i].Condition is { } condition)
                rendered.Add(visitor.VisitToString(condition));

            rendered.Add(visitor.VisitToString(branches[i].Value));
        }

        visitor.Builder!.Append(multiIf.Render(
            rendered,
            Nullable.GetUnderlyingType(node.Type) ?? node.Type));
    }

    /// <summary>
    /// Flattens the <c>params</c> branch array of <c>multi_if</c> into condition/value pairs and the
    /// trailing else value (<c>Condition == null</c>). The branches must be inline
    /// <c>when(condition, value)</c>/<c>otherwise(value)</c> calls, with exactly one trailing
    /// <c>otherwise</c>.
    /// </summary>
    private static IReadOnlyList<(Expression? Condition, Expression Value)> FlattenMultiIfBranches(Expression expression)
    {
        if (expression is not NewArrayExpression { Expressions: var items } || items.Count == 0)
            throw new NotSupportedException("The multiIf branches must be inline when(...)/otherwise(...) calls.");

        var branches = new List<(Expression?, Expression)>(items.Count);
        var hasElse = false;

        for (var (i, cnt) = (0, items.Count); i < cnt; i++)
        {
            if (items[i] is not MethodCallExpression call
                || call.Method.DeclaringType != typeof(ClickHouseFunctions)
                || call.Method.Name is not (nameof(ClickHouseFunctions.when) or nameof(ClickHouseFunctions.otherwise))
                || call.Arguments.Count == 0)
                throw new NotSupportedException("The multiIf branches must be inline when(...)/otherwise(...) calls.");

            if (call.Method.Name == nameof(ClickHouseFunctions.otherwise))
            {
                if (i != items.Count - 1)
                    throw new NotSupportedException("The multiIf otherwise(...) branch must be the last one.");

                hasElse = true;
                branches.Add((null, call.Arguments[0]));
            }
            else
            {
                if (call.Arguments.Count != 2)
                    throw new NotSupportedException("A multiIf when(...) branch requires a condition and a value.");

                branches.Add((call.Arguments[0], call.Arguments[1]));
            }
        }

        if (!hasElse)
            throw new NotSupportedException("multiIf requires a final otherwise(...) branch.");

        if (branches.Count < 2)
            throw new NotSupportedException("multiIf requires at least one when(...) branch.");

        return branches;
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
            visitor.Dialect.PromoteDateOperand(field, visitor.VisitToString(args[2]))));
    }

    /// <summary><c>date_diff(field, start, end)</c> with a validated constant date-part name.</summary>
    private static void EmitDateDiff(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args, bool big = false)
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
        var start = visitor.VisitToString(args[1]);
        var end = visitor.VisitToString(args[2]);
        visitor.Builder!.Append(big
            ? visitor.Dialect.MakeDateDiffBig(field, start, end)
            : visitor.Dialect.MakeDateDiff(field, start, end));
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
    /// <c>extract(part, value)</c>/<c>date_part(part, value)</c> with a validated constant date part.
    /// <paramref name="numeric"/> selects the double-valued surface (currently only <c>epoch</c>);
    /// the integer surface rejects <c>epoch</c> and the numeric surface rejects every integer part.
    /// </summary>
    private static void EmitDatePart(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args, bool numeric)
    {
        if (!SqlLiteral.TryGetConstantString(args[0], out var part))
            throw new NotSupportedException("The date part must be a constant string.");

        part = part.ToLowerInvariant();

        if (numeric && part != "epoch")
            throw new NotSupportedException($"date_part only provides the numeric 'epoch' part; use extract for the '{part}' part.");
        if (!numeric && part == "epoch")
            throw new NotSupportedException("'epoch' is a numeric date part; use date_part.");

        if (!visitor.Dialect.SupportsDatePart(part))
            throw new NotSupportedException($"'{part}' is not a supported date part for this provider.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[1]);
            return;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(visitor.Dialect.MakeDatePart(part, visitor.VisitToString(args[1])));
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
        AggregateFilter.RequireSupport(visitor, filter);

        if (visitor.IsParamMode)
        {
            visitor.Visit(args[0]);
            visitor.Visit(args[1]);
            if (filter is not null) AggregateFilter.Append(visitor, filter);
            return;
        }

        visitor.NeedAliasForColumn = true;
        var value = visitor.VisitToString(args[0]);
        var delimiter = visitor.VisitToString(args[1]);

        // ClickHouse has no ANSI FILTER clause: its filtered string_agg is
        // arrayStringConcat(groupArrayIf(value, predicate), delimiter).
        if (filter is not null && visitor.Dialect.AggregateFilterStyle == AggregateFilterStyle.IfCombinator)
        {
            var predicate = AggregateFilter.RenderPredicate(visitor, filter);
            visitor.Builder!.Append(visitor.Dialect.MakeFilteredStringAgg(value, delimiter, predicate));
            return;
        }

        visitor.Builder!.Append(visitor.Dialect.MakeStringAgg(value, delimiter));

        if (filter is not null) AggregateFilter.Append(visitor, filter);
    }

    private static void EmitArrayAgg(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (!visitor.Dialect.SupportsArrayAgg)
            throw new NotSupportedException("The string_agg/array_agg aggregates are not supported by this provider.");

        var filter = args.Count == 2 ? args[1] : null;
        AggregateFilter.RequireSupport(visitor, filter);

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

    /// <summary>
    /// Flattens a <c>params</c> argument list. The C# compiler wraps the arguments of a <c>params</c>
    /// call in a single <see cref="NewArrayExpression"/>, which is turned back into the items.
    /// </summary>
    private static IReadOnlyList<Expression> FlattenParams(IReadOnlyList<Expression> args)
        => args.Count == 1 && args[0] is NewArrayExpression { Expressions: var expressions }
            ? expressions
            : args;
}
