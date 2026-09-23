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
                "Tuple element access requires a provider with a native tuple type (PostgreSQL, ClickHouse).");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Expression);
            return true;
        }

        Func<string, int, string?> renderElement = tuple.RenderElement;
        if (renderElement is null)
            throw new NotSupportedException(
                "Positional tuple element access requires a provider with a native tuple type that supports element access (PostgreSQL, ClickHouse).");

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append(renderElement(visitor.VisitToString(node.Expression), index));
        return true;
    }

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
                "Tuple construction requires a provider with a native tuple type (PostgreSQL, ClickHouse).");

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
