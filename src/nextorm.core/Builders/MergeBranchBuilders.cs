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

    /// <summary>Updates the selected columns of the matched row from the same-named source columns.</summary>
    /// <param name="columns">Selects the written columns, for example <c>d =&gt; new { d.Name, d.Age }</c>.</param>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenUpdate(Expression<Func<TEntity, object?>> columns)
        => _merge.AddMatchedUpdate(columns, _condition);

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

    /// <summary>Inserts the selected columns of the unmatched source row.</summary>
    /// <param name="columns">Selects the written columns, for example <c>d =&gt; new { d.Id, d.Name }</c>.</param>
    /// <returns>The originating merge builder, for chaining further branches.</returns>
    public MergeBuilder<TEntity> ThenInsert(Expression<Func<TEntity, object?>> columns)
        => _merge.AddNotMatchedInsert(columns, _condition);

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
