using System.Linq.Expressions;

namespace NextORM.Core;

public partial class EntityBuilder<TEntity>
{
    internal JoinedEntityBuilder<TEntity, TJoinEntity> JoinRepository<TJoinEntity>(
        IRepository<TJoinEntity> repository, Expression<Func<TEntity, TJoinEntity, bool>> predicate)
    {
        RepositoryExtensions.RequireSameContext(RequireContext, repository);
        return Join(predicate);
    }
}
