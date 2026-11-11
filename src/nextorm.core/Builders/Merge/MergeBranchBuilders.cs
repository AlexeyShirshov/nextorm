using System.Linq.Expressions;

namespace NextORM.Core;

/// <summary>
/// Terminal of a full-<c>MERGE</c> <c>WHEN MATCHED</c> branch, produced by
/// <see cref="MergeBuilder{TEntity}.WhenMatched()"/>. Every terminal returns the originating
/// <see cref="MergeBuilder{TEntity}"/> so the branches can be chained.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type merged.</typeparam>
public sealed class MergeMatchedBuilder<TEntity>
{
    private readonly MergeBuilder<TEntity> _merge;
    private readonly LambdaExpression? _condition;

    internal MergeMatchedBuilder(MergeBuilder<TEntity> merge, LambdaExpression? condition = null)
    {
        _merge = merge;
        _condition = condition;
    }

    /// <summary>Updates every non-key writable column of the matched row from the source row.</summary>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenUpdate() => _merge.AddMatchedUpdate(null, _condition);

    /// <summary>
    /// Updates the explicitly assigned columns of the matched row. The lambda body must be an anonymous
    /// initializer or a named initializer assignable to the entity; each binding is
    /// <c>destination = value</c>, where the destination is a mapped CLR property of the entity and the
    /// value may read the pre-update target row (<c>target</c>) and the original input row
    /// (<c>source</c>). Columns that are not assigned are not written at all.
    /// </summary>
    /// <param name="assignments">The assignments, for example <c>(target, source) =&gt; new { Total = target.Total + source.Amount }</c>.</param>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    /// <exception cref="ArgumentException">The body is not a supported non-empty initializer, or a destination is unknown, duplicated or type-incompatible.</exception>
    /// <exception cref="NotSupportedException">A destination is identity, computed or the merge key, or a provider does not support the form.</exception>
    public MergeBuilder<TEntity> ThenUpdate(Expression<Func<TEntity, TEntity, object?>> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        return _merge.AddMatchedUpdate(assignments, _condition);
    }

    /// <summary>
    /// Updates the explicitly assigned columns of the matched row, with the third parameter naming the
    /// provider-native proposed insert row (<c>excluded</c>). A parameter that is never read is accepted;
    /// reading it on a form that has no native proposed row fails before execution.
    /// </summary>
    /// <param name="assignments">The assignments, for example <c>(target, source, excluded) =&gt; new { Count = target.Count + excluded.Count }</c>.</param>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    /// <exception cref="ArgumentException">The body is not a supported non-empty initializer, or a destination is unknown, duplicated or type-incompatible.</exception>
    /// <exception cref="NotSupportedException">A destination may not be written, or the provider has no proposed row for this form.</exception>
    public MergeBuilder<TEntity> ThenUpdate(Expression<Func<TEntity, TEntity, TEntity, object?>> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        return _merge.AddMatchedUpdate(assignments, _condition);
    }

    /// <summary>Deletes the matched target row.</summary>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenDelete() => _merge.AddDelete(MergeMatchKind.Matched, _condition);

    /// <summary>Leaves the matched target row unchanged (<c>DO NOTHING</c>; PostgreSQL only).</summary>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenDoNothing() => _merge.AddDoNothing(MergeMatchKind.Matched, _condition);
}

/// <summary>
/// Terminal of a full-<c>MERGE</c> <c>WHEN NOT MATCHED [BY TARGET]</c> branch, produced by
/// <see cref="MergeBuilder{TEntity}.WhenNotMatched()"/>. Every terminal returns the originating
/// <see cref="MergeBuilder{TEntity}"/> so the branches can be chained.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type merged.</typeparam>
public sealed class MergeNotMatchedBuilder<TEntity>
{
    private readonly MergeBuilder<TEntity> _merge;
    private readonly LambdaExpression? _condition;

    internal MergeNotMatchedBuilder(MergeBuilder<TEntity> merge, LambdaExpression? condition = null)
    {
        _merge = merge;
        _condition = condition;
    }

    /// <summary>Inserts every writable column of the unmatched source row.</summary>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenInsert() => _merge.AddNotMatchedInsert(null, _condition);

    /// <summary>
    /// Inserts the explicitly assigned columns of the unmatched source row. The lambda body must be an
    /// anonymous initializer or a named initializer assignable to the entity; only the listed columns are
    /// inserted, so every omitted column receives its normal database default, nullability or constraint
    /// behaviour. The proposed-row (<c>excluded</c>) and target rows are not visible here.
    /// </summary>
    /// <param name="assignments">The assignments, for example <c>source =&gt; new { Id = source.Id, Name = source.Name }</c>.</param>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    /// <exception cref="ArgumentException">The body is not a supported non-empty initializer, or a destination is unknown, duplicated or type-incompatible.</exception>
    /// <exception cref="NotSupportedException">A destination may not be written, or an unavailable role is referenced.</exception>
    public MergeBuilder<TEntity> ThenInsert(Expression<Func<TEntity, object?>> assignments)
    {
        ArgumentNullException.ThrowIfNull(assignments);
        return _merge.AddNotMatchedInsert(assignments, _condition);
    }

    /// <summary>Leaves the unmatched source row unwritten (<c>DO NOTHING</c>; PostgreSQL only).</summary>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenDoNothing() => _merge.AddDoNothing(MergeMatchKind.NotMatchedByTarget, _condition);
}

/// <summary>
/// Terminal of a full-<c>MERGE</c> <c>WHEN NOT MATCHED BY SOURCE</c> branch, produced by
/// <see cref="MergeBuilder{TEntity}.WhenNotMatchedBySource()"/>. Only SQL Server renders this branch; the
/// other dialects reject it with <see cref="NotSupportedException"/>.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type merged.</typeparam>
public sealed class MergeNotMatchedBySourceBuilder<TEntity>
{
    private readonly MergeBuilder<TEntity> _merge;
    private readonly LambdaExpression? _condition;

    internal MergeNotMatchedBySourceBuilder(MergeBuilder<TEntity> merge, LambdaExpression? condition = null)
    {
        _merge = merge;
        _condition = condition;
    }

    /// <summary>Deletes the target row that has no matching source row.</summary>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenDelete() => _merge.AddDelete(MergeMatchKind.NotMatchedBySource, _condition);

    /// <summary>Leaves the target row that has no matching source row unchanged (<c>DO NOTHING</c>; PostgreSQL only).</summary>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenDoNothing() => _merge.AddDoNothing(MergeMatchKind.NotMatchedBySource, _condition);
}
