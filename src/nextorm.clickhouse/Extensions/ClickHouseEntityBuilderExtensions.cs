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
        return builder.Final();
    }

    /// <summary>Trailing ClickHouse <c>SETTINGS key = value</c> clause.</summary>
    public static EntityBuilder<TEntity> Settings<TEntity>(this EntityBuilder<TEntity> builder, params (string Key, string Value)[] settings)
    {
        return builder.Settings(settings);
    }

    /// <summary>ClickHouse <c>PREWHERE</c> predicate.</summary>
    public static EntityBuilder<TEntity> PreWhere<TEntity>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, bool>> condition)
    {
        return builder.PreWhere(condition);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over an array expression.</summary>
    public static EntityBuilder<TEntity> ArrayJoin<TEntity, TArray>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over an array expression.</summary>
    public static EntityBuilder<TEntity> LeftArrayJoin<TEntity, TArray>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> exposing the expanded element as <c>p.Element</c>.</summary>
    public static EntityBuilder<ArrayJoinProjection<TEntity, TElement>> ArrayJoinElement<TEntity, TElement>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, IEnumerable<TElement>>> array)
    {
        return builder.ArrayJoinElement(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> exposing the expanded element as <c>p.Element</c>.</summary>
    public static EntityBuilder<ArrayJoinProjection<TEntity, TElement>> LeftArrayJoinElement<TEntity, TElement>(this EntityBuilder<TEntity> builder, Expression<Func<TEntity, IEnumerable<TElement>>> array)
    {
        return builder.LeftArrayJoinElement(array);
    }

    /// <summary>ClickHouse <c>LIMIT n BY expr</c> clause.</summary>
    public static EntityBuilder<TEntity> LimitBy<TEntity, TResult>(this EntityBuilder<TEntity> builder, int limit, Expression<Func<TEntity, TResult>> exp)
    {
        return builder.LimitBy(limit, exp);
    }

    /// <summary>ClickHouse <c>LIMIT offset, n BY expr</c> clause.</summary>
    public static EntityBuilder<TEntity> LimitBy<TEntity, TResult>(this EntityBuilder<TEntity> builder, int limit, int offset, Expression<Func<TEntity, TResult>> exp)
    {
        return builder.LimitBy(limit, offset, exp);
    }

    /// <summary>ClickHouse <c>WITH TOTALS</c> grouping modifier.</summary>
    public static EntityBuilder<TEntity> WithTotals<TEntity>(this EntityBuilder<TEntity> builder)
    {
        return builder.WithTotals();
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static EntityBuilder<TEntity> SemiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, EntityBuilder<TJoinEntity> join, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static EntityBuilder<TEntity> AntiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, EntityBuilder<TJoinEntity> join, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, EntityBuilder<TJoinEntity> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c> over a subquery.</summary>
    public static EntityBuilder<TEntity> SemiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(query, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c> over a subquery.</summary>
    public static EntityBuilder<TEntity> AntiJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(query, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c> over a subquery.</summary>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> PasteJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, QueryCommand<TJoinEntity> query, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(query, options);
    }

    /// <summary>ClickHouse <c>SAMPLE ratio</c> modifier.</summary>
    public static FromOptions Sample(this FromOptions options, double ratio)
    {
        return options.Sample(ratio);
    }

    /// <summary>ClickHouse <c>SAMPLE ratio OFFSET offset</c> modifier.</summary>
    public static FromOptions Sample(this FromOptions options, double ratio, double offset)
    {
        return options.Sample(ratio, offset);
    }

    /// <summary>ClickHouse <c>GLOBAL</c> join modifier.</summary>
    public static JoinOptions Global(this JoinOptions options)
    {
        return options.Global();
    }

    /// <summary>ClickHouse join modifier (<c>ANY</c>/<c>ALL</c>/<c>ASOF</c>).</summary>
    public static JoinOptions WithStrictness(this JoinOptions options, JoinStrictness strictness)
    {
        return options.WithStrictness(strictness);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder SemiJoin(this EntityBuilder builder, EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(from, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder AntiJoin(this EntityBuilder builder, EntityBuilder from, Expression<Func<TableAlias, TableAlias, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(from, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static JoinedEntityBuilder<TableAlias, TableAlias> PasteJoin(this EntityBuilder builder, EntityBuilder from, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(from, options);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder SemiJoin<TJoinEntity>(this EntityBuilder builder, EntityBuilder<TJoinEntity> join, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static EntityBuilder AntiJoin<TJoinEntity>(this EntityBuilder builder, EntityBuilder<TJoinEntity> join, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c> in named-table (<c>TableAlias</c>) mode.</summary>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> PasteJoin<TJoinEntity>(this EntityBuilder builder, EntityBuilder<TJoinEntity> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
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
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2> AntiJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, EntityBuilder<T3> join, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> PasteJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, EntityBuilder<T3> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> SemiJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, EntityBuilder<T4> join, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> AntiJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, EntityBuilder<T4> join, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> PasteJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, EntityBuilder<T4> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> SemiJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, EntityBuilder<T5> join, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> AntiJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, EntityBuilder<T5> join, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> PasteJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, EntityBuilder<T5> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> SemiJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, EntityBuilder<T6> join, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> AntiJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, EntityBuilder<T6> join, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> PasteJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, EntityBuilder<T6> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> SemiJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, EntityBuilder<T7> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> AntiJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, EntityBuilder<T7> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> PasteJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, EntityBuilder<T7> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
    }

    /// <summary>ClickHouse <c>LEFT SEMI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> SemiJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, EntityBuilder<T8> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.SemiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>LEFT ANTI JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> AntiJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, EntityBuilder<T8> join, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions>? options = null)
    {
        return builder.AntiJoin(join, joinCondition, options);
    }

    /// <summary>ClickHouse <c>PASTE JOIN</c>.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> PasteJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, EntityBuilder<T8> join, Action<JoinOptions>? options = null)
    {
        return builder.PasteJoin(join, options);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2> ArrayJoin<T1, T2, TArray>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<Projection<T1, T2>, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2> LeftArrayJoin<T1, T2, TArray>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<Projection<T1, T2>, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> ArrayJoin<T1, T2, T3, TArray>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<Projection<T1, T2, T3>, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3> LeftArrayJoin<T1, T2, T3, TArray>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<Projection<T1, T2, T3>, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> ArrayJoin<T1, T2, T3, T4, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4> LeftArrayJoin<T1, T2, T3, T4, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<Projection<T1, T2, T3, T4>, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> ArrayJoin<T1, T2, T3, T4, T5, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftArrayJoin<T1, T2, T3, T4, T5, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<Projection<T1, T2, T3, T4, T5>, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> ArrayJoin<T1, T2, T3, T4, T5, T6, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftArrayJoin<T1, T2, T3, T4, T5, T6, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> ArrayJoin<T1, T2, T3, T4, T5, T6, T7, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftArrayJoin<T1, T2, T3, T4, T5, T6, T7, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

    /// <summary>ClickHouse <c>ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> ArrayJoin<T1, T2, T3, T4, T5, T6, T7, T8, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
    {
        return builder.ArrayJoin(array);
    }

    /// <summary>ClickHouse <c>LEFT ARRAY JOIN</c> over the projection.</summary>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftArrayJoin<T1, T2, T3, T4, T5, T6, T7, T8, TArray>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> builder, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7, T8>, TArray>> array)
    {
        return builder.LeftArrayJoin(array);
    }

}

