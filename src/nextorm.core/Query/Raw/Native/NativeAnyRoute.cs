namespace NextORM.Core;

/// <summary>
/// Native route for the shared <c>Any</c>/<c>AnyAsync</c> terminal (I7/M2).
/// </summary>
/// <remarks>
/// <para>
/// The legacy terminal builds a fresh parent command, prepares it, swaps it into the shared EXISTS
/// wrapper, rebuilds the normalized plan key and re-renders the statement to refresh the captured
/// value — roughly 3.8 KB per call for a predicate over one captured local. This route derives a
/// value-agnostic shape from the builder instead, finds the compiled holder the miss published, binds
/// the current closure values into its parameters and executes it. Nothing is built, prepared or
/// re-rendered on a hit.
/// </para>
/// <para>
/// Fail closed: only a plain entity source with a condition and none of the SQL-affecting modifiers is
/// eligible; a shape with an applicable global/builder filter, a captured collection, a nested command
/// or any modifier keeps the legacy route. Parameterization is bound per execution, never keyed.
/// </para>
/// </remarks>
internal static class NativeAnyRoute
{
    /// <summary>The terminal discriminator of the shape header (a plan is never shared across terminals).</summary>
    private const int TerminalAny = 1;

    /// <summary>Tries to answer a synchronous <c>Any</c> from the native index.</summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="builder">The builder the terminal was called on.</param>
    /// <param name="params">The terminal's positional arguments; a non-empty span keeps the legacy route.</param>
    /// <param name="result">The scalar result when the route handled the call.</param>
    /// <returns><see langword="true"/> when the call was answered natively.</returns>
    internal static bool TryExecute<TEntity>(EntityBuilder<TEntity> builder, ReadOnlySpan<object?> @params, out bool result)
    {
        result = false;
        if (!TryResolve(builder, @params, out var context, out var header) || header is not { } shapeHeader)
            return false;

        if (!QueryPlanStore.TryGetNative(shapeHeader, builder.Condition, out var candidate) || candidate is null)
            return false;

        if (candidate.Holder is not DbPreparedQueryCommand<bool> prepared)
            return false;

        if (!ShapeIdentity.TryBind(candidate.Shape, shapeHeader, candidate.Binder, prepared.DbCommandParams, builder.Condition))
            return false;

        result = context!.ExecuteScalar<bool>(prepared, @params, true);
        return true;
    }

    /// <summary>Tries to answer an asynchronous <c>AnyAsync</c> from the native index.</summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="builder">The builder the terminal was called on.</param>
    /// <param name="cancellationToken">Cancels the execution.</param>
    /// <param name="params">The terminal's positional arguments; a non-empty array keeps the legacy route.</param>
    /// <param name="result">The pending scalar when the route handled the call.</param>
    /// <returns><see langword="true"/> when the call was answered natively.</returns>
    internal static bool TryExecuteAsync<TEntity>(
        EntityBuilder<TEntity> builder,
        CancellationToken cancellationToken,
        object[] @params,
        out Task<bool> result)
    {
        result = Task.FromResult(false);
        if (!TryResolve(builder, @params, out var context, out var header) || header is not { } shapeHeader)
            return false;

        if (!QueryPlanStore.TryGetNative(shapeHeader, builder.Condition, out var candidate) || candidate is null)
            return false;

        if (candidate.Holder is not DbPreparedQueryCommand<bool> prepared)
            return false;

        if (!ShapeIdentity.TryBind(candidate.Shape, shapeHeader, candidate.Binder, prepared.DbCommandParams, builder.Condition))
            return false;

        result = context!.ExecuteScalar<bool>(prepared, @params, true, cancellationToken);
        return true;
    }

