using System.Linq.Expressions;
using NextORM.Core;

namespace NextORM.ClickHouse;

/// <summary>
/// ClickHouse-only fluent members of <c>EntityBuilder&lt;TEntity&gt;</c>, removed from the common
/// <c>nextorm</c> package and re-exposed here as extension methods. Add
/// <c>using NextORM.ClickHouse;</c> to keep the fluent chain unchanged on a ClickHouse provider.
/// The render-time dialect gating (and the thrown exceptions) is unchanged.
/// </summary>
public static class ClickHouseEntityBuilderExtensions
{
    /// <summary>ClickHouse <c>FINAL</c> modifier on the primary table.</summary>
    public static EntityBuilder<TEntity> Final<TEntity>(this EntityBuilder<TEntity> builder)
    {
        var b = builder.Clone();
        b.IsFinal = true;
        return b;
    }

    /// <summary>Trailing ClickHouse <c>SETTINGS key = value</c> clause.</summary>
    public static EntityBuilder<TEntity> Settings<TEntity>(this EntityBuilder<TEntity> builder, params (string Key, string Value)[] settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        var b = builder.Clone();
        var list = b.SettingsListBacking ??= [];

        foreach (var (key, value) in settings)
        {
            if (string.IsNullOrWhiteSpace(key))
                throw new ArgumentException("A SETTINGS key must not be empty.", nameof(settings));

            list.Add(new KeyValuePair<string, string>(key, value));
        }

        return b;
    }

