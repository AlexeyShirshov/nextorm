using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// #320 (T320) lambda-source join overloads: the runtime table-valued-function descriptor is supplied as
/// an <b>expression</b> (<c>builder.Join(() =&gt; Db.MyTvf(42), (left, right) =&gt; ...)</c>), so the factory
/// call keeps its argument expressions and the source is lowered by the shared
/// <see cref="SourceExpressionLowerer"/> instead of being resolved from entity metadata.
/// <para>
/// These are extension methods on purpose: an instance overload taking
/// <see cref="Expression{TDelegate}"/> next to the existing <c>Cte&lt;T&gt;</c> overload would make an
/// untyped <c>Join(null, null)</c> probe ambiguous (the existing call must keep binding to the CTE
/// overload), while an extension is only considered when no instance overload applies. Calls that
/// passed an entity/CTE/derived-query builder keep binding to the instance methods unchanged.
/// </para>
/// <para>
/// The factory must return <see cref="TvfEntityBuilder{T}"/> for the same row type, and its metadata must
/// not depend on the argument values. A captured descriptor or any other shape without the original call
/// expression is rejected with <see cref="NotSupportedException"/> (lost provenance).
/// </para>
/// </summary>
public static class TvfJoinSourceExtensions
{

    // EntityBuilder<TEntity>: the single-entity receiver.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> Join<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> Join<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="TEntity">The projection type of the receiver.</typeparam>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TEntity, TJoinEntity>(this EntityBuilder<TEntity> builder, Expression<Func<EntityBuilder<TJoinEntity>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }

    // Non-generic named-table EntityBuilder keeps the existing TableAlias return convention.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> Join<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> Join<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> LeftJoin<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> RightJoin<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> FullJoin<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source, Expression<Func<TableAlias, TJoinEntity, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> CrossJoin<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> CrossApply<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="TJoinEntity">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<TableAlias, TJoinEntity> OuterApply<TJoinEntity>(this EntityBuilder builder, Expression<Func<EntityBuilder<TJoinEntity>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }

    // JoinedEntityBuilder<T1..T2>: the projection arity is carried into the predicate type.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> Join<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> Join<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> LeftJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> LeftJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> RightJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> RightJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> FullJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> FullJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source, Expression<Func<Projection<T1, T2>, T3, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> CrossJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> CrossApply<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3> OuterApply<T1, T2, T3>(this JoinedEntityBuilder<T1, T2> builder, Expression<Func<EntityBuilder<T3>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }

    // JoinedEntityBuilder<T1..T3>: the projection arity is carried into the predicate type.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> Join<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> Join<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> LeftJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> LeftJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> RightJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> RightJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> FullJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> FullJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source, Expression<Func<Projection<T1, T2, T3>, T4, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> CrossJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> CrossApply<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4> OuterApply<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3> builder, Expression<Func<EntityBuilder<T4>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }

    // JoinedEntityBuilder<T1..T4>: the projection arity is carried into the predicate type.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> Join<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> Join<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> LeftJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> RightJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> RightJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> FullJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> FullJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source, Expression<Func<Projection<T1, T2, T3, T4>, T5, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> CrossJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> CrossApply<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5> OuterApply<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4> builder, Expression<Func<EntityBuilder<T5>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }

    // JoinedEntityBuilder<T1..T5>: the projection arity is carried into the predicate type.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Join<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> Join<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> LeftJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> RightJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> RightJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> FullJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> FullJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5>, T6, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> CrossJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> CrossApply<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> OuterApply<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> builder, Expression<Func<EntityBuilder<T6>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }

    // JoinedEntityBuilder<T1..T6>: the projection arity is carried into the predicate type.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> Join<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> Join<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> LeftJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> RightJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> RightJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> FullJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> FullJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6>, T7, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> CrossJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> CrossApply<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> OuterApply<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> builder, Expression<Func<EntityBuilder<T7>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }

    // JoinedEntityBuilder<T1..T7>: the projection arity is carried into the predicate type.

    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Join<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => builder.Join(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>INNER JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> Join<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.Join(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => builder.LeftJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>LEFT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> LeftJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.LeftJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> RightJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => builder.RightJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>RIGHT JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> RightJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.RightJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source supplied as an expression.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/>, <paramref name="source"/> or <paramref name="joinCondition"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> FullJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition)
        => builder.FullJoin(builder.DataProvider.From(source), joinCondition);
    /// <summary>Adds a <c>FULL JOIN</c> over a runtime table-valued-function source with per-join configuration.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <param name="joinCondition">The join predicate over the two sources.</param>
    /// <param name="configure">Per-join configuration, for example <c>j =&gt; j.WithJoinHint("loop")</c>.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException">A required argument is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> FullJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source, Expression<Func<Projection<T1, T2, T3, T4, T5, T6, T7>, T8, bool>> joinCondition, Action<JoinOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        return builder.FullJoin(builder.DataProvider.From(source), joinCondition, configure);
    }
    /// <summary>Adds a <c>CROSS JOIN</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> CrossJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossJoin(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>CROSS APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> CrossApply<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.CrossApply(builder.DataProvider.From(source));
    }
    /// <summary>Adds a <c>OUTER APPLY</c> over a runtime table-valued-function source supplied as an expression; there is no <c>ON</c> condition.</summary>
    /// <typeparam name="T1">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T2">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T3">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T4">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T5">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T6">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T7">A projection element type of the receiver.</typeparam>
    /// <typeparam name="T8">The mapped row type the function returns.</typeparam>
    /// <param name="builder">The builder whose join chain is extended.</param>
    /// <param name="source">The expression that calls the runtime factory.</param>
    /// <returns>A builder over the joined projection.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="builder"/> or <paramref name="source"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The expression is not a direct runtime-factory call or lost its argument provenance.</exception>
    public static JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> OuterApply<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> builder, Expression<Func<EntityBuilder<T8>>> source)
    {
        ArgumentNullException.ThrowIfNull(builder);
        return builder.OuterApply(builder.DataProvider.From(source));
    }
}
