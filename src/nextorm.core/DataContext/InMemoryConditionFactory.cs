using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Condition axis of the in-memory provider: turns a prepared entity predicate into a strongly typed
/// delegate (or a factory that builds one from the current parameters), so the enumerator does not
/// index/box an <c>object[]</c> on every row.
/// <para>
/// The compiled delegates are cached in caller-owned dictionaries (the context's per-instance
/// <c>_conditionFactoryCache</c>/<c>_conditionDirectCache</c>), so this type is stateless and shares
/// nothing process-wide. Bodies are moved verbatim from <see cref="InMemoryDataContext"/> (F13
/// follow-up).
/// </para>
/// </summary>
internal static class InMemoryConditionFactory
{
    /// <summary>
    /// Returns a strongly typed predicate (or a factory that builds one from the current parameters)
    /// so the enumerator does not index/box an <c>object[]</c> on every row.
    /// </summary>
    public static (Func<object[]?, Func<TEntity, bool>>? Factory, Func<TEntity, bool>? Direct) GetConditionPredicates<TResult, TEntity>(
        InMemoryDataContext context,
        QueryCommand<TResult> query,
        Expression<Func<TEntity, bool>> condition,
        IDictionary<ExpressionKey, Delegate> conditionFactoryCache,
        IDictionary<ExpressionKey, Delegate> conditionDirectCache)
    {
        if (InMemoryCorrelatedSubqueryRewriter.IsNeeded(query))
            condition = (Expression<Func<TEntity, bool>>)new InMemoryCorrelatedSubqueryRewriter(context, query).Rewrite(condition);

        condition = (Expression<Func<TEntity, bool>>)InMemoryStringFunctionRewriter.Rewrite(condition);

        var key = new ExpressionKey(condition, query);
        if (conditionFactoryCache.TryGetValue(key, out var f))
            return ((Func<object[]?, Func<TEntity, bool>>)f, null);
        if (conditionDirectCache.TryGetValue(key, out var d))
            return (null, (Func<TEntity, bool>)d);

        var collector = new ParamCollectorVisitor();
        collector.Visit(condition.Body);
        var ps = collector.Parameters;

        if (ps.Count == 0)
        {
            var direct = condition.Compile();
            conditionDirectCache[key] = direct;
            return (null, direct);
        }

        var factory = BuildConditionFactory(condition, ps);
        conditionFactoryCache[key] = factory;
        return (factory, null);
    }

    /// <summary>
    /// Compiles <c>(object[] p) =&gt; { var p0 = (T0)p[0]; ...; return (TEntity e) =&gt; &lt;body&gt;; }</c>.
    /// Parameters are unpacked once per query; the returned predicate is fully typed.
    /// </summary>
    private static Func<object[]?, Func<TEntity, bool>> BuildConditionFactory<TEntity>(Expression<Func<TEntity, bool>> condition, SortedDictionary<int, Type> ps)
    {
        var p = Expression.Parameter(typeof(object[]), "p");
        var locals = new Dictionary<int, ParameterExpression>();
        var variables = new List<ParameterExpression>();
        var body = new List<Expression>();

        foreach (var (idx, type) in ps)
        {
            var v = Expression.Variable(type, "p" + idx);
            locals[idx] = v;
            variables.Add(v);
            body.Add(Expression.Assign(v, Expression.Convert(Expression.ArrayIndex(p, Expression.Constant(idx)), type)));
        }

        var entity = condition.Parameters[0];
        var newBody = new ParamLocalSubstitutionVisitor(locals).Visit(condition.Body)!;
        body.Add(Expression.Lambda<Func<TEntity, bool>>(newBody, entity));

        return Expression.Lambda<Func<object[]?, Func<TEntity, bool>>>(Expression.Block(variables, body), p).Compile();
    }
}
