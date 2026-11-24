using System.Linq.Expressions;

namespace NextORM.Core;

public static partial class RepositoryExtensions
{
    extension<TEntity>(IRepository<TEntity> repository)
    {
        /// <summary>Starts a query that loads a collection navigation using its mapped relationship.</summary>
        public EntityBuilder<TEntity> LoadWith<TChild>(Expression<Func<TEntity, ICollection<TChild>>> collection) =>
            repository.Query().LoadWith(collection);

        /// <summary>Starts a query that loads a collection navigation using the given loading mode.</summary>
        public EntityBuilder<TEntity> LoadWith<TChild>(
            Expression<Func<TEntity, ICollection<TChild>>> collection, EagerLoadMode mode) =>
            repository.Query().LoadWith(collection, mode);

        /// <summary>Starts a query that loads a collection navigation from a custom child query.</summary>
        public EntityBuilder<TEntity> LoadWith<TChild>(Expression<Func<TEntity, ICollection<TChild>>> collection,
            Func<IDataContext, EntityBuilder<TChild>> childQuery, EagerLoadMode mode) =>
            repository.Query().LoadWith(collection, childQuery, mode);

        /// <summary>Starts a query that loads a collection navigation using a child query and explicit keys.</summary>
        public EntityBuilder<TEntity> LoadWith<TChild, TKey>(Expression<Func<TEntity, ICollection<TChild>>> collection,
            Func<IDataContext, EntityBuilder<TChild>> childQuery, Expression<Func<TEntity, TKey>> parentKey,
            Expression<Func<TChild, TKey>> childKey, EagerLoadMode mode)
            where TKey : notnull =>
            repository.Query().LoadWith(collection, childQuery, parentKey, childKey, mode);

        /// <summary>Starts a query that loads a reference navigation using its mapped relationship.</summary>
        public EntityBuilder<TEntity> LoadWith<TChild>(Expression<Func<TEntity, TChild?>> reference)
            where TChild : class =>
            repository.Query().LoadWith(reference);

        /// <summary>Starts a query that loads a reference navigation using the given loading mode.</summary>
        public EntityBuilder<TEntity> LoadWith<TChild>(
            Expression<Func<TEntity, TChild?>> reference, EagerLoadMode mode)
            where TChild : class =>
            repository.Query().LoadWith(reference, mode);

        /// <summary>Starts a query that loads a reference navigation from a custom child query.</summary>
        public EntityBuilder<TEntity> LoadWith<TChild>(Expression<Func<TEntity, TChild?>> reference,
            Func<IDataContext, EntityBuilder<TChild>> childQuery, EagerLoadMode mode)
            where TChild : class =>
            repository.Query().LoadWith(reference, childQuery, mode);

        /// <summary>Starts a query that joins children into a collection using its mapped relationship.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(Expression<Func<TEntity, ICollection<TChild>>> collection) =>
            repository.Query().JoinInto(collection);

        /// <summary>Starts a query that joins and filters children using the collection's mapped relationship.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(Expression<Func<TEntity, ICollection<TChild>>> collection,
            Expression<Func<TEntity, TChild, bool>>? filter, Action<JoinOptions>? options) =>
            repository.Query().JoinInto(collection, filter, options);

        /// <summary>Starts a query that joins children into a collection using an explicit predicate.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, ICollection<TChild>>> collection,
            Action<JoinOptions>? options) =>
            repository.Query().JoinInto(predicate, collection, options);

        /// <summary>Starts a query that joins children into a collection using the given join type.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, ICollection<TChild>>> collection,
            JoinType joinType, Action<JoinOptions>? options) =>
            repository.Query().JoinInto(predicate, collection, joinType, options);

        /// <summary>Starts a query that joins a custom child source into a collection.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(EntityBuilder<TChild> child,
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, ICollection<TChild>>> collection,
            Action<JoinOptions>? options) =>
            repository.Query().JoinInto(child, predicate, collection, options);

        /// <summary>Starts a query that joins a custom child source into a collection using the given join type.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(EntityBuilder<TChild> child,
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, ICollection<TChild>>> collection,
            JoinType joinType, Action<JoinOptions>? options) =>
            repository.Query().JoinInto(child, predicate, collection, joinType, options);

        /// <summary>Starts a query that joins children into a collection using explicit keys.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild, TKey>(
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, ICollection<TChild>>> collection,
            Expression<Func<TEntity, TKey>> parentKey, Expression<Func<TChild, TKey>> childKey,
            Action<JoinOptions>? options)
            where TKey : notnull =>
            repository.Query().JoinInto(predicate, collection, parentKey, childKey, options);

        /// <summary>Starts a query that joins a custom child source into a collection using explicit keys.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild, TKey>(EntityBuilder<TChild> child,
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, ICollection<TChild>>> collection,
            Expression<Func<TEntity, TKey>> parentKey, Expression<Func<TChild, TKey>> childKey,
            Action<JoinOptions>? options)
            where TKey : notnull =>
            repository.Query().JoinInto(child, predicate, collection, parentKey, childKey, options);

        /// <summary>Starts a query that joins a child into a reference using its mapped relationship.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(Expression<Func<TEntity, TChild?>> navigation)
            where TChild : class =>
            repository.Query().JoinInto(navigation);

        /// <summary>Starts a query that joins and filters a child using the navigation's mapped relationship.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(Expression<Func<TEntity, TChild?>> navigation,
            Expression<Func<TEntity, TChild, bool>>? filter, Action<JoinOptions>? options)
            where TChild : class =>
            repository.Query().JoinInto(navigation, filter, options);

        /// <summary>Starts a query that joins a child into a reference using an explicit predicate.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, TChild?>> navigation,
            Action<JoinOptions>? options)
            where TChild : class =>
            repository.Query().JoinInto(predicate, navigation, options);

        /// <summary>Starts a query that joins a child into a reference using the given join type.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, TChild?>> navigation,
            JoinType joinType, Action<JoinOptions>? options)
            where TChild : class =>
            repository.Query().JoinInto(predicate, navigation, joinType, options);

        /// <summary>Starts a query that joins a custom child source into a reference.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(EntityBuilder<TChild> child,
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, TChild?>> navigation,
            Action<JoinOptions>? options)
            where TChild : class =>
            repository.Query().JoinInto(child, predicate, navigation, options);

        /// <summary>Starts a query that joins a custom child source into a reference using the given join type.</summary>
        public EntityBuilder<TEntity> JoinInto<TChild>(EntityBuilder<TChild> child,
            Expression<Func<TEntity, TChild, bool>> predicate, Expression<Func<TEntity, TChild?>> navigation,
            JoinType joinType, Action<JoinOptions>? options)
            where TChild : class =>
            repository.Query().JoinInto(child, predicate, navigation, joinType, options);
    }
}
