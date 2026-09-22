using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Translates the tuple surface into SQL for a provider with a native tuple type: the
/// <c>Tuple.Create(a, b, ...)</c> constructor as <c>tuple(a, b, ...)</c> and
/// <c>System.Tuple&lt;...&gt;.ItemN</c> access on a server-side tuple as
/// <c>tupleElement(tuple, n)</c>. Both are gated by <see cref="ISqlDialect.SupportsTupleFunctions"/>;
/// a provider without it rejects them with a clear message.
/// </summary>
internal static class TupleSqlTranslator
{
    /// <summary>Renders <c>Tuple.Create(a, b, ...)</c> as <c>tuple(a, b, ...)</c>; returns <c>false</c> for other calls.</summary>
    internal static bool TryTranslateCreate(BaseExpressionVisitor visitor, MethodCallExpression node)
    {
        if (node.Object is not null || node.Method.DeclaringType != typeof(Tuple) || node.Method.Name != nameof(Tuple.Create))
            return false;

        if (!visitor.Dialect.SupportsTupleFunctions)
            throw new NotSupportedException(
                "Tuple construction requires a provider with a native tuple type (ClickHouse).");

        RenderTuple(visitor, node.Arguments);
        return true;
    }

    /// <summary>
    /// Renders <c>System.Tuple&lt;...&gt;.ItemN</c> access on a server-side tuple as
    /// <c>tupleElement(tuple, N)</c>; returns <c>false</c> for other members. A tuple that does not
    /// reference the query (a captured/local value) is left to constant folding.
    /// </summary>
    internal static bool TryTranslateElement(BaseExpressionVisitor visitor, MemberExpression node)
    {
        var declaringType = node.Member.DeclaringType;
        if (declaringType is null || !TypeFacts.IsTupleType(declaringType) || node.Expression is null)
            return false;

        var name = node.Member.Name;
        if (name.Length <= 4 || !name.StartsWith("Item", StringComparison.Ordinal))
            return false;
        if (!int.TryParse(name.AsSpan(4), out var index))
            return false;

        if (!node.Expression.Has<ParameterExpression>())
            return false;

        if (!visitor.Dialect.SupportsTupleFunctions)
            throw new NotSupportedException(
                "Tuple element access requires a provider with a native tuple type (ClickHouse).");

        if (visitor.IsParamMode)
        {
            visitor.Visit(node.Expression);
            return true;
        }

        visitor.NeedAliasForColumn = true;
        visitor.Builder!.Append("tupleElement(");

        if (node.Expression is NewExpression newTuple && TypeFacts.IsTupleType(newTuple.Type))
            RenderTuple(visitor, newTuple.Arguments);
        else
            visitor.Visit(node.Expression);

        visitor.Builder!.Append(", ").Append(index).Append(')');
        return true;
    }

    private static void RenderTuple(BaseExpressionVisitor visitor, IReadOnlyList<Expression> args)
    {
        if (visitor.IsParamMode)
        {
            for (var (i, cnt) = (0, args.Count); i < cnt; i++)
                visitor.Visit(args[i]);

            return;
        }

        visitor.NeedAliasForColumn = true;
        var builder = visitor.Builder!;
        builder.Append("tuple(");

        for (var (i, cnt) = (0, args.Count); i < cnt; i++)
        {
            if (i > 0)
                builder.Append(", ");

            visitor.Visit(args[i]);
        }

        builder.Append(')');
    }
}
