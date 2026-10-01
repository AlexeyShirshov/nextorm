using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Fluent starter for the row-returning form of a multi-table <c>DELETE</c>, produced by
/// <see cref="DataContextExtensions.CreateDeleteJoinBuilder{T1, T2}(JoinedEntityBuilder{T1, T2})"/>. It
/// switches an already-built joined query to the returning terminal through <see cref="Returning"/> or
/// <see cref="Returning{TResult}"/>; the target is the first table of the join chain and the removed-row
/// values come from every joined table the selector reads. The whole-projection (identity) form returns
/// every returnable mapped property of every item slot, in slot order, so a self-join of one type keeps
/// its <c>Item1</c>/<c>Item2</c> values distinct (a repeated CLR type stays separated by slot).
/// <para>
/// PostgreSQL only, on INNER joins and for arities 2–8; every other provider and every outer join
/// rejects. The same builder is also accepted as a data-modifying CTE body through
/// <see cref="DataContextExtensions.With{TProjection, TResult}(IDataContext, string, DeleteJoinReturningBuilder{TProjection, TResult})"/>.
/// </para>
/// </summary>
/// <typeparam name="TProjection">The positional join projection (<c>Projection&lt;T1, ...&gt;</c>).</typeparam>
public sealed class DeleteJoinBuilder<TProjection>
{
    private readonly EntityBuilder<TProjection> _query;
    private readonly Type _targetType;

    internal DeleteJoinBuilder(EntityBuilder<TProjection> query, Type targetType)
    {
        _query = query;
        _targetType = targetType;
    }

    /// <summary>
    /// Switches the builder to the whole-projection (identity) row-returning terminal: every returnable
    /// mapped property of every item slot is returned, in slot order, so a self-join of one type keeps its
    /// <c>Item1</c>/<c>Item2</c> values distinct (a repeated CLR type stays separated by slot). Equivalent
    /// to <c>Returning(p =&gt; p)</c>. The removed rows carry the deleted-row values, and the builder can be
    /// consumed directly or as a data-modifying CTE body through <c>With(name, delete)</c>. Returned
    /// columns get deterministic per-slot aliases; explicit projections are unchanged and no call adds a
    /// <c>RETURNING</c> list implicitly.
    /// <para>
    /// PostgreSQL only, on INNER joins and for arities 2–8; every other provider and every outer join
    /// rejects. A returned item whose mapped property is a multi-column <see cref="Range{T}"/> is rejected,
    /// as is a required member with no counterpart in the joined source shape.
    /// </para>
    /// </summary>
    /// <returns>A returning builder whose terminals produce the full <typeparamref name="TProjection"/>.</returns>
    /// <exception cref="NotSupportedException">A returned mapped property is a multi-column <see cref="Range{T}"/>, or the provider/join is unsupported.</exception>
    /// <exception cref="QueryPreparationException">A returned item slot has no registered entity metadata and exposes no readable columns.</exception>
    public DeleteJoinReturningBuilder<TProjection, TProjection> Returning()
    {
        var parameter = Expression.Parameter(typeof(TProjection), "p");
        var identity = Expression.Lambda<Func<TProjection, TProjection>>(parameter, parameter);
        var (columns, selectList, oneColumn) = JoinedReturningProjection.Parse(identity);
        return new DeleteJoinReturningBuilder<TProjection, TProjection>(_query, _targetType, columns, selectList, oneColumn, identity);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises a projection of the removed rows
    /// through PostgreSQL's <c>DELETE ... USING ... RETURNING</c> form. A selected member may reference any
    /// joined table (for example <c>Returning(p =&gt; new { p.Item1.Id, p.Item2.Name })</c>). Only
    /// PostgreSQL supports returning rows from a multi-table delete; every other provider rejects it.
    /// </summary>
    /// <typeparam name="TResult">The projected row shape.</typeparam>
    /// <param name="projection">Selects the mapped columns to return.</param>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TResult"/>.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The projection references something other than mapped properties.</exception>
    public DeleteJoinReturningBuilder<TProjection, TResult> Returning<TResult>(Expression<Func<TProjection, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var (columns, selectList, oneColumn) = JoinedReturningProjection.Parse(projection);
        return new DeleteJoinReturningBuilder<TProjection, TResult>(_query, _targetType, columns, selectList, oneColumn, projection);
    }
}
