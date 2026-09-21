using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Query-preparation axis of the in-memory provider: builds the compiled query, the enumerator
/// factory and the enumerator itself.
/// <para>
/// The reflection-addressed entry points stay as thin wrappers on <see cref="InMemoryDataContext"/>
/// (identical signatures and visibility) and delegate here, so
/// <c>typeof(InMemoryDataContext).GetMethod(nameof(...), NonPublic | Instance)</c> and
/// <c>Expression.Call(@this, mi...)</c> keep resolving unchanged. Compiled delegates are cached in
/// the caller-owned per-instance dictionaries (<see cref="InMemoryDataContext.ExpressionsCache"/> etc.),
/// so caching stays per-instance and the embedded <c>Expression.Constant(context)</c> still calls
/// back into the context the delegate was compiled for. Bodies are moved verbatim from
/// <see cref="InMemoryDataContext"/> (F13 follow-up).
/// </para>
/// </summary>
internal static class InMemoryQueryBuilder
{
    public static IPreparedQueryCommand<TResult> GetPreparedQueryCommand<TResult>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, bool createEnumerator, bool storeInCache, CancellationToken cancellationToken)
    {
        QueryPlan? queryPlan = null;
        IPreparedQueryCommand<TResult>? planCache = null;

        if (!queryCommand.IsPrepared) queryCommand.PrepareCommand(false, cancellationToken);
        else queryCommand.RefreshInValuesShape();

        // Raw SQL (WithSql / PrepareFromSql) cannot be rendered by the in-memory provider, which
        // evaluates the expression tree instead. Reject it explicitly rather than silently running the
        // generated shape and ignoring the supplied statement.
        if (queryCommand.CustomData is RawSqlOverride)
            throw new NotSupportedException(
                "Raw SQL (WithSql/PrepareFromSql) is not supported by the in-memory provider; run the statement against a SQL provider.");

        // Correlated subqueries need a per-row execution of the inner query with the outer row's
        // values bound. The in-memory enumerator has no such binding, and the SQL-shaped prepared
        // condition (an <IQueryRegistry> lambda) is not a TEntity predicate, so the condition was
        // silently dropped and every row passed. Reject it explicitly instead of returning wrong data.
        if (queryCommand.OuterReferences is { Count: > 0 })
            throw new NotSupportedException(
                "Correlated subqueries are not supported by the in-memory provider; run the query against a SQL provider.");

        // Rejected here, before the row materializer is compiled: the projection of a bound ARRAY JOIN
        // references the ArrayJoinProjection parameter, which the in-memory materializer cannot bind.
        if (queryCommand.ArrayJoinExpressions is { Count: > 0 })
            throw new NotSupportedException("The ARRAY JOIN clause is not supported by the in-memory provider.");

        if (queryCommand.Cache && storeInCache)
        {
            queryPlan = new QueryPlan(queryCommand, null);
            if (context.CommandIndex.TryGetValue(queryPlan, out var planCache2)) planCache = planCache2 as IPreparedQueryCommand<TResult>;
        }

        if (planCache is null)
        {
            var @this = Expression.Constant(context);
            var param = Expression.Parameter(typeof(QueryCommand<TResult>));
            var callExp = Expression.Call(@this, InMemoryDataContext.miCreateCompiledQuery.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!),
                param
            );

            var key = new ExpressionKey(callExp, queryCommand);
            Func<QueryCommand<TResult>, object> createCompiledQueryDelegate;
            if (!context.ExpressionsCache.TryGetValue(key, out var del))
            {
                var body = Expression.Convert(callExp, typeof(object));
                createCompiledQueryDelegate = Expression.Lambda<Func<QueryCommand<TResult>, object>>(body, param).Compile();
                context.ExpressionsCache[key] = createCompiledQueryDelegate;
            }
            else
                createCompiledQueryDelegate = (Func<QueryCommand<TResult>, object>)del;

            var ce = new InMemoryPreparedQueryCommand<TResult>(createCompiledQueryDelegate(queryCommand), BuildCreateEnumeratorDelegate(context, queryCommand, cancellationToken), queryCommand);
            ce.Enumerator = ce.CreateEnumerator(queryCommand, ce, null, cancellationToken)!;

            planCache = ce;

            if (storeInCache && queryCommand.Cache)
                context.CommandIndex[queryPlan!.GetCacheVersion()] = planCache;

        }

        return planCache!;
    }

    private static InMemoryPreparedQueryCommand<TResult> GetCacheEntry<TResult>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        return (InMemoryPreparedQueryCommand<TResult>)GetPreparedQueryCommand(context, queryCommand, false, true, cancellationToken);
    }

    public static CreateEnumeratorDelegate<TResult> BuildCreateEnumeratorDelegate<TResult>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, CancellationToken cancellationToken)
    {
        if (queryCommand.LimitBy is not null)
            throw new NotSupportedException("The LIMIT BY clause is not supported by the in-memory provider.");

        if (queryCommand.DistinctOn is not null)
            throw new NotSupportedException("The DISTINCT ON clause is not supported by the in-memory provider.");

        if (queryCommand.TableSample is not null)
            throw new NotSupportedException("The TABLESAMPLE modifier is not supported by the in-memory provider.");

        if (queryCommand.Temporal is not null)
            throw new NotSupportedException("The FOR SYSTEM_TIME clause is not supported by the in-memory provider.");

        if (queryCommand.RowLock is not null)
            throw new NotSupportedException("The FOR UPDATE/FOR SHARE clause is not supported by the in-memory provider.");

        if (queryCommand.Paging.HasWithTies)
            throw new NotSupportedException("The WITH TIES page modifier is not supported by the in-memory provider.");

        if (queryCommand.Final || queryCommand.SampleRatio is not null || queryCommand.Settings is { Count: > 0 })
            throw new NotSupportedException("The FINAL/SAMPLE/SETTINGS query modifiers are not supported by the in-memory provider.");

        if (queryCommand.PreWhere is not null)
            throw new NotSupportedException("The PREWHERE clause is not supported by the in-memory provider.");

        if (queryCommand.ArrayJoinExpressions is { Count: > 0 })
            throw new NotSupportedException("The ARRAY JOIN clause is not supported by the in-memory provider.");

        if (queryCommand.From?.TableFunction is not null)
            throw new NotSupportedException("Table-valued function sources are not supported by the in-memory provider.");

        if (queryCommand.From?.Pivot is not null)
            throw new NotSupportedException("The PIVOT/UNPIVOT source construct is not supported by the in-memory provider.");

        if (queryCommand.From?.LinqSource is { } linqSource)
            return InMemoryLinqSource.BuildLinqSourceDelegate<TResult>(linqSource, context, InMemoryDataContext.miApplySelectMany, InMemoryDataContext.miApplyGroupJoin, InMemoryDataContext.miCreateEnumeratorAdapter);

        if (queryCommand.From?.SubQuery is not null)
        {
            if (!string.IsNullOrEmpty(queryCommand.From.Table))
                throw new DataContextException("Cannot use table as source for in-memory provider");

            var subQuery = queryCommand.From.SubQuery;
            var subQueryType = subQuery.GetType();
            if (!subQueryType.IsGenericType)
                throw new DataContextException("Cannot use table as source for in-memory provider");

            var resultType = subQueryType.GenericTypeArguments[0];

            //var delPayload = subQuery.GetOrAddPayload(() =>
            //{

            var @this = Expression.Constant(context);
            var p1 = Expression.Parameter(typeof(QueryCommand<TResult>));
            var p2 = Expression.Parameter(typeof(InMemoryPreparedQueryCommand<TResult>));
            var p3 = Expression.Parameter(typeof(CancellationToken));
            var p4 = Expression.Parameter(typeof(object[]));
            var callCreateEnumerator = Expression.Call(@this, InMemoryDataContext.miCreateAsyncEnumerator.MakeGenericMethod(resultType),
                Expression.Convert(Expression.Property(p1, nameof(QueryCommand.FromQuery)), subQueryType), p4, p3
            );


            var callCreateEnumeratorAdapter = Expression.Call(@this, InMemoryDataContext.miCreateEnumeratorAdapter.MakeGenericMethod(typeof(TResult), resultType),
                p1, p2, callCreateEnumerator
            );

            var del = Expression.Lambda<CreateEnumeratorDelegate<TResult>>(callCreateEnumeratorAdapter, p1, p2, p4, p3).Compile();
            //var delPayload = new CreateMainEnumeratorPayload();
            // });

            return del;
        }
        else
        {
            // var delPayload = queryCommand.GetOrAddPayload(() =>
            // {

            var @this = Expression.Constant(context);
            var p1 = Expression.Parameter(typeof(QueryCommand<TResult>));
            var p2 = Expression.Parameter(typeof(InMemoryPreparedQueryCommand<TResult>));
            var p3 = Expression.Parameter(typeof(CancellationToken));
            var p4 = Expression.Parameter(typeof(object[]));
            var callExp = Expression.Call(@this, InMemoryDataContext.miCreateEnumerator.MakeGenericMethod(typeof(TResult), queryCommand.EntityType!),
                p1, p2, p4, p3
            );

            var key = new ExpressionKey(callExp, queryCommand);
            CreateEnumeratorDelegate<TResult> factory;
            if (!context.ExpressionsCache.TryGetValue(key, out var d))
            {
                factory = Expression.Lambda<CreateEnumeratorDelegate<TResult>>(callExp, p1, p2, p4, p3).Compile();
                context.ExpressionsCache[key] = factory;
            }
            else
                factory = (CreateEnumeratorDelegate<TResult>)d;

            return factory;
        }
    }

    public static IAsyncEnumerator<TResult> CreateAsyncEnumerator<TResult>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, object[]? @params, CancellationToken cancellationToken)
    {
        var cacheEntry = GetCacheEntry(context, queryCommand, cancellationToken);
        return cacheEntry.CreateEnumerator(cacheEntry.QueryCommand, cacheEntry, @params, cancellationToken)!;
    }

    public static IAsyncEnumerator<TResult> CreateEnumerator<TResult, TEntity>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, object[] @params, CancellationToken cancellationToken)
    {
        if (queryCommand.UnionQuery is not null)
            return InMemorySetOperations.CreateSetOperationEnumerator(context, queryCommand, @params, cancellationToken);

        if (cacheEntry.Resolver is not null)
            return cacheEntry.Resolver(@params);

        object? v = null;

        IEnumerable<TEntity>? data = cacheEntry.Data as IEnumerable<TEntity>;
        if (data is not null)
            goto next;

        if (cacheEntry.Data is not IAsyncEnumerable<TEntity> asyncData)
        {
            if (!context.Data.TryGetValue(typeof(TEntity), out v) || v is not IAsyncEnumerable<TEntity> av)
                goto next;

            asyncData = av;
        }

        cacheEntry.Data = asyncData;
        if (InMemoryGrouping.TryGetAggregate(queryCommand, out var asyncAggregateName, out _, out _))
            throw new NotSupportedException($"Aggregate '{asyncAggregateName}' over an async source is not supported by the in-memory provider.");
        if (queryCommand.GroupBy is not null)
            throw new NotSupportedException("GroupBy over an async source is not supported by the in-memory provider.");
        if (queryCommand.Sorting is not null)
        {
            // An async source cannot be sorted lazily without buffering; wrap it in an iterator that
            // materialises once and then serves the ordered rows. The raw source is cached above so a
            // repeat call does not wrap an already-wrapped sequence.
            var ordered = InMemoryOrdering.OrderAsyncEnumerable(asyncData, queryCommand, cancellationToken, context.SortingSelectorCache);
            return CreateEnumeratorAdapter(context, queryCommand, cacheEntry, ordered.GetAsyncEnumerator(cancellationToken));
        }
        return CreateEnumeratorAdapter(context, queryCommand, cacheEntry, asyncData.GetAsyncEnumerator(cancellationToken));

    next:
        {

            if (queryCommand.Joins?.Length > 0 && typeof(TEntity).IsAssignableTo(typeof(IProjection)))
            {
                var dim = 2;
                object? joinResult = null;
                Type? firstType = null;
                for (var idx = 0; idx < queryCommand.Joins.Length; idx++)
                {
                    var join = queryCommand.Joins[idx];

                    firstType ??= join.JoinCondition?.Parameters[0].Type ?? typeof(TEntity).GetGenericArguments()[0];
                    var secondType = join.JoinCondition?.Parameters[1].Type ?? join.EntityType
                        ?? throw new NotSupportedException($"A {join.JoinType} join requires a join condition or an entity type");

                    var prjType = InMemoryProjectionFactory.CreateProjectionType(firstType, secondType, dim);

                    var @this = Expression.Constant(context);
                    var p1 = Expression.Parameter(typeof(QueryCommand));
                    var p2 = Expression.Parameter(typeof(object));
                    var p3 = Expression.Parameter(typeof(JoinExpression));
                    var p4 = Expression.Parameter(typeof(int));
                    var callExp = Expression.Call(@this, InMemoryDataContext.miLoopJoin.MakeGenericMethod(firstType, secondType, prjType),
                        p1,
                        Expression.Convert(p2, typeof(IEnumerable<>).MakeGenericType(firstType)),
                        p3,
                        p4
                    );
                    var key = new ExpressionKey(callExp, queryCommand);
                    if (!context.ExpressionsCache.TryGetValue(key, out var del))
                    {
                        var d = Expression.Lambda<Func<QueryCommand, object?, JoinExpression, int, object>>(callExp,
                            p1,
                            p2,
                            p3,
                            p4
                        ).Compile();
                        context.ExpressionsCache[key] = d;
                        del = d;
                    }

                    joinResult = ((Func<QueryCommand, object?, JoinExpression, int, object>)del)(queryCommand, joinResult, join, dim);

                    firstType = prjType;

                    dim++;
                }

                data = (IEnumerable<TEntity>)joinResult!;
            }
            else if (data is null)
            {
                if (v is not IEnumerable<TEntity> ev)
                    ev = Array.Empty<TEntity>();

                data = ev;
            }

            if (queryCommand.GroupBy is not null)
                return InMemoryGrouping.CreateGroupedEnumerator<TResult, TEntity>(context, queryCommand, cacheEntry, data!, @params);

            if (queryCommand.Sorting is not null)
            {
                data = InMemoryOrdering.ApplyOrdering(data, queryCommand, context.SortingSelectorCache);
            }
            cacheEntry.Data = data;

            if (InMemoryGrouping.TryGetAggregate(queryCommand, out var aggregateName, out var aggregateParam, out var aggregateBody))
            {
                if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> aggregateCompiled)
                {
                    aggregateCompiled = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(context, queryCommand);
                    cacheEntry.CompiledQuery = aggregateCompiled;
                }

                // Aggregates must see the same filtered rows as the mapped path: apply WHERE before
                // folding, otherwise Count/Sum/... would include rows the query excludes.
                var aggregatePredicate = aggregateCompiled.ConditionDirect;
                if (aggregateCompiled.ConditionFactory is not null && @params is not null)
                    aggregatePredicate = aggregateCompiled.ConditionFactory(@params);

                var aggregateSource = aggregatePredicate is null ? data! : data!.Where(aggregatePredicate);

                var aggregateValueType = aggregateBody?.Type ?? typeof(object);
                var aggregateSelector = InMemoryGrouping.GetAggregateSelector<TEntity>(aggregateBody, aggregateParam, aggregateValueType, queryCommand, context.AggregateSelectorCache);
                var aggregateValue = InMemoryAggregates.Compute(aggregateSource, aggregateName!, aggregateSelector, new AggregateTypeInfo(typeof(TEntity), typeof(TResult), aggregateValueType));

                return new InMemoryScalarEnumerator<TResult>((TResult)aggregateValue!);
            }

            if (cacheEntry.Enumerator is not InMemoryEnumerator<TResult, TEntity> enumerator)
            {
                if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiledQuery)
                {
                    compiledQuery = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(context, queryCommand);
                    cacheEntry.CompiledQuery = compiledQuery;
                }

                enumerator = new InMemoryEnumerator<TResult, TEntity>(compiledQuery, cancellationToken, queryCommand.IsDistinct);
                enumerator.Init(data!, @params);
                cacheEntry.Enumerator = enumerator;
            }
            else
            {
                enumerator.Init(data!, @params);
            }

            // The compiled plan does not change between calls, so cache a resolver that only
            // re-initialises the enumerator with new parameter values.
            var resolvedEnumerator = enumerator;
            var resolvedData = data!;
            cacheEntry.Resolver = p => { resolvedEnumerator.Init(resolvedData, p); return resolvedEnumerator; };

            return enumerator;
        }
    }

    public static IAsyncEnumerator<TResult> CreateEnumeratorAdapter<TResult, TEntity>(InMemoryDataContext context, QueryCommand<TResult> queryCommand, InMemoryPreparedQueryCommand<TResult> cacheEntry, IAsyncEnumerator<TEntity> enumerator)
    {
        if (queryCommand.Joins?.Length > 0 && typeof(TEntity).IsAssignableTo(typeof(IProjection)))
        {
            throw new NotImplementedException("joins");
        }

        if (cacheEntry.CompiledQuery is not InMemoryCompiledQuery<TResult, TEntity> compiledQuery)
        {
            compiledQuery = (InMemoryCompiledQuery<TResult, TEntity>)CreateCompiledQuery<TResult, TEntity>(context, queryCommand);
            cacheEntry.CompiledQuery = compiledQuery;
        }

        // An aggregate over a subquery (for example ctx.From(cmd).Count()) is not a per-row map: fold
        // the buffered subquery rows instead. Only a buffered subquery can be folded synchronously.
        if (InMemoryGrouping.TryGetAggregate(queryCommand, out var aggregateName, out var aggregateParam, out var aggregateBody))
        {
            if (enumerator is not IEnumerator<TEntity> sync)
                throw new NotSupportedException($"Aggregate '{aggregateName}' over an async subquery is not supported by the in-memory provider.");

            var rows = new List<TEntity>();
            try
            {
                while (sync.MoveNext()) rows.Add(sync.Current);
            }
            finally
            {
                (sync as IDisposable)?.Dispose();
            }

            if (compiledQuery.ConditionDirect is not null)
                rows = rows.Where(compiledQuery.ConditionDirect).ToList();

            var aggregateValueType = aggregateBody?.Type ?? typeof(object);
            var aggregateSelector = InMemoryGrouping.GetAggregateSelector<TEntity>(aggregateBody, aggregateParam, aggregateValueType, queryCommand, context.AggregateSelectorCache);
            var aggregateValue = InMemoryAggregates.Compute(rows, aggregateName!, aggregateSelector, new AggregateTypeInfo(typeof(TEntity), typeof(TResult), aggregateValueType));

            return new InMemoryScalarEnumerator<TResult>((TResult)aggregateValue!);
        }

        return new InMemoryEnumeratorAdapter<TResult, TEntity>(compiledQuery, enumerator, queryCommand.IsDistinct);
    }

    public static PreparedQueryCommand<TResult, TEntity> CreateCompiledQuery<TResult, TEntity>(InMemoryDataContext context, QueryCommand<TResult> query)
    {
        Func<TEntity, object[]?, bool>? conditionDelegate = null;
        Func<object[]?, Func<TEntity, bool>>? conditionFactory = null;
        Func<TEntity, bool>? conditionDirect = null;

        if (query.PreparedCondition is Expression<Func<TEntity, bool>> condition)
        {
            var key = new ExpressionKey(condition, query);
            if (!context.ExpressionsCache.TryGetValue(key, out var d))
            {
                var p = Expression.Parameter(typeof(object[]));
                var replaceParam = new ParameterBinderVisitor(p);
                var lambda = (LambdaExpression)replaceParam.Visit(condition)!;

                var @params = new List<ParameterExpression>(lambda.Parameters) { p };
                conditionDelegate = Expression.Lambda<Func<TEntity, object[]?, bool>>(lambda.Body, @params).Compile();
                context.ExpressionsCache[key] = conditionDelegate;
            }
            else
                conditionDelegate = (Func<TEntity, object[]?, bool>?)d;

            (conditionFactory, conditionDirect) = InMemoryConditionFactory.GetConditionPredicates(query, condition, context.ConditionFactoryCache, context.ConditionDirectCache);
        }
        return new InMemoryCompiledQuery<TResult, TEntity>(context.GetMap<TResult, TEntity>(query), conditionDelegate, conditionFactory, conditionDirect);
    }
}
