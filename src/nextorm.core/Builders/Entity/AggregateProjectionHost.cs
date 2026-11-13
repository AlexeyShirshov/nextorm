using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// A builder that can expose its untyped command without a user-defined conversion operator.
/// </summary>
/// <remarks>
/// The correlated-subquery visitor has to turn a builder expression into its <see cref="QueryCommand"/>
/// while building an expression tree. <c>Expression.Convert(builder, typeof(QueryCommand))</c> resolves
/// the implicit conversion operator through reflection, so a trimmed or native image — where the
/// operator method is removed — fails with "No coercion operator is defined". Casting to this interface
/// and reading <see cref="Command"/> is a built-in reference conversion plus a statically known property
/// getter, and it produces the same command the operator did.
/// </remarks>
internal interface IQueryCommandSource
{
    /// <summary>The command built by the builder's <c>ToCommand</c>.</summary>
    QueryCommand Command { get; }
}

/// <summary>
/// A builder that can build a projection command whose result type is only known at run time.
/// </summary>
/// <remarks>
/// An aggregate terminal inside a correlated subquery (for example <c>inner.Where(...).Count()</c>) is
/// rewritten into a projection over an aggregate body whose result type is a runtime value. Resolving
/// <c>EntityBuilder&lt;T&gt;.Select&lt;TResult&gt;</c> through <see cref="System.Reflection"/> and closing it
/// with <c>MakeGenericMethod</c> needs metadata a trimmed or native image does not keep. This interface
/// exposes the same projection as a non-generic call; the command carries the type as its runtime
/// <see cref="QueryCommand.ResultType"/>.
/// </remarks>
internal interface IAggregateProjectionHost
{
    /// <summary>
    /// Builds the single-projection command for <paramref name="projection"/>, recording
    /// <paramref name="resultType"/> as the command's result type.
    /// </summary>
    /// <param name="projection">The aggregate projection lambda over the entity parameter.</param>
    /// <param name="resultType">The aggregate's result type.</param>
    /// <returns>The projection command.</returns>
    QueryCommand SelectAggregateProjection(LambdaExpression projection, Type resultType);
}
