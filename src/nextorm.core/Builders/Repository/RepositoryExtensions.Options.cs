namespace NextORM.Core;

public static partial class RepositoryExtensions
{
    extension<TEntity>(IRepository<TEntity> repository)
    {
        /// <summary>Starts a query using the given mapping variant.</summary>
        /// <param name="variant">The mapping variant to use for the query.</param>
        /// <returns>A query builder for the repository's source.</returns>
        public EntityBuilder<TEntity> Query(MappingVariant variant) => repository.Query().CopyWithMappingVariant(variant);

        /// <summary>Starts a query routed by the given shard key values.</summary>
        public EntityBuilder<TEntity> WithShardKey(params object?[] shardKeyValues) =>
            repository.Query().WithShardKey(shardKeyValues);

        /// <summary>Starts a query over all shard buckets.</summary>
        public ShardQuery<TEntity> AcrossBuckets(int? parallelism = null) => repository.Query().AcrossBuckets(parallelism);

        /// <summary>Starts a query over the requested temporal range or point in time.</summary>
        public EntityBuilder<TEntity> ForSystemTime(TemporalClause clause) => repository.Query().ForSystemTime(clause);

        /// <summary>Starts a query that locks selected rows for update.</summary>
        public EntityBuilder<TEntity> ForUpdate() => repository.Query().ForUpdate();

        /// <summary>Starts a query with update locks and the given lock wait behavior.</summary>
        public EntityBuilder<TEntity> ForUpdate(LockWaitMode wait) => repository.Query().ForUpdate(wait);

        /// <summary>Starts a query that takes shared locks on selected rows.</summary>
        public EntityBuilder<TEntity> ForShare() => repository.Query().ForShare();

        /// <summary>Starts a query with shared locks and the given lock wait behavior.</summary>
        public EntityBuilder<TEntity> ForShare(LockWaitMode wait) => repository.Query().ForShare(wait);

        /// <summary>Starts a query with the given query component.</summary>
        public EntityBuilder<TEntity> WithComponent(IQueryComponent component) => repository.Query().WithComponent(component);

        /// <summary>Starts a query with hints on its tables. Only pass trusted hint text.</summary>
        public EntityBuilder<TEntity> WithTablesInScopeHint(params string[] hints) =>
            repository.Query().WithTablesInScopeHint(hints);

        /// <summary>Starts a query with an override for SQL identifier quoting.</summary>
        public EntityBuilder<TEntity> WithQuotedIdentifiers(bool value = true) => repository.Query().WithQuotedIdentifiers(value);

        /// <summary>Starts a query with an override for the SQL naming convention.</summary>
        public EntityBuilder<TEntity> WithNamingConvention(INamingConvention? convention) =>
            repository.Query().WithNamingConvention(convention);

        /// <summary>Starts a query with an override for SQL keyword casing.</summary>
        public EntityBuilder<TEntity> WithKeywordCase(KeywordCase keywordCase = KeywordCase.Upper) =>
            repository.Query().WithKeywordCase(keywordCase);

        /// <summary>Starts a query with an override for uppercase SQL keywords.</summary>
        public EntityBuilder<TEntity> WithUppercaseKeywords(bool value = true) =>
            repository.Query().WithUppercaseKeywords(value);

        /// <summary>Starts a tagged query.</summary>
        public EntityBuilder<TEntity> WithTag(string? tag) => repository.Query().WithTag(tag);

        /// <summary>Starts a query with a command timeout in seconds.</summary>
        public EntityBuilder<TEntity> WithCommandTimeout(int seconds) => repository.Query().WithCommandTimeout(seconds);

        /// <summary>Starts a query with an override for the mapped table name.</summary>
        public EntityBuilder<TEntity> WithTableName(string name) => repository.Query().WithTableName(name);

        /// <summary>Starts a query with an override for the mapped schema.</summary>
        public EntityBuilder<TEntity> WithSchema(string schema) => repository.Query().WithSchema(schema);

        /// <summary>Starts a query with an override for the mapped database.</summary>
        public EntityBuilder<TEntity> WithDatabase(string database) => repository.Query().WithDatabase(database);

        /// <summary>Starts a query with an override for the mapped server.</summary>
        public EntityBuilder<TEntity> WithServer(string server) => repository.Query().WithServer(server);

        /// <summary>Starts a query over a raw table expression. Only pass trusted SQL.</summary>
        public EntityBuilder<TEntity> WithTableExpression(string sql) => repository.Query().WithTableExpression(sql);
    }
}
