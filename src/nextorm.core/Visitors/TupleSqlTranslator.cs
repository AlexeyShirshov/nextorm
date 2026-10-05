using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the tuple surface into SQL for a provider with a native row-value type: a tuple
/// constructor (<c>Tuple.Create(a, b, ...)</c>, <c>new Tuple&lt;...&gt;(...)</c> or
/// <c>new ValueTuple&lt;...&gt;(...)</c>) as the dialect's row constructor (PostgreSQL <c>ROW(a, b)</c>,
/// ClickHouse <c>tuple(a, b)</c>), and <c>.ItemN</c> access either folded to an inline constructor's
/// argument or rendered through the dialect's positional element access. Gated by
/// <see cref="ISqlDialect.Tuple"/>; a provider without it rejects the surface with a clear message.
/// </summary>
internal static class TupleSqlTranslator
{
    /// <summary>Renders <c>Tuple.Create(a, b, ...)</c>; returns <c>false</c> for other calls.</summary>
    internal static bool TryTranslateCreate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Object is not null || node.Method.DeclaringType != typeof(Tuple) || node.Method.Name != nameof(Tuple.Create))
            return false;

        RenderConstructor(visitor, node.Arguments);
        return true;
    }

    /// <summary>
    /// Renders a <c>new Tuple&lt;...&gt;(...)</c> / <c>new ValueTuple&lt;...&gt;(...)</c> constructor;
    /// returns <c>false</c> for other <c>new</c> expressions.
    /// </summary>
    internal static bool TryTranslateNew(BaseExpressionVisitor visitor, NewExpression node)
    {
        if (!TypeFacts.IsTupleLike(node.Type))
            return false;

        RenderConstructor(visitor, node.Arguments);
        return true;
    }

    /// <summary>
    /// Renders <c>.ItemN</c> access on a tuple. An inline constructor folds to its N-th argument; a
    /// server-side tuple value is rendered through the dialect's positional element access. Returns
    /// <c>false</c> for other members; a tuple that does not reference the query is left to constant
    /// folding.
    /// </summary>
    internal static bool TryTranslateElement(BaseExpressionVisitor visitor, MemberExpression node)
    {
        var declaringType = node.Member.DeclaringType;
        if (declaringType is null || !TypeFacts.IsTupleLike(declaringType) || node.Expression is null)
            return false;

        var name = node.Member.Name;
        if (name.Length <= 4 || !name.StartsWith("Item", StringComparison.Ordinal))
            return false;
        if (!int.TryParse(name.AsSpan(4), out var index))
            return false;

        if (!node.Expression.Has<ParameterExpression>())
            return false;

        if (TryGetInlineTupleArguments(node.Expression, out var arguments))
        {
            if (index < 1 || index > arguments.Count)
                return false;

            if (visitor.IsParamMode)
            {
                visitor.Visit(arguments[index - 1]);
                return true;
            }

            visitor.NeedAliasForColumn = true;
            visitor.Builder!.Append(visitor.VisitToString(arguments[index - 1]));
            return true;
        }

        if (!visitor.Dialect.SupportsTupleFunctions || visitor.Dialect.Tuple is not { } tuple)
            throw new NotSupportedException(
                $"Tuple element access is not supported by provider '{visitor.Dialect.GetType().Name}'.");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Expression);
            return true;
        }

        Func<string, int, string?> renderElement = tuple.RenderElement;
        var renderedElement = renderElement(visitor.VisitToString(node.Expression), index);
        if (renderedElement is null)
            throw new NotSupportedException(
                $"Positional tuple element access (.ItemN) on a server-side tuple is not supported by provider '{visitor.Dialect.GetType().Name}'.");

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(renderedElement);
        return true;
    }

    /// <summary>
    /// True when <paramref name="expression"/> is a tuple constructor (possibly under a <c>Convert</c>),
    /// i.e. the direct operand of a comparison that a provider with a flat row constructor may render.
    /// </summary>
    internal static bool IsDirectTupleConstructor(Expression expression) => TypeFacts.UnwrapConvert(expression) switch
    {
        NewExpression newExpression when TypeFacts.IsTupleLike(newExpression.Type) => true,
        MethodCallExpression call when call.Object is null
            && call.Method.DeclaringType == typeof(Tuple)
            && call.Method.Name == nameof(Tuple.Create) => true,
        _ => false,
    };

    /// <summary>
    /// Rejects a comparison operand that is a tuple but not an inline row constructor on a provider with
    /// the flat row form (MySQL/MariaDB/SQLite). Such an operand (a captured local, a parameter or a
    /// tuple-typed member) would otherwise fold to a single <see cref="Tuple"/> parameter and reach the
    /// driver as an unbindable value on a provider without a server-side row type.
    /// </summary>
    internal static void ValidateComparisonOperand(BaseExpressionVisitor visitor, Expression operand)
    {
        if (visitor.IsParamMode || !IsFlatRowConstructorProvider(visitor.Dialect))
            return;
        if (IsDirectTupleConstructor(operand) || !TypeFacts.IsTupleLike(operand.Type))
            return;

        throw new NotSupportedException(
            $"A row comparison operand that is not an inline row constructor is not supported by provider '{visitor.Dialect.GetType().Name}'; " +
            "both operands must be inline row constructors.");
    }

    /// <summary>
    /// Rejects a flat-provider row constructor compared against a null literal. A row value is never a
    /// nullable scalar, so the comparison has no SQL form; without this check it reaches the generic
    /// "wrong position" rejection, which does not reflect the real reason.
    /// </summary>
    internal static void RejectNullComparisonOfFlatTupleConstructor(BaseExpressionVisitor visitor, Expression operand)
    {
        if (visitor.IsParamMode || !IsFlatRowConstructorProvider(visitor.Dialect) || !IsDirectTupleConstructor(operand))
            return;

        throw new NotSupportedException(
            $"A row constructor cannot be compared to null by provider '{visitor.Dialect.GetType().Name}'.");
    }

    // ITupleRenderer encodes positional access as RenderElement returning null (PostgreSQL/ClickHouse
    // return the field expression, MySQL/MariaDB/SQLite return null). Probing with a placeholder row keeps
    // the check side-effect-free and independent of any specific provider type.
    private static bool IsFlatRowConstructor(ITupleRenderer tuple) => tuple.RenderElement("row", 1) is null;

    private static bool IsFlatRowConstructorProvider(ISqlDialect dialect)
        => dialect.SupportsTupleFunctions && dialect.Tuple is { } tuple && IsFlatRowConstructor(tuple);

    private static void RenderConstructor(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        if (!visitor.Dialect.SupportsTupleFunctions || visitor.Dialect.Tuple is not { } tuple)
            throw new NotSupportedException(
                $"Row constructors are not supported by provider '{visitor.Dialect.GetType().Name}'.");

        // A provider whose ITupleRenderer has no positional element access renders the row constructor as
        // the flat ANSI tuple "(a, b)" (MySQL/MariaDB/SQLite). That form is valid SQL only as an operand of
        // a row comparison in WHERE/HAVING/JOIN ON; in any other position (projection, ORDER BY/GROUP BY, a
        // function argument, or a comparison nested inside a function argument) it would be invalid or
        // ambiguous, so it is rejected at preparation with a provider-named message. The clause origin is
        // the visitor's predicate context; the position flag is scoped to one direct comparison operand.
        if (IsFlatRowConstructor(tuple)
            && !(visitor.IsPredicateContext && visitor.IsDirectTupleComparisonOperand))
            throw new NotSupportedException(
                $"A row constructor in this position is not supported by provider '{visitor.Dialect.GetType().Name}'; " +
                "it is supported only as a direct comparison operand in WHERE/HAVING/JOIN ON.");

        var fields = new string[args.Count];
        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
            fields[i] = visitor.VisitToString(args[i]);

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(tuple.RenderConstructor(fields));
    }

    private static bool TryGetInlineTupleArguments(Expression expression, out IReadOnlyList<Expression> arguments)
    {
        switch (expression)
        {
            case NewExpression newExpression when TypeFacts.IsTupleLike(newExpression.Type):
                arguments = newExpression.Arguments;
                return true;
            case MethodCallExpression call when call.Object is null
                && call.Method.DeclaringType == typeof(Tuple)
                && call.Method.Name == nameof(Tuple.Create):
                arguments = call.Arguments;
                return true;
            default:
                arguments = [];
                return false;
        }
    }
}
