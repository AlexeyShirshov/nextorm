#define PARAM_CONDITION
namespace NextORM.Core;

/// <summary>
/// Compiled query over an in-memory entity set.
/// </summary>
public sealed class InMemoryCompiledQuery<TResult, TEntity> : PreparedQueryCommand<TResult, TEntity>
{
#if PARAM_CONDITION
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

    public InMemoryCompiledQuery(Func<Func<TEntity, TResult>> func, Func<TEntity, object[]?, bool>? condition,
        Func<object[]?, Func<TEntity, bool>>? conditionFactory = null, Func<TEntity, bool>? conditionDirect = null)
        : base(func)
    {
        Condition = condition;
        ConditionFactory = conditionFactory;
        ConditionDirect = conditionDirect;
    }
}
