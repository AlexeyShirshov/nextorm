using System.Linq.Expressions;
using NextORM.Core.Repositories;

namespace NextORM.Core;

/// <summary>Starts queries and mutations from a typed repository.</summary>
public static class RepositoryExtensions
{
    /// <summary>Starts a query against the repository's mapped table.</summary>
    public static EntityBuilder<TEntity> From<TEntity>(this IDataContext context, IRepository<TEntity> repository)
    {
        RequireSameContext(context, repository);
        return context.From<TEntity>();
    }

    /// <summary>Starts a query against the repository's mapped table.</summary>
    public static EntityBuilder<TEntity> From<TEntity>(this DbContext context, IRepository<TEntity> repository) =>
        context.DataContext.From(repository);

    /// <summary>Starts a query with the specified predicate.</summary>
    public static EntityBuilder<TEntity> Where<TEntity>(this IRepository<TEntity> repository, Expression<Func<TEntity, bool>> predicate) =>
        repository.Query().Where(predicate);

    /// <summary>Starts a query in ascending sort order.</summary>
    public static EntityBuilder<TEntity> OrderBy<TEntity>(this IRepository<TEntity> repository, Expression<Func<TEntity, object?>> key) =>
        repository.Query().OrderBy(key);

    /// <summary>Starts a query in descending sort order.</summary>
    public static EntityBuilder<TEntity> OrderByDescending<TEntity>(this IRepository<TEntity> repository, Expression<Func<TEntity, object?>> key) =>
        repository.Query().OrderByDescending(key);

    /// <summary>Starts a query with a row limit.</summary>
    public static EntityBuilder<TEntity> Limit<TEntity>(this IRepository<TEntity> repository, int count) =>
        repository.Query().Limit(count);

    /// <summary>Executes a query and reads the matching rows.</summary>
    public static List<TEntity> ToList<TEntity>(this IRepository<TEntity> repository) =>
        repository.Query().ToList();

    /// <summary>Asynchronously executes a query and reads the matching rows.</summary>
    public static Task<List<TEntity>> ToListAsync<TEntity>(this IRepository<TEntity> repository, CancellationToken cancellationToken = default) =>
        repository.Query().ToListAsync(cancellationToken);

    /// <summary>Counts the matching rows.</summary>
    public static int Count<TEntity>(this IRepository<TEntity> repository) =>
        repository.Query().Count();

    /// <summary>Asynchronously counts the matching rows.</summary>
    public static Task<int> CountAsync<TEntity>(this IRepository<TEntity> repository, CancellationToken cancellationToken = default) =>
        repository.Query().CountAsync(cancellationToken);

    /// <summary>Starts an independent insert.</summary>
    public static InsertBuilder<TEntity> MakeInsert<TEntity>(this IRepository<TEntity> repository) =>
        repository.DataContext.CreateInsertBuilder<TEntity>();

    /// <summary>Starts an independent update.</summary>
    public static UpdateBuilder<TEntity> MakeUpdate<TEntity>(this IRepository<TEntity> repository) =>
        repository.DataContext.CreateUpdateBuilder<TEntity>();

    /// <summary>Starts an independent delete.</summary>
    public static DeleteBuilder<TEntity> MakeDelete<TEntity>(this IRepository<TEntity> repository) =>
        repository.DataContext.CreateDeleteBuilder<TEntity>();

    /// <summary>Joins the repository's mapped table to the query.</summary>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> Join<TEntity, TJoinEntity>(
        this IRepository<TEntity> repository, IRepository<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> predicate) =>
        repository.Query().Join(source, predicate);

    /// <summary>Joins the repository's mapped table to the query.</summary>
    public static JoinedEntityBuilder<TEntity, TJoinEntity> Join<TEntity, TJoinEntity>(
        this EntityBuilder<TEntity> builder, IRepository<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> predicate) =>
        builder.JoinRepository(source, predicate);

    internal static void RequireSameContext<TEntity>(IDataContext context, IRepository<TEntity> repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        if (!ReferenceEquals(context, repository.DataContext))
            throw new ArgumentException("The repository belongs to a different data context.", nameof(repository));
    }
}
