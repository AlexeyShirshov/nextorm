namespace NextORM.Core;

/// <summary>A typed source for queries and data modifications on a mapped entity.</summary>
public interface IRepository<TEntity>
{
    /// <summary>Gets the underlying provider context.</summary>
    IDataContext DataContext { get; }

    /// <summary>Starts an independent query over the mapped entity.</summary>
    EntityBuilder<TEntity> Query();
}
