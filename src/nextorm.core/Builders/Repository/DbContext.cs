namespace NextORM.Core.Repositories;

/// <summary>Provides typed repositories over a borrowed data context.</summary>
public abstract class DbContext
{
    private readonly Dictionary<Type, object> _repositories = [];

    /// <summary>Initializes the facade without taking ownership of the provider.</summary>
    protected DbContext(IDataContext dataContext)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        DataContext = dataContext;
    }

    /// <summary>Gets the exact provider context supplied to the facade.</summary>
    public IDataContext DataContext { get; }

    /// <summary>Gets the mapped repository for this entity type.</summary>
    protected IRepository<TEntity> Repository<TEntity>() => GetRepository<TEntity>(null);

    /// <summary>Gets the repository, using the supplied configuration on first mapping registration.</summary>
    protected IRepository<TEntity> Repository<TEntity>(Action<EntityMetadataBuilder<TEntity>> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        return GetRepository(configure);
    }

    private IRepository<TEntity> GetRepository<TEntity>(Action<EntityMetadataBuilder<TEntity>>? configure)
    {
        if (!_repositories.TryGetValue(typeof(TEntity), out var repository))
        {
            if (configure is not null)
                _ = DataContext.From(configure);

            repository = new MappedRepository<TEntity>(DataContext);
            _repositories.Add(typeof(TEntity), repository);
        }

        return (IRepository<TEntity>)repository;
    }

    private sealed class MappedRepository<TEntity>(IDataContext dataContext) : IRepository<TEntity>
    {
        public IDataContext DataContext => dataContext;

        public EntityBuilder<TEntity> Query() => dataContext.From<TEntity>();
    }
}
