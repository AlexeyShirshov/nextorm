using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Renders the optional <c>FILTER (WHERE ...)</c> clause of an aggregate. The filter is carried by a
/// <c>Expression&lt;Func&lt;bool&gt;&gt;</c> argument (a quoted lambda); the predicate is rendered with
/// the same visitor state as the surrounding expression, so it can reference the query's columns and
/// its parameters line up between the SQL and the parameter-extraction passes.
/// <para>
/// Only a dialect that opts in with <see cref="ISqlDialect.SupportsFilter"/> may use this clause; the
/// callers reject it with a clear message otherwise.
/// </para>
/// </summary>
internal static class AggregateFilter
{
    /// <summary>True when <paramref name="expression"/> is a filter argument (a quoted lambda).</summary>
    internal static bool IsFilterExpression(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Quote, Operand: LambdaExpression };

    /// <summary>
    /// Appends <c> filter (where &lt;predicate&gt;)</c> for the quoted <paramref name="filterExpression"/>.
    /// In parameter mode the predicate is only walked, so its parameters are collected in the same
    /// order as in the SQL pass.
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
    /// Renders just the filter predicate, without the ANSI <c>filter (where ...)</c> wrapper. Used by
    /// the ClickHouse-style combinators (<c>countIf(...)</c>, <c>sumIf(value, ...)</c>).
    /// </summary>
    internal static void AppendPredicate(BaseExpressionVisitor visitor, Expression filterExpression)
    {
        var lambda = (LambdaExpression)((UnaryExpression)filterExpression).Operand;

        using var filterVisitor = new WhereExpressionVisitor(visitor.Options with { DontNeedAlias = false });

        filterVisitor.VisitCondition(lambda);

        if (!visitor.IsParamMode)
            filterVisitor.WriteTo(visitor.Builder!);
    }
}
