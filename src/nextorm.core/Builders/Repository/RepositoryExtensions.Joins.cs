using System.Linq.Expressions;

namespace NextORM.Core;

public static partial class RepositoryExtensions
{
    extension<TEntity>(IRepository<TEntity> repository)
    {
        /// <summary>Starts an inner join to the repository's mapped source with the given options.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(
            IRepository<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> predicate, Action<JoinOptions>? options) =>
            repository.Query().Join(repository.DataContext.From(source), predicate, options);

        /// <summary>Starts an inner join to a builder source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(
            EntityBuilder<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().Join(source, joinCondition);

        /// <summary>Starts an inner join to a mapped entity type.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(
            Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().Join(joinCondition);

        /// <summary>Starts an inner join to the named table.</summary>
        public JoinedEntityBuilder<TEntity, TableAlias> Join(
            string table, Expression<Func<TEntity, TableAlias, bool>> joinCondition) =>
            repository.Query().Join(table, joinCondition);

        /// <summary>Starts an inner join to a common table expression.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(
            Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().Join(cte, joinCondition);

        /// <summary>Starts an inner join to a query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> Join<TJoinEntity>(
            QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().Join(query, joinCondition);

        /// <summary>Starts a left join to the repository's mapped source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(
            IRepository<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> predicate, Action<JoinOptions>? options = null) =>
            repository.Query().LeftJoin(repository.DataContext.From(source), predicate, options);

        /// <summary>Starts a left join to a builder source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(
            EntityBuilder<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().LeftJoin(source, joinCondition);

        /// <summary>Starts a left join to a mapped entity type.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(
            Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().LeftJoin(joinCondition);

        /// <summary>Starts a left join to the named table.</summary>
        public JoinedEntityBuilder<TEntity, TableAlias> LeftJoin(
            string table, Expression<Func<TEntity, TableAlias, bool>> joinCondition) =>
            repository.Query().LeftJoin(table, joinCondition);

        /// <summary>Starts a left join to a common table expression.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(
            Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().LeftJoin(cte, joinCondition);

        /// <summary>Starts a left join to a query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> LeftJoin<TJoinEntity>(
            QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().LeftJoin(query, joinCondition);

        /// <summary>Starts a right join to the repository's mapped source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(
            IRepository<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> predicate, Action<JoinOptions>? options = null) =>
            repository.Query().RightJoin(repository.DataContext.From(source), predicate, options);

        /// <summary>Starts a right join to a builder source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(
            EntityBuilder<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().RightJoin(source, joinCondition);

        /// <summary>Starts a right join to a mapped entity type.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(
            Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().RightJoin(joinCondition);

        /// <summary>Starts a right join to the named table.</summary>
        public JoinedEntityBuilder<TEntity, TableAlias> RightJoin(
            string table, Expression<Func<TEntity, TableAlias, bool>> joinCondition) =>
            repository.Query().RightJoin(table, joinCondition);

        /// <summary>Starts a right join to a common table expression.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(
            Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().RightJoin(cte, joinCondition);

        /// <summary>Starts a right join to a query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> RightJoin<TJoinEntity>(
            QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().RightJoin(query, joinCondition);

        /// <summary>Starts a full outer join to the repository's mapped source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(
            IRepository<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> predicate, Action<JoinOptions>? options = null) =>
            repository.Query().FullJoin(repository.DataContext.From(source), predicate, options);

        /// <summary>Starts a full outer join to a builder source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(
            EntityBuilder<TJoinEntity> source, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().FullJoin(source, joinCondition);

        /// <summary>Starts a full outer join to a mapped entity type.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(
            Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().FullJoin(joinCondition);

        /// <summary>Starts a full outer join to the named table.</summary>
        public JoinedEntityBuilder<TEntity, TableAlias> FullJoin(
            string table, Expression<Func<TEntity, TableAlias, bool>> joinCondition) =>
            repository.Query().FullJoin(table, joinCondition);

        /// <summary>Starts a full outer join to a common table expression.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(
            Cte<TJoinEntity> cte, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().FullJoin(cte, joinCondition);

        /// <summary>Starts a full outer join to a query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> FullJoin<TJoinEntity>(
            QueryCommand<TJoinEntity> query, Expression<Func<TEntity, TJoinEntity, bool>> joinCondition) =>
            repository.Query().FullJoin(query, joinCondition);

        /// <summary>Starts a cross join to the repository's mapped source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(
            IRepository<TJoinEntity> source, Action<JoinOptions>? options = null) =>
            repository.Query().CrossJoin(repository.DataContext.From(source), options);

        /// <summary>Starts a cross join to a builder source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(
            EntityBuilder<TJoinEntity> source) =>
            repository.Query().CrossJoin(source);

        /// <summary>Starts a cross join to a mapped entity type.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>() =>
            repository.Query().CrossJoin<TJoinEntity>();

        /// <summary>Starts a cross join to the named table.</summary>
        public JoinedEntityBuilder<TEntity, TableAlias> CrossJoin(string table) =>
            repository.Query().CrossJoin(table);

        /// <summary>Starts a cross join to a common table expression.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(Cte<TJoinEntity> cte) =>
            repository.Query().CrossJoin(cte);

        /// <summary>Starts a cross join to a query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossJoin<TJoinEntity>(
            QueryCommand<TJoinEntity> query) =>
            repository.Query().CrossJoin(query);

        /// <summary>Starts a cross apply of the repository's mapped source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(
            IRepository<TJoinEntity> source, Action<JoinOptions>? options = null) =>
            repository.Query().CrossApply(repository.DataContext.From(source), options);

        /// <summary>Starts a cross apply of a builder source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(
            EntityBuilder<TJoinEntity> source) =>
            repository.Query().CrossApply(source);

        /// <summary>Starts a cross apply of a mapped entity type.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>() =>
            repository.Query().CrossApply<TJoinEntity>();

        /// <summary>Starts a cross apply of the named table.</summary>
        public JoinedEntityBuilder<TEntity, TableAlias> CrossApply(string table) =>
            repository.Query().CrossApply(table);

        /// <summary>Starts a cross apply of a common table expression.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(Cte<TJoinEntity> cte) =>
            repository.Query().CrossApply(cte);

        /// <summary>Starts a cross apply of a query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(
            QueryCommand<TJoinEntity> query) =>
            repository.Query().CrossApply(query);

        /// <summary>Starts a cross apply of a correlated builder query.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(
            Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source) =>
            repository.Query().CrossApply(source);

        /// <summary>Starts a cross apply of a correlated query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> CrossApply<TJoinEntity>(
            Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source) =>
            repository.Query().CrossApply(source);

        /// <summary>Starts an outer apply of the repository's mapped source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(
            IRepository<TJoinEntity> source, Action<JoinOptions>? options = null) =>
            repository.Query().OuterApply(repository.DataContext.From(source), options);

        /// <summary>Starts an outer apply of a builder source.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(
            EntityBuilder<TJoinEntity> source) =>
            repository.Query().OuterApply(source);

        /// <summary>Starts an outer apply of a mapped entity type.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>() =>
            repository.Query().OuterApply<TJoinEntity>();

        /// <summary>Starts an outer apply of the named table.</summary>
        public JoinedEntityBuilder<TEntity, TableAlias> OuterApply(string table) =>
            repository.Query().OuterApply(table);

        /// <summary>Starts an outer apply of a common table expression.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(Cte<TJoinEntity> cte) =>
            repository.Query().OuterApply(cte);

        /// <summary>Starts an outer apply of a query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(
            QueryCommand<TJoinEntity> query) =>
            repository.Query().OuterApply(query);

        /// <summary>Starts an outer apply of a correlated builder query.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(
            Expression<Func<TEntity, EntityBuilder<TJoinEntity>>> source) =>
            repository.Query().OuterApply(source);

        /// <summary>Starts an outer apply of a correlated query command.</summary>
        public JoinedEntityBuilder<TEntity, TJoinEntity> OuterApply<TJoinEntity>(
            Expression<Func<TEntity, QueryCommand<TJoinEntity>>> source) =>
            repository.Query().OuterApply(source);

        /// <summary>Starts a query that groups matching inner elements for each outer row.</summary>
        public EntityBuilder<TResult> GroupJoin<TInner, TKey, TResult>(
            EntityBuilder<TInner> inner, Expression<Func<TEntity, TKey>> outerKeySelector,
            Expression<Func<TInner, TKey>> innerKeySelector,
            Expression<Func<TEntity, IEnumerable<TInner>, TResult>> resultSelector) =>
            repository.Query().GroupJoin(inner, outerKeySelector, innerKeySelector, resultSelector);
    }
}
