using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Translates the <see cref="PostgresFunctions"/> range surface over a <see cref="Range{T}"/> stored as
/// a pair of scalar columns (<see cref="RangeColumnsAttribute"/>) on a provider without a native range
/// type. Each predicate is rendered as comparisons between the two bounds, with SQL <c>NULL</c> treated
/// as an (un)bounded side and the bound inclusivity folded from the mapping.
/// <para>
/// Only predicates and inspection functions are expressible over a column pair; the range-returning
/// operators (<c>range_union</c>, <c>range_intersection</c>, <c>range_difference</c>, constructors and
/// the range aggregates) have no scalar SQL form and are rejected with <see cref="NotSupportedException"/>.
/// </para>
/// </summary>
internal static class RangeColumnsSqlTranslator
{
    private static readonly ConcurrentDictionary<Type, RangeAccessors> Accessors = new();

    private static readonly HashSet<string> Predicates = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.overlaps),
        nameof(PostgresFunctions.range_contains),
        nameof(PostgresFunctions.range_contained_by),
        nameof(PostgresFunctions.range_adjacent),
        nameof(PostgresFunctions.range_strictly_left_of),
        nameof(PostgresFunctions.range_strictly_right_of),
        nameof(PostgresFunctions.range_not_extend_right_of),
        nameof(PostgresFunctions.range_not_extend_left_of),
    };

    private static readonly HashSet<string> Inspection = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.isempty),
        nameof(PostgresFunctions.lower),
        nameof(PostgresFunctions.upper),
        nameof(PostgresFunctions.lower_inc),
        nameof(PostgresFunctions.upper_inc),
        nameof(PostgresFunctions.lower_inf),
        nameof(PostgresFunctions.upper_inf),
    };

    private static readonly HashSet<string> RangeReturning = new(StringComparer.Ordinal)
    {
        nameof(PostgresFunctions.range_union),
        nameof(PostgresFunctions.range_intersection),
        nameof(PostgresFunctions.range_difference),
        nameof(PostgresFunctions.empty_range),
        nameof(PostgresFunctions.int4range),
        nameof(PostgresFunctions.int8range),
        nameof(PostgresFunctions.numrange),
        nameof(PostgresFunctions.tsrange),
        nameof(PostgresFunctions.tstzrange),
        nameof(PostgresFunctions.daterange),
        nameof(PostgresFunctions.range_merge),
        nameof(PostgresFunctions.multirange),
        nameof(PostgresFunctions.range_agg),
        nameof(PostgresFunctions.range_intersect_agg),
    };

    internal static bool TryTranslate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Method.DeclaringType != typeof(PostgresFunctions))
            return false;

        var name = node.Method.Name;

        if (RangeReturning.Contains(name))
        {
            if (AnyPairColumn(visitor, node))
                throw new NotSupportedException($"The PostgreSQL range function '{name}' returns a range and cannot be expressed over a pair of columns. Use a provider with a native range type (PostgreSQL) or a range-returning projection.");

            return false;
        }

        if (!Predicates.Contains(name) && !Inspection.Contains(name))
            return false;

        if (!AnyPairColumn(visitor, node))
            return false;

        if (!visitor.Dialect.SupportsRangeColumns)
            throw new NotSupportedException($"The provider '{visitor.Dialect.GetType().Name}' does not support range functions over a pair of columns.");

        var sql = BuildSql(visitor, node, name);

        if (!visitor.IsParamMode)
        {
            // Boolean results go through the dialect so a provider without a native boolean type
            // (SQL Server) can materialise a scalar projection while keeping a predicate usable as-is.
            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(IsBoolean(name) ? visitor.Dialect.MakeBooleanPredicate(sql, visitor.IsPredicateContext) : sql);
        }

        return true;
    }

    private static bool IsBoolean(string name)
        => Predicates.Contains(name) || (Inspection.Contains(name)
            && name is not (nameof(PostgresFunctions.lower) or nameof(PostgresFunctions.upper)));

    private static bool AnyPairColumn(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        for (var i = 0; i < node.Arguments.Count; i++)
        {
            if (TryResolvePairColumn(visitor, Unwrap(node.Arguments[i]), out _))
                return true;
        }

        return false;
    }

    private static string BuildSql(BaseExpressionVisitor visitor, MethodCallExpression node, string name)
    {
        if (Inspection.Contains(name))
        {
            var operand = Resolve(visitor, node.Arguments[0]);
            return name switch
            {
                // A pair can never be empty; infinity means an unbounded (NULL) column.
                nameof(PostgresFunctions.isempty) => Or(),
                nameof(PostgresFunctions.lower) => operand.Lower.Sql,
                nameof(PostgresFunctions.upper) => operand.Upper.Sql,
                nameof(PostgresFunctions.lower_inc) => operand.Lower.Inclusive ? NotNull(operand.Lower) ?? And() : Or(),
                nameof(PostgresFunctions.upper_inc) => operand.Upper.Inclusive ? NotNull(operand.Upper) ?? And() : Or(),
                nameof(PostgresFunctions.lower_inf) => operand.Lower.NullSql ?? Or(),
                nameof(PostgresFunctions.upper_inf) => operand.Upper.NullSql ?? Or(),
                _ => throw new NotSupportedException($"Unsupported range inspection function '{name}'.")
            };
        }

        var a = Resolve(visitor, node.Arguments[0]);

        if (name == nameof(PostgresFunctions.range_contains) && !RangeTypeFacts.IsRange(node.Arguments[1].Type))
            return ContainsValue(a, RenderScalar(visitor, node.Arguments[1]));

        var b = Resolve(visitor, node.Arguments[1]);

        return name switch
        {
            nameof(PostgresFunctions.overlaps) => Overlaps(a, b),
            nameof(PostgresFunctions.range_contains) => Contains(a, b),
            nameof(PostgresFunctions.range_contained_by) => Contains(b, a),
            nameof(PostgresFunctions.range_adjacent) => Adjacent(a, b),
            nameof(PostgresFunctions.range_strictly_left_of) => StrictlyLeft(a, b),
            nameof(PostgresFunctions.range_strictly_right_of) => StrictlyLeft(b, a),
            nameof(PostgresFunctions.range_not_extend_right_of) => NotExtendRight(a, b),
            nameof(PostgresFunctions.range_not_extend_left_of) => NotExtendLeft(a, b),
            _ => throw new NotSupportedException($"Unsupported range predicate '{name}'.")
        };
    }

    private static RangeOperand Resolve(BaseExpressionVisitor visitor, Expression argument)
    {
        var exp = Unwrap(argument);

        if (TryResolvePairColumn(visitor, exp, out var operand))
            return operand;

        if (exp is MethodCallExpression call && call.Method.DeclaringType == typeof(PostgresFunctions) && RangeReturning.Contains(call.Method.Name))
            throw new NotSupportedException($"The PostgreSQL range function '{call.Method.Name}' returns a range and cannot be expressed over a pair of columns. Use a provider with a native range type (PostgreSQL) or a range-returning projection.");

        if (RangeTypeFacts.IsRange(exp.Type) && IsConstantRange(exp))
            return ResolveConstant(visitor, exp);

        throw new NotSupportedException($"The range operand '{exp}' is neither a range column pair nor a captured range value.");
    }

    private static bool TryResolvePairColumn(BaseExpressionVisitor visitor, Expression expression, out RangeOperand operand)
    {
        operand = default;
        if (expression is not MemberExpression member || member.Member is not PropertyInfo pi)
            return false;
        if (visitor.EntityType is null || !DataContextCache.Metadata.TryGetValue(visitor.EntityType, out var metadata))
            return false;

        RangeColumnsMetadata? columns = null;
        var props = metadata.Properties;
        for (var i = 0; i < props.Count; i++)
        {
            if (props[i].PropertyInfo == pi && props[i].RangeColumns is not null)
            {
                columns = props[i].RangeColumns;
                break;
            }
        }

        if (columns is null)
            return false;

        if (visitor.IsParamMode)
        {
            operand = default;
            return true;
        }

        var lower = RenderPairColumn(visitor, member, RangeColumnRole.Lower);
        var upper = RenderPairColumn(visitor, member, RangeColumnRole.Upper);
        operand = new RangeOperand(
            new Bound(lower, $"({lower} is null)", columns.LowerInclusive),
            new Bound(upper, $"({upper} is null)", columns.UpperInclusive));
        return true;
    }

    private static string RenderPairColumn(BaseExpressionVisitor visitor, MemberExpression member, RangeColumnRole role)
    {
        using var clone = visitor.Clone();
        clone.RangeColumnRole = role;
        clone.Visit(member);
        return clone.ToString();
    }

    private static RangeOperand ResolveConstant(BaseExpressionVisitor visitor, Expression expression)
    {
        var key = new ExpressionKey(expression, visitor.QueryProvider);
        if (!DataContextCache.ExpressionsCache.TryGetValue(key, out var compiled))
        {
            compiled = Expression.Lambda<Func<object>>(Expression.Convert(expression, typeof(object))).Compile();
            DataContextCache.ExpressionsCache[key] = compiled;
        }

        var value = ((Func<object>)compiled)();
        var accessors = Accessors.GetOrAdd(value.GetType(), static type => new RangeAccessors(type));

        if ((bool)accessors.IsEmpty.GetValue(value)!)
            throw new NotSupportedException("The empty range cannot be expressed over a pair of columns.");

        var lowerInfinite = (bool)accessors.LowerInfinite.GetValue(value)!;
        var upperInfinite = (bool)accessors.UpperInfinite.GetValue(value)!;

        var lower = MakeConstantBound(visitor, accessors.Lower.GetValue(value), lowerInfinite,
            lowerInfinite ? false : (bool)accessors.LowerInclusive.GetValue(value)!);
        var upper = MakeConstantBound(visitor, accessors.Upper.GetValue(value), upperInfinite,
            upperInfinite ? false : (bool)accessors.UpperInclusive.GetValue(value)!);

        return new RangeOperand(lower, upper);
    }

    private static Bound MakeConstantBound(BaseExpressionVisitor visitor, object? value, bool infinite, bool inclusive)
    {
        if (infinite)
            return new Bound("null", "1=1", false);

        var name = visitor.ParameterProvider.GetParamName();
        visitor.Params.Add(new Parameter(name, value));
        var sql = visitor.IsParamMode ? string.Empty : visitor.Dialect.MakeParam(name);
        return new Bound(sql, null, inclusive);
    }

    private static string RenderScalar(BaseExpressionVisitor visitor, Expression value)
    {
        if (visitor.IsParamMode)
        {
            visitor.Visit(value);
            return string.Empty;
        }

        return visitor.VisitToString(value);
    }

    private static string Overlaps(RangeOperand a, RangeOperand b)
    {
        var startBeforeEnd = Or(
            a.Lower.NullSql,
            b.Upper.NullSql,
            $"{a.Lower.Sql} < {b.Upper.Sql}",
            a.Lower.Inclusive && b.Upper.Inclusive ? $"{a.Lower.Sql} = {b.Upper.Sql}" : null);
        var otherStartBeforeEnd = Or(
            b.Lower.NullSql,
            a.Upper.NullSql,
            $"{b.Lower.Sql} < {a.Upper.Sql}",
            b.Lower.Inclusive && a.Upper.Inclusive ? $"{b.Lower.Sql} = {a.Upper.Sql}" : null);
        return And(startBeforeEnd, otherStartBeforeEnd);
    }

    private static string ContainsValue(RangeOperand range, string value)
        => And(
            Or(range.Lower.NullSql, $"{range.Lower.Sql} < {value}", range.Lower.Inclusive ? $"{range.Lower.Sql} = {value}" : null),
            Or(range.Upper.NullSql, $"{value} < {range.Upper.Sql}", range.Upper.Inclusive ? $"{value} = {range.Upper.Sql}" : null));

    private static string Contains(RangeOperand outer, RangeOperand inner)
    {
        var lowerOk = Or(
            outer.Lower.NullSql,
            And(
                inner.Lower.NullSql is null ? null : Not(inner.Lower.NullSql),
                Or(
                    $"{outer.Lower.Sql} < {inner.Lower.Sql}",
                    outer.Lower.Inclusive || !inner.Lower.Inclusive ? $"{outer.Lower.Sql} = {inner.Lower.Sql}" : null)));
        var upperOk = Or(
            outer.Upper.NullSql,
            And(
                inner.Upper.NullSql is null ? null : Not(inner.Upper.NullSql),
                Or(
                    $"{outer.Upper.Sql} > {inner.Upper.Sql}",
                    outer.Upper.Inclusive || !inner.Upper.Inclusive ? $"{outer.Upper.Sql} = {inner.Upper.Sql}" : null)));
        return And(lowerOk, upperOk);
    }

    private static string Adjacent(RangeOperand a, RangeOperand b)
    {
        var first = a.Upper.Inclusive != b.Lower.Inclusive
            ? And(NotNull(a.Upper), NotNull(b.Lower), $"{a.Upper.Sql} = {b.Lower.Sql}")
            : null;
        var second = b.Upper.Inclusive != a.Lower.Inclusive
            ? And(NotNull(b.Upper), NotNull(a.Lower), $"{b.Upper.Sql} = {a.Lower.Sql}")
            : null;
        return Or(first, second);
    }

    private static string StrictlyLeft(RangeOperand a, RangeOperand b)
        => And(
            And(NotNull(a.Upper), NotNull(b.Lower)),
            Or(
                $"{a.Upper.Sql} < {b.Lower.Sql}",
                a.Upper.Inclusive && b.Lower.Inclusive ? null : $"{a.Upper.Sql} = {b.Lower.Sql}"));

    private static string NotExtendRight(RangeOperand a, RangeOperand b)
    {
        var equalityAllowed = !(a.Upper.Inclusive && !b.Upper.Inclusive);
        return Or(
            b.Upper.NullSql,
            And(
                NotNull(a.Upper),
                Or(
                    $"{a.Upper.Sql} < {b.Upper.Sql}",
                    equalityAllowed ? $"{a.Upper.Sql} = {b.Upper.Sql}" : null)));
    }

    private static string NotExtendLeft(RangeOperand a, RangeOperand b)
    {
        var equalityAllowed = !(a.Lower.Inclusive && !b.Lower.Inclusive);
        return Or(
            b.Lower.NullSql,
            And(
                NotNull(a.Lower),
                Or(
                    $"{a.Lower.Sql} > {b.Lower.Sql}",
                    equalityAllowed ? $"{a.Lower.Sql} = {b.Lower.Sql}" : null)));
    }

    private static string? NotNull(Bound bound) => bound.NullSql is null ? null : Not(bound.NullSql);

    private static string Not(string sql) => $"(not {sql})";

    private static string Or(params string?[] terms) => Join(terms, " or ", "1=0");

    private static string And(params string?[] terms) => Join(terms, " and ", "1=1");

    private static string Join(string?[] terms, string separator, string empty)
    {
        List<string>? kept = null;
        foreach (var term in terms)
        {
            if (term is null)
                continue;
            (kept ??= []).Add(term);
        }

        return kept is null ? empty : kept.Count == 1 ? kept[0] : $"({string.Join(separator, kept)})";
    }

    private static bool IsConstantRange(Expression expression)
    {
        if (expression is ConstantExpression or NewExpression)
            return true;

        var root = expression;
        while (root is MemberExpression member)
            root = member.Expression!;

        return root is ConstantExpression || root is null;
    }

    private static Expression Unwrap(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? unary.Operand
            : expression;

    private readonly struct Bound(string sql, string? nullSql, bool inclusive)
    {
        public string Sql { get; } = sql;
        public string? NullSql { get; } = nullSql;
        public bool Inclusive { get; } = inclusive;
    }

    private readonly struct RangeOperand(Bound lower, Bound upper)
    {
        public Bound Lower { get; } = lower;
        public Bound Upper { get; } = upper;
    }

    private sealed class RangeAccessors
    {
        public RangeAccessors(Type rangeType)
        {
            Lower = rangeType.GetProperty(nameof(Range<int>.Lower))!;
            Upper = rangeType.GetProperty(nameof(Range<int>.Upper))!;
            LowerInclusive = rangeType.GetProperty(nameof(Range<int>.LowerInclusive))!;
            UpperInclusive = rangeType.GetProperty(nameof(Range<int>.UpperInclusive))!;
            LowerInfinite = rangeType.GetProperty(nameof(Range<int>.LowerInfinite))!;
            UpperInfinite = rangeType.GetProperty(nameof(Range<int>.UpperInfinite))!;
            IsEmpty = rangeType.GetProperty(nameof(Range<int>.IsEmpty))!;
        }

        public PropertyInfo Lower { get; }
        public PropertyInfo Upper { get; }
        public PropertyInfo LowerInclusive { get; }
        public PropertyInfo UpperInclusive { get; }
        public PropertyInfo LowerInfinite { get; }
        public PropertyInfo UpperInfinite { get; }
        public PropertyInfo IsEmpty { get; }
    }
}
