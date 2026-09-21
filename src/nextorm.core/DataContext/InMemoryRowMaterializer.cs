using System.Linq.Expressions;
using Microsoft.Extensions.Logging;

namespace NextORM.Core;

/// <summary>
/// Row-materializer axis of the in-memory provider: builds (and caches) the <c>TEntity -&gt; TResult</c>
/// delegate for a prepared command, either by reusing a scalar projection or by composing the
/// select list through <see cref="RowMaterializerBuilder"/> and <see cref="InMemoryDataContext.MapColumn"/>.
/// <para>
/// The compiled delegate is cached in the caller-owned per-instance dictionary (the context's
/// <c>_expCache</c>), so this type is stateless and shares nothing process-wide. <see cref="InMemoryDataContext.GetMap{TResult,TEntity}"/>
/// stays on the context (public API) and delegates here. Body is moved verbatim from
/// <see cref="InMemoryDataContext"/> (F13 follow-up).
/// </para>
/// </summary>
internal static class InMemoryRowMaterializer
{
    public static Func<Func<TEntity, TResult>> GetMap<TResult, TEntity>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, IDictionary<ExpressionKey, Delegate> expCache)
    {
#if DEBUG
        if (!queryCommand.IsPrepared)
            throw new InvalidOperationException("Command not prepared");
#endif
        // var key = new ExpressionKey(_exp);
        // if (!(_dataProvider as SqlDataProvider).MapCache.TryGetValue(key, out var del))
        // {
        //     if (Logger?.IsEnabled(LogLevel.Information) ?? false) Logger.LogInformation("Map delegate cache miss for: {exp}", _exp);
        var resultType = typeof(TResult);

        // An entity-sourced command (for example ctx.From(scalarSubquery) or From<Entity>()) materialises
        // the source rows as-is: the in-memory data is already TResult, so no row materializer is needed.
        // Without this, scalar sources (int, ...) would fail in RowMaterializerBuilder, which expects a
        // constructor.
        if (resultType == typeof(TEntity))
            return static () => static (TEntity e) => (TResult)(object)e!;

        return () =>
        {
            Expression<Func<TEntity, TResult>> lambda;
            if (queryCommand.OneColumn)
            {
                var corVisitor = new CorrelatedQueryExpressionVisitor(context, queryCommand, typeof(TEntity), context.Logger);
                var newExp = corVisitor.Visit(queryCommand.SelectList![0].Expression);
                lambda = (Expression<Func<TEntity, TResult>>)newExp!;
            }
            else
            {
                var param = Expression.Parameter(typeof(TEntity));

                var body = RowMaterializerBuilder.Build(
                    resultType,
                    param,
                    queryCommand.SelectList!,
                    queryCommand.IgnoreColumns,
                    column => context.MapColumn(column, param));

                lambda = Expression.Lambda<Func<TEntity, TResult>>(body, param);
            }

            if (context.Logger?.IsEnabled(LogLevel.Debug) ?? false) context.Logger.LogDebug("Get instance of {type} as: {exp}", resultType, lambda);
            var key = new ExpressionKey(lambda, queryCommand);
            if (!expCache.TryGetValue(key, out var d))
            {
                d = lambda.Compile();
                expCache[key] = d;
            }

            return (Func<TEntity, TResult>)d;
        };

        //         (_dataProvider as SqlDataProvider).MapCache[key] = del;
        //     }
    }
}
