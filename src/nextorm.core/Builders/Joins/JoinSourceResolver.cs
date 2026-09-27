namespace NextORM.Core;

/// <summary>
/// Resolves the source of a joined builder into a <see cref="FromExpression"/>. A builder over a raw
/// table or CTE name (the <c>From(string)</c> shape) carries the name in <see cref="EntityBuilder{TEntity}.Table"/>
/// rather than a <see cref="FromExpression"/>, so it must be turned back into a table source; otherwise
/// the join falls back to entity metadata that does not exist for <see cref="TableAlias"/>. Shared by the
/// single-entity join builder and the multi-table <c>JoinedEntityBuilder</c> chain so a name-based source
/// works on every join position, not only the first.
/// </summary>
internal static class JoinSourceResolver
{
    internal static FromExpression Resolve<TJoinEntity>(IDataContext dataProvider, EntityBuilder<TJoinEntity> builder)
        => builder.ResolveSource() ?? dataProvider.GetFrom(typeof(TJoinEntity), null)!;
}