    /// <summary>
    /// Publishes the compiled holder of a just-executed miss under the builder's value-agnostic shape.
    /// A no-op for an ineligible builder, a holder that is not a boolean scalar or a parameter map that
    /// is not a provable bijection.
    /// </summary>
    /// <typeparam name="TEntity">The queried entity type.</typeparam>
    /// <param name="builder">The builder the terminal was called on.</param>
    /// <param name="prepared">The holder the legacy route compiled.</param>
    internal static void Publish<TEntity>(EntityBuilder<TEntity> builder, IPreparedQueryCommand<bool> prepared)
    {
        if (prepared is not DbPreparedQueryCommand<bool> holder)
            return;

        if (!TryResolve(builder, ReadOnlySpan<object?>.Empty, out _, out var header) || header is not { } shapeHeader)
            return;

        var shape = ShapeIdentity.TryCreate(shapeHeader, builder.Condition);
        if (shape is null)
            return;

        var binder = NativeBinder.TryCreate(shape, holder.DbCommandParams);
        if (binder is null)
            return;

        QueryPlanStore.SetNative(shape, holder, binder);
    }

    /// <summary>
    /// Whether the builder is a provable plain entity source the native route may key, and the header
    /// identifying it.
    /// </summary>
    private static bool TryResolve<TEntity>(
        EntityBuilder<TEntity> builder,
        ReadOnlySpan<object?> @params,
        out DataContext? context,
        out ShapeHeader? header)
    {
        context = null;
        header = null;

        // Explicit positional arguments are bound by the legacy path (they are not captures).
        if (!@params.IsEmpty)
            return false;

        if (builder.DataProvider is not DataContext sqlContext || !sqlContext.QueryCacheEnabled)
            return false;

        // The context-shared Any/Count/All command is part of the observable contract (it is
        // `DataContext.AnyCommand`, and its identity/state is asserted by tests and reused by the other
        // terminals). A native hit never builds the wrapper, so the *first* call on a context must take
        // the legacy route to create it; every later call is served natively.
        if (sqlContext.AnyCommand is null)
            return false;

        // Only a plain entity source with a predicate: every other SQL-affecting member keeps the
        // legacy route. The list mirrors the ones the parent command transfer carries.
        if (builder.SourceFrom is not null
            || builder.Table is not null
            || builder.Sorting is { Count: > 0 }
            || builder.Paging.Limit != 0
            || builder.Paging.Offset != 0
            || builder.Paging.HasWithTies
            || builder.Joins is { Count: > 0 }
            || builder.Ctes is { Count: > 0 }
            || builder.GroupByExpression is not null
            || builder.HavingCondition is not null
            || builder.IsDistinct
            || builder.IsFinal
            || builder.SampleRatio is not null
            || builder.LimitByClause is not null
            || builder.DistinctOnClause is not null
            || builder.ExtremeRowClause is not null
            || builder.TableSampleClause is not null
            || builder.TemporalClause is not null
            || builder.RowLockClause is not null
            || builder.SourceLockClause is not null
            || builder.ArrayJoins is { Count: > 0 }
            || builder.Windows is { Count: > 0 }
            || builder.SettingsList is { Count: > 0 }
            || builder.TableHints is { Count: > 0 }
            || builder.IndexHints is { Count: > 0 }
            || builder.TablesInScopeHints is { Count: > 0 }
            || builder.Tag is not null
            || builder.QuoteIdentifiers is not null
            || builder.NamingConvention is not null
            || builder.KeywordCase is not null
            || builder.CommandTimeout is not null
            || builder.LoadSpecs is { Count: > 0 }
            || builder.JoinIntos is { Count: > 0 })
        {
            return false;
        }

        // Metadata-only applicability probe: an injected filter changes the statement without changing
        // the raw shape, so a filtered entity must never reuse a native plan (design Q5).
        if (QueryFilterResolver.GetFilters(typeof(TEntity), builder.FilterScope, sqlContext).Count != 0)
            return false;

        context = sqlContext;
        header = new ShapeHeader(
            typeof(TEntity),
            typeof(bool),
            sqlContext.GetType(),
            TerminalAny,
            NativeContextStamp.For(sqlContext));
        return true;
    }
}
