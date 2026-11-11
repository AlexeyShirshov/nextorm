#define PARAM_CONDITION
namespace NextORM.Core;

/// <summary>
/// Compiled query over an in-memory entity set.
/// </summary>
public sealed class InMemoryCompiledQuery<TResult, TEntity> : PreparedQueryCommand<TResult, TEntity>
{
#if PARAM_CONDITION
    /// <summary>
    /// Predicate evaluated per entity, receiving the current parameter values; <see langword="null"/>
    /// when the query is unconditional.
    /// </summary>
    public readonly Func<TEntity, object[]?, bool>? Condition;
#else
    public readonly Func<TEntity, bool>? Condition;
#endif

    /// <summary>
    /// Builds a strongly typed predicate from the current parameter values (resolved once per query).
    /// Avoids indexing/boxing <c>object[]</c> on every enumerated row.
    /// </summary>
    public readonly Func<object[]?, Func<TEntity, bool>>? ConditionFactory;

    /// <summary>Strongly typed predicate for parameterless conditions.</summary>
    public readonly Func<TEntity, bool>? ConditionDirect;

    /// <summary>
    /// Creates a compiled in-memory query from lazily resolved projection and predicate factories.
    /// </summary>
    /// <param name="func">Resolves the projection applied to each matching entity.</param>
    /// <param name="condition">Predicate evaluated per entity with the current parameters, or <see langword="null"/> for an unconditional query.</param>
    /// <param name="conditionFactory">Builds a strongly typed predicate from the current parameters, taking precedence over <paramref name="condition"/> when supplied.</param>
    /// <param name="conditionDirect">Strongly typed predicate for parameterless conditions, used when no factory output is available.</param>
    public InMemoryCompiledQuery(Func<Func<TEntity, TResult>> func, Func<TEntity, object[]?, bool>? condition,
        Func<object[]?, Func<TEntity, bool>>? conditionFactory = null, Func<TEntity, bool>? conditionDirect = null)
        : base(func)
    {
        Condition = condition;
        ConditionFactory = conditionFactory;
        ConditionDirect = conditionDirect;
    }
}
