using System.Linq.Expressions;

namespace NextORM.Core;

public static partial class RepositoryExtensions
{
    extension<TEntity>(IRepository<TEntity> repository)
    {
        /// <summary>Starts a query that projects each row with the given selector.</summary>
        public QueryCommand<TResult> Select<TResult>(Expression<Func<TEntity, TResult>> exp) =>
            repository.Query().Select(exp);

        /// <summary>Starts a composable query over a projection of the repository's rows.</summary>
        public EntityBuilder<TResult> As<TResult>(Expression<Func<TEntity, TResult>> selector) =>
            repository.Query().As(selector);

        /// <summary>Starts a query that flattens a collection from each row.</summary>
        public EntityBuilder<TCollection> SelectMany<TCollection>(Expression<Func<TEntity, IEnumerable<TCollection>>> collectionSelector) =>
            repository.Query().SelectMany(collectionSelector);

        /// <summary>Starts a query that flattens a collection and projects each parent and element pair.</summary>
        public EntityBuilder<TResult> SelectMany<TCollection, TResult>(
            Expression<Func<TEntity, IEnumerable<TCollection>>> collectionSelector,
            Expression<Func<TEntity, TCollection, TResult>> resultSelector) =>
            repository.Query().SelectMany(collectionSelector, resultSelector);

        /// <summary>Creates a query command that selects the mapped entity.</summary>
        public QueryCommand<TEntity> ToCommand() => repository.Query().ToCommand();

        /// <summary>Starts a query that removes duplicate result rows.</summary>
        public EntityBuilder<TEntity> Distinct() => repository.Query().Distinct();

        /// <summary>Starts a query that skips the specified number of rows.</summary>
        public EntityBuilder<TEntity> Offset(int offset) => repository.Query().Offset(offset);

        /// <summary>Starts a query with both a row limit and an offset.</summary>
        public EntityBuilder<TEntity> Page(int limit, int offset) => repository.Query().Page(limit, offset);

        /// <summary>Starts a query ordered by the given key and direction.</summary>
        public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp, OrderDirection direction) =>
            repository.Query().OrderBy(orderExp, direction);

        /// <summary>Starts a query in ascending order with the given null placement.</summary>
        public EntityBuilder<TEntity> OrderBy(Expression<Func<TEntity, object?>> orderExp, NullsPosition nullsPosition) =>
            repository.Query().OrderBy(orderExp, nullsPosition);

        /// <summary>Starts a query ordered by the given key, direction, and null placement.</summary>
        public EntityBuilder<TEntity> OrderBy(
            Expression<Func<TEntity, object?>> orderExp, OrderDirection direction, NullsPosition nullsPosition) =>
            repository.Query().OrderBy(orderExp, direction, nullsPosition);

        /// <summary>Starts a query ordered by a 1-based result column index.</summary>
        public EntityBuilder<TEntity> OrderBy(int columnIdx) => repository.Query().OrderBy(columnIdx);

        /// <summary>Starts a query ordered by a 1-based result column index and direction.</summary>
        public EntityBuilder<TEntity> OrderBy(int columnIdx, OrderDirection direction) =>
            repository.Query().OrderBy(columnIdx, direction);

        /// <summary>Starts an ascending query order by column index with the given null placement.</summary>
        public EntityBuilder<TEntity> OrderBy(int columnIdx, NullsPosition nullsPosition) =>
            repository.Query().OrderBy(columnIdx, nullsPosition);

        /// <summary>Starts a query ordered by column index, direction, and null placement.</summary>
        public EntityBuilder<TEntity> OrderBy(int columnIdx, OrderDirection direction, NullsPosition nullsPosition) =>
            repository.Query().OrderBy(columnIdx, direction, nullsPosition);

        /// <summary>Starts a query in descending order with the given null placement.</summary>
        public EntityBuilder<TEntity> OrderByDescending(Expression<Func<TEntity, object?>> orderExp, NullsPosition nullsPosition) =>
            repository.Query().OrderByDescending(orderExp, nullsPosition);

        /// <summary>Starts a query in descending order by a 1-based result column index.</summary>
        public EntityBuilder<TEntity> OrderByDescending(int columnIdx) => repository.Query().OrderByDescending(columnIdx);

        /// <summary>Starts a descending query order by column index with the given null placement.</summary>
        public EntityBuilder<TEntity> OrderByDescending(int columnIdx, NullsPosition nullsPosition) =>
            repository.Query().OrderByDescending(columnIdx, nullsPosition);

        /// <summary>Starts a query that groups rows by the given key.</summary>
        public EntityBuilder<TEntity> GroupBy<TResult>(Expression<Func<TEntity, TResult>> exp) =>
            repository.Query().GroupBy(exp);

        /// <summary>Starts a grouped query with prefix subtotals and a grand total.</summary>
        public EntityBuilder<TEntity> GroupByRollup<TResult>(Expression<Func<TEntity, TResult>> exp) =>
            repository.Query().GroupByRollup(exp);

        /// <summary>Starts a grouped query with subtotals for every combination of grouping columns.</summary>
        public EntityBuilder<TEntity> GroupByCube<TResult>(Expression<Func<TEntity, TResult>> exp) =>
            repository.Query().GroupByCube(exp);

        /// <summary>Starts a grouped query with explicit sets of grouping columns.</summary>
        public EntityBuilder<TEntity> GroupByGroupingSets<TResult>(Expression<Func<TEntity, TResult>> exp, params int[][] sets) =>
            repository.Query().GroupByGroupingSets(exp, sets);

        /// <summary>Starts a query with a named SQL window.</summary>
        public EntityBuilder<TEntity> Window(string name, Expression<Func<TEntity, object?>>[]? partitionBy = null,
            NamedWindowOrderKey[]? orderBy = null, WindowFrame? frame = null) =>
            repository.Query().Window(name, partitionBy, orderBy, frame);

        /// <summary>Starts a query without global filters.</summary>
        public EntityBuilder<TEntity> IgnoreFilters() => repository.Query().IgnoreFilters();

        /// <summary>Starts a query that ignores global filters on the given entity types.</summary>
        public EntityBuilder<TEntity> IgnoreFilters(params Type[] entityTypes) =>
            repository.Query().IgnoreFilters(entityTypes);

        /// <summary>Starts a query that ignores the named global filters.</summary>
        public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys) =>
            repository.Query().IgnoreFilters(filterKeys);

        /// <summary>Starts a query that ignores filters by name or entity type.</summary>
        public EntityBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes) =>
            repository.Query().IgnoreFilters(filterKeys, entityTypes);

        /// <summary>Starts a nearest-neighbor query for the given vector.</summary>
        public QueryCommand<VectorSearchResult<TEntity>> VectorSearch<TVector>(
            Expression<Func<TEntity, TVector>> vectorSelector, TVector queryVector, VectorDistanceMetric metric,
            int topK, VectorSearchMode mode = VectorSearchMode.Exact)
            where TVector : IVectorValue =>
            repository.Query().VectorSearch(vectorSelector, queryVector, metric, topK, mode);
    }
}