    /// <summary>ClickHouse <c>PREWHERE</c> predicate.</summary>
    public static EntityBuilder<TEntity> PreWhere<TEntity>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);

        var b = builder.Clone();

        if (builder.PreWhereCondition is { } previous)
        {
            var replVisitor = new ReplaceParameterExpressionVisitor(previous.Parameters[0]);
            var newBody = Expression.AndAlso(previous.Body, replVisitor.Visit(condition.Body));
            b.PreWhereCondition = Expression.Lambda<Func<TEntity, bool>>(newBody, previous.Parameters[0]);
        }
        else
            b.PreWhereCondition = condition;

        return b;
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over an array expression.</summary>
    public static EntityBuilder<TEntity> ArrayJoin<TEntity, TArray>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TArray>> array)
        => AddArrayJoin(builder, array, ArrayJoinKind.Inner);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over an array expression.</summary>
    public static EntityBuilder<TEntity> LeftArrayJoin<TEntity, TArray>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TArray>> array)
        => AddArrayJoin(builder, array, ArrayJoinKind.Left);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> exposing the expanded element as <c>p.Element</c>.</summary>
    public static EntityBuilder<ArrayJoinProjection<TEntity, TElement>> ArrayJoinElement<TEntity, TElement>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, IEnumerable<TElement>>> array)
        => ToArrayJoinElement(builder, array, ArrayJoinKind.Inner);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> exposing the expanded element as <c>p.Element</c>.</summary>
    public static EntityBuilder<ArrayJoinProjection<TEntity, TElement>> LeftArrayJoinElement<TEntity, TElement>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, IEnumerable<TElement>>> array)
        => ToArrayJoinElement(builder, array, ArrayJoinKind.Left);

    /// <summary>ClickHouse <c>LIMIT n BY expr</c> clause.</summary>
    public static EntityBuilder<TEntity> LimitBy<TEntity, TResult>(this EntityBuilder<TEntity> builder, int limit, Expression<Func<TEntity, TResult>> exp)
    {
        ArgumentNullException.ThrowIfNull(exp);

        var b = builder.Clone();
        b.LimitByClause = new LimitByClause(exp, limit, 0);
        return b;
    }

    /// <summary>ClickHouse <c>LIMIT offset, n BY expr</c> clause.</summary>
    public static EntityBuilder<TEntity> LimitBy<TEntity, TResult>(this EntityBuilder<TEntity> builder, int limit, int offset, Expression<Func<TEntity, TResult>> exp)
    {
        ArgumentNullException.ThrowIfNull(exp);

        var b = builder.Clone();
        b.LimitByClause = new LimitByClause(exp, limit, offset);
        return b;
    }

    /// <summary>ClickHouse <c>WITH TOTALS</c> grouping modifier.</summary>
    public static EntityBuilder<TEntity> WithTotals<TEntity>(this EntityBuilder<TEntity> builder)
    {
        var b = builder.Clone();
        b.GroupByWithTotals = true;
        return b;
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static EntityBuilder<TEntity> SemiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, EntityBuilder<TJoinEntity> join, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(builder, builder.GetJoinSource(join), joinCondition, JoinType.Semi, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static EntityBuilder<TEntity> AntiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, EntityBuilder<TJoinEntity> join, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(builder, builder.GetJoinSource(join), joinCondition, JoinType.Anti, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, EntityBuilder<TJoinEntity> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c> over a subquery.</summary>
    public static EntityBuilder<TEntity> SemiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(builder, new FromExpression(query), joinCondition, JoinType.Semi, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c> over a subquery.</summary>
    public static EntityBuilder<TEntity> AntiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
        => AddSemiAntiJoin(builder, new FromExpression(query), joinCondition, JoinType.Anti, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c> over a subquery.</summary>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, QueryCommand<TJoinEntity> query, Action<JoinOptions>? options = null)
        => builder.JoinCore(query, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>SAMPLE ratio</c> modifier.</summary>
    public static FromOptions Sample(this FromOptions options, double ratio)
        => options.Sample(ratio);

    /// <summary>ClickHouse <c>SAMPLE ratio OFFSET offset</c> modifier.</summary>
    public static FromOptions Sample(this FromOptions options, double ratio, double offset)
        => options.Sample(ratio, offset);

    /// <summary>ClickHouse <c>GLOBAL</c> join modifier.</summary>
    public static JoinOptions Global(this JoinOptions options)
        => options.Global();

    /// <summary>ClickHouse join modifier (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>).</summary>
    public static JoinOptions WithStrictness(this JoinOptions options, JoinStrictness strictness)
        => options.WithStrictness(strictness);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder SemiJoin(this EntityBuilder builder, EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(builder, new JoinExpression(joinCondition, JoinType.Semi)
        {
            From = new FromExpression(from.Table!),
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder AntiJoin(this EntityBuilder builder, EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(builder, new JoinExpression(joinCondition, JoinType.Anti)
        {
            From = new FromExpression(from.Table!),
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static JoinedEntityBuilder<TableAlias, TableAlias> PasteJoin(this EntityBuilder builder, EntityBuilder from, Action<JoinOptions>? options = null)
        => builder.JoinCore(from, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder SemiJoin<TJoinEntity>(this EntityBuilder builder, EntityBuilder<TJoinEntity> join, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(builder, new JoinExpression(joinCondition, JoinType.Semi)
        {
            From = builder.DataProvider.GetFrom(typeof(TJoinEntity), null)!,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder AntiJoin<TJoinEntity>(this EntityBuilder builder, EntityBuilder<TJoinEntity> join, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        var opts = new JoinOptions();
        options?.Invoke(opts);
        return AddSemiAntiJoin(builder, new JoinExpression(joinCondition, JoinType.Anti)
        {
            From = builder.DataProvider.GetFrom(typeof(TJoinEntity), null)!,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> PasteJoin<TJoinEntity>(this EntityBuilder builder, EntityBuilder<TJoinEntity> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    private static EntityBuilder<TEntity> AddArrayJoin<TEntity, TArray>(EntityBuilder<TEntity> builder, Expression<Func<TEntity, TArray>> array, ArrayJoinKind kind)
    {
        ArgumentNullException.ThrowIfNull(array);

        if (typeof(TArray) == typeof(string) || !typeof(System.Collections.IEnumerable).IsAssignableFrom(typeof(TArray)))
            throw new ArgumentException("ARRAY JOIN requires an array or sequence expression.", nameof(array));

        if (builder.ArrayJoins is { Count: > 0 } && builder.ArrayJoinKind != kind)
            throw new InvalidOperationException("An ARRAY JOIN clause cannot mix ARRAY JOIN and LEFT ARRAY JOIN.");

        var b = builder.Clone();

        var joins = b.ArrayJoinsBacking ??= new List<LambdaExpression>(1);
        joins.Add(array);
        b.ArrayJoinKind = kind;

        return b;
    }

    private static EntityBuilder<ArrayJoinProjection<TEntity, TElement>> ToArrayJoinElement<TEntity, TElement>(EntityBuilder<TEntity> builder, Expression<Func<TEntity, IEnumerable<TElement>>> array, ArrayJoinKind kind)
    {
        ArgumentNullException.ThrowIfNull(array);
        builder.EnsureNoEagerLoadState(kind == ArrayJoinKind.Left ? nameof(LeftArrayJoinElement) : nameof(ArrayJoinElement));

        if (builder.SourceEntityType is not null)
            throw new InvalidOperationException("Only one ArrayJoinElement/LeftArrayJoinElement is supported per query.");

        if (builder.Condition is not null || builder.HavingCondition is not null)
            throw new InvalidOperationException("Where/Having must be applied after ArrayJoinElement/LeftArrayJoinElement, whose projection parameter differs from the entity.");

        if (builder.Joins is { Count: > 0 })
            throw new InvalidOperationException("ArrayJoinElement/LeftArrayJoinElement is not supported on a joined query; use ArrayJoin/LeftArrayJoin.");

        if (builder.ArrayJoins is { Count: > 0 } && builder.ArrayJoinKind != kind)
            throw new InvalidOperationException("An ARRAY JOIN clause cannot mix ARRAY JOIN and LEFT ARRAY JOIN.");

        if (builder.Windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be applied before ArrayJoinElement/LeftArrayJoinElement.");

        var b = new EntityBuilder<ArrayJoinProjection<TEntity, TElement>>(builder.DataProvider)
        {
            Logger = builder.Logger,
            SourceEntityType = typeof(TEntity)
        };

        // Carry the query shape. The projection parameter type changes, so the Where/Having lambdas
        // cannot be carried (they are rejected above); everything else is projection independent.
        builder.CopyProjectionIndependentStateTo(b);

        var joins = builder.ArrayJoins is null ? new List<LambdaExpression>(1) : new List<LambdaExpression>(builder.ArrayJoins);
        joins.Add(array);
        b.ArrayJoinsBacking = joins;
        b.ArrayJoinKind = kind;
        b.BindArrayJoinElement = true;

        return b;
    }

    private static EntityBuilder<TEntity> AddSemiAntiJoin<TEntity>(EntityBuilder<TEntity> builder, FromExpression rightSource, LambdaExpression joinCondition, JoinType joinType, Action<JoinOptions>? options = null)
    {
        if (builder.JoinIntos is { Count: > 0 })
            throw new NotSupportedException(
                "JoinInto cannot be combined with other joins on the same builder; declare JoinInto on a plain entity source only.");

        if (builder.Windows is not null)
            throw new InvalidOperationException("Named windows must be declared after joins; Window cannot be combined with a later Join.");

        var opts = new JoinOptions();
        options?.Invoke(opts);

        var b = builder.Clone();
        var joins = b.Joins;
        if (joins is null)
            b.Joins = joins = builder.Joins is null ? [] : [.. builder.Joins];

        joins.Add(new JoinExpression(joinCondition, joinType)
        {
            From = rightSource,
            EntityType = null,
            Strictness = opts.Strictness ?? JoinStrictness.Default,
            IsGlobal = opts.IsGlobal,
            JoinHint = opts.JoinHint,
            TableHints = opts.TableHints
        });

        return b;
    }

    private static EntityBuilder AddSemiAntiJoin(EntityBuilder builder, JoinExpression join)
    {
        var b = builder.Clone();
        var joins = b.Joins;
        if (joins is null)
            b.Joins = joins = builder.Joins is null ? [] : [.. builder.Joins];

        joins.Add(join);

        return b;
    }

}

/// <summary>
/// Arity-preserving ClickHouse extension methods for <c>JoinedEntityBuilder&lt;...&gt;</c>: they keep
/// the explicitly typed projection so a join can be followed by further joins and operators. Add
/// <c>using NextORM.ClickHouse;</c> to use them.
/// </summary>
public static class ClickHouseJoinedEntityBuilderExtensions
{
    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2> SemiJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, EntityBuilder<T3> join, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2>)((EntityBuilder<Projection<T1, T2>>)builder).SemiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2> AntiJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, EntityBuilder<T3> join, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2>)((EntityBuilder<Projection<T1, T2>>)builder).AntiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> PasteJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, EntityBuilder<T3> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> SemiJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, EntityBuilder<T4> join, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3>)((EntityBuilder<Projection<T1, T2, T3>>)builder).SemiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> AntiJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, EntityBuilder<T4> join, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3>)((EntityBuilder<Projection<T1, T2, T3>>)builder).AntiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> PasteJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, EntityBuilder<T4> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> SemiJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, EntityBuilder<T5> join, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)((EntityBuilder<Projection<T1, T2, T3, T4>>)builder).SemiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> AntiJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, EntityBuilder<T5> join, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)((EntityBuilder<Projection<T1, T2, T3, T4>>)builder).AntiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> PasteJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, EntityBuilder<T5> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> SemiJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, EntityBuilder<T6> join, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)((EntityBuilder<Projection<T1, T2, T3, T4, T5>>)builder).SemiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> AntiJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, EntityBuilder<T6> join, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)((EntityBuilder<Projection<T1, T2, T3, T4, T5>>)builder).AntiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> PasteJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, EntityBuilder<T6> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> SemiJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, EntityBuilder<T7> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6>>)builder).SemiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> AntiJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, EntityBuilder<T7> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6>>)builder).AntiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> PasteJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, EntityBuilder<T7> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> SemiJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, EntityBuilder<T8> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>>)builder).SemiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> AntiJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, EntityBuilder<T8> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>>)builder).AntiJoin(join, joinCondition, options);

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> PasteJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, EntityBuilder<T8> join, Action<JoinOptions>? options = null)
        => builder.JoinCore(join, JoinType.Paste, null, options);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2> ArrayJoin<T1, T2, TArray>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<Projection<T1, T2>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2>)((EntityBuilder<Projection<T1, T2>>)builder).ArrayJoin(array);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2> LeftArrayJoin<T1, T2, TArray>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<Projection<T1, T2>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2>)((EntityBuilder<Projection<T1, T2>>)builder).LeftArrayJoin(array);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> ArrayJoin<T1, T2, T3, TArray>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<Projection<T1, T2, T3>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3>)((EntityBuilder<Projection<T1, T2, T3>>)builder).ArrayJoin(array);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> LeftArrayJoin<T1, T2, T3, TArray>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<Projection<T1, T2, T3>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3>)((EntityBuilder<Projection<T1, T2, T3>>)builder).LeftArrayJoin(array);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> ArrayJoin<T1, T2, T3, T4, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)((EntityBuilder<Projection<T1, T2, T3, T4>>)builder).ArrayJoin(array);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> LeftArrayJoin<T1, T2, T3, T4, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4>)((EntityBuilder<Projection<T1, T2, T3, T4>>)builder).LeftArrayJoin(array);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> ArrayJoin<T1, T2, T3, T4, T5, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)((EntityBuilder<Projection<T1, T2, T3, T4, T5>>)builder).ArrayJoin(array);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftArrayJoin<T1, T2, T3, T4, T5, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5>)((EntityBuilder<Projection<T1, T2, T3, T4, T5>>)builder).LeftArrayJoin(array);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> ArrayJoin<T1, T2, T3, T4, T5, T6, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6>>)builder).ArrayJoin(array);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftArrayJoin<T1, T2, T3, T4, T5, T6, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6>>)builder).LeftArrayJoin(array);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> ArrayJoin<T1, T2, T3, T4, T5, T6, T7, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>>)builder).ArrayJoin(array);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftArrayJoin<T1, T2, T3, T4, T5, T6, T7, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>>)builder).LeftArrayJoin(array);

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> ArrayJoin<T1, T2, T3, T4, T5, T6, T7, T8, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7, T8>>)builder).ArrayJoin(array);

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftArrayJoin<T1, T2, T3, T4, T5, T6, T7, T8, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
        => (JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8>)((EntityBuilder<Projection<T1, T2, T3, T4, T5, T6, T7, T8>>)builder).LeftArrayJoin(array);
}
