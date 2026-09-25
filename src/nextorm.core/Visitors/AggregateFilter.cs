using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Renders the optional filter of an aggregate. The filter is carried by a
/// <c>Expression&lt;Func&lt;bool&gt;&gt;</c> argument (a quoted lambda); the predicate is rendered with
/// the same visitor state as the surrounding expression, so it can reference the query's columns and
/// its parameters line up between the SQL and the parameter-extraction passes.
/// <para>
/// The shape is selected by <see cref="ISqlDialect.AggregateFilterStyle"/>: the ANSI
/// <c>agg(x) filter (where predicate)</c> clause or the ClickHouse <c>-If</c> combinator
/// <c>aggIf(x, predicate)</c>. Only a dialect that returns a style other than
/// <see cref="AggregateFilterStyle.None"/> may filter an aggregate; the callers reject it with a clear
/// message otherwise.
/// </para>
/// </summary>
internal static class AggregateFilter
{
    /// <summary>True when <paramref name="expression"/> is a filter argument (a quoted lambda).</summary>
    internal static bool IsFilterExpression(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression };

    /// <summary>
    /// The provider's combinator spelling for <paramref name="aggregateName"/> (the ClickHouse
    /// <c>-If</c> combinator is the mapped aggregate name with the <c>If</c> suffix), for example
    /// <c>sum</c> to <c>sumIf</c> and <c>count</c> to <c>countIf</c>. Only used by dialects whose
    /// <see cref="ISqlDialect.AggregateFilterStyle"/> is <see cref="AggregateFilterStyle.IfCombinator"/>.
    /// </summary>
    internal static string CombinatorName(ISqlDialect dialect, string aggregateName)
        => dialect.MakeAggregate(aggregateName) + "If";

    /// <summary>
    /// Appends the ANSI <c> filter (where &lt;predicate&gt;)</c> clause for the quoted
    /// <paramref name="filterExpression"/>. In parameter mode the predicate is only walked, so its
    /// parameters are collected in the same order as in the SQL pass.
    /// </summary>
    internal static void Append(BaseExpressionVisitor visitor, Expression filterExpression)
    {
        if (!visitor.IsParamMode)
            visitor.Builder!.Append(visitor.Kw(" filter (where "));

        AppendPredicate(visitor, filterExpression);

        if (!visitor.IsParamMode)
            visitor.Builder!.Append(')');
    }

    /// <summary>
    /// Appends the ClickHouse <c>-If</c> combinator call
    /// <c>combinator([value, ]predicate)</c> for the quoted <paramref name="filterExpression"/>,
    /// where <paramref name="value"/> is the aggregate's already-typed value argument (the rendered
    /// argument is appended only in the SQL pass). <paramref name="combinator"/> is the mapped name
    /// (see <see cref="CombinatorName"/>), for example <c>sumIf</c> or <c>countIf</c>.
    /// </summary>
    internal static void AppendCombinator(BaseExpressionVisitor visitor, string combinator, Expression? value, Expression filterExpression)
    {
        if (visitor.IsParamMode)
        {
            if (value is not null)
                visitor.Visit(value);

            AppendPredicate(visitor, filterExpression);
            return;
        }

        visitor.Builder!.Append(combinator).Append('(');

        if (value is not null)
            visitor.Builder!.Append(visitor.VisitToString(value)).Append(", ");

        AppendPredicate(visitor, filterExpression);
        visitor.Builder!.Append(')');
    }

    /// <summary>
    /// Renders just the filter predicate, without any wrapper. Used by the ClickHouse-style
    /// combinators (<c>countIf(...)</c>, <c>sumIf(value, ...)</c>).
    /// </summary>
    internal static void AppendPredicate(BaseExpressionVisitor visitor, Expression filterExpression)
    {
        var lambda = (LambdaExpression)((UnaryExpression)filterExpression).Operand;

        using var filterVisitor = new WhereExpressionVisitor(visitor.Options with { DontNeedAlias = false });

        filterVisitor.VisitCondition(lambda);

        if (!visitor.IsParamMode)
            filterVisitor.WriteTo(visitor.Builder!);
    }

    /// <summary>
    /// Renders the filter predicate to a string using the surrounding visitor state, for a dialect that
    /// composes the predicate into a larger expression itself (for example a
    /// <c>groupArrayIf(value, predicate)</c> nested inside a string aggregate). In parameter mode the
    /// predicate is only walked (so its parameters are collected) and an empty string is returned.
    /// </summary>
    internal static string RenderPredicate(BaseExpressionVisitor visitor, Expression filterExpression)
    {
        var lambda = (LambdaExpression)((UnaryExpression)filterExpression).Operand;

        using var filterVisitor = new WhereExpressionVisitor(visitor.Options with { DontNeedAlias = false });

        filterVisitor.VisitCondition(lambda);

        return visitor.IsParamMode ? string.Empty : filterVisitor.ToString();
    }

    /// <summary>
    /// Throws when a filter is present but the provider cannot render one
    /// (<see cref="ISqlDialect.AggregateFilterStyle"/> is <see cref="AggregateFilterStyle.None"/>).
    /// </summary>
    internal static void RequireSupport(BaseExpressionVisitor visitor, Expression? filter)
    {
        if (filter is not null && visitor.Dialect.AggregateFilterStyle == AggregateFilterStyle.None)
            throw new NotSupportedException("The FILTER clause is not supported by this provider.");
    }
}
