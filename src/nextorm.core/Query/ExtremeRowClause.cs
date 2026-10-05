using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Which rows survive a <c>SelectWhereMax</c>/<c>SelectWhereMin</c> query: <see cref="One"/> keeps a
/// single extreme row, <see cref="All"/> keeps every row tied on the extreme value.
/// </summary>
public enum ExtremeRowTies
{
    /// <summary>Keep a single row per group (the provider picks one when values tie).</summary>
    One,
    /// <summary>Keep every row tied on the extreme value.</summary>
    All,
}

/// <summary>Whether an <see cref="ExtremeRowClause"/> keeps the maximum or the minimum value.</summary>
internal enum ExtremeKind
{
    /// <summary>Keep the row(s) with the maximum value.</summary>
    Max,
    /// <summary>Keep the row(s) with the minimum value.</summary>
    Min,
}

/// <summary>
/// The <c>SelectWhereMax</c>/<c>SelectWhereMin</c> request attached to a query through
/// <see cref="EntityBuilder{TEntity}.SelectWhereMax{TValue}(Expression{Func{TEntity, TValue}}, ExtremeRowTies, Expression{Func{TEntity, object?}}?)"/>:
/// keeps the row(s) with the extreme value of <see cref="ValueSelector"/>, optionally per
/// <see cref="GroupBy"/> group. Carried from the builder to the command and rendered by the dialect;
/// <see cref="Projection"/> is set by the projection-typed overloads and otherwise <c>null</c>.
/// </summary>
internal sealed record ExtremeRowClause(
    ExtremeKind Kind,
    LambdaExpression ValueSelector,
    ExtremeRowTies Ties,
    LambdaExpression? GroupBy,
    LambdaExpression? Projection);
