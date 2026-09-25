using System.Linq.Expressions;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Provider-independent helpers and convenience overloads for <see cref="IDataContext"/>.
/// These used to be default interface methods; keeping them outside the interface leaves the
/// provider-implemented contract (the role interfaces) free of engine-independent behavior.
/// </summary>
public static class DataContextExtensions
{
    /// <summary>
    /// Creates a typed query command from a <see cref="QueryDefinition"/>. This is the single factory
    /// the builders use; it replaces the previous overloads that took the whole query shape as a long
    /// parameter list.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static QueryCommand<T> CreateCommand<T>(this IDataContext dataContext, QueryDefinition definition)
        => new(dataContext, definition);

    /// <summary>
    /// Starts an <c>INSERT</c> over the mapping of <typeparamref name="TEntity"/> and returns its fluent
    /// builder. The type's metadata is resolved lazily and cached per process, exactly like
    /// <c>From&lt;T&gt;()</c>; <paramref name="configEntity"/> therefore runs only on the first call for
    /// <typeparamref name="TEntity"/> (declare keys/identity/computed columns there or with attributes).
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type to insert.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the insert.</returns>
    public static InsertBuilder<TEntity> InsertInto<TEntity>(this IDataContext dataContext, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        return new(dataContext, ResolveMetadata(configEntity));
    }

    /// <summary>
    /// Starts a bulk insert over the mapping of <typeparamref name="TEntity"/> and returns its fluent
    /// builder. The type's metadata is resolved lazily and cached per process, exactly like
    /// <see cref="InsertInto{TEntity}"/>; <paramref name="configEntity"/> therefore runs only on the first
    /// call for <typeparamref name="TEntity"/>. The write uses the provider's native bulk API where one
    /// exists and a chunked parameterised <c>INSERT ... VALUES</c> otherwise.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type to write.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the bulk insert.</returns>
    public static BulkInsertBuilder<TEntity> BulkInsertInto<TEntity>(this IDataContext dataContext, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        return new(dataContext, ResolveMetadata(configEntity), new BulkInsertOptions());
    }

    /// <summary>
    /// Starts a bulk insert with explicit <see cref="BulkInsertOptions"/>. The type's metadata is resolved
    /// lazily and cached per process, exactly like <see cref="InsertInto{TEntity}"/>; the write uses the
    /// provider's native bulk API where one exists and a chunked parameterised <c>INSERT ... VALUES</c>
    /// otherwise. The options shape the statement (batch limits, identity, conflict handling, timeout and
    /// progress).
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type to write.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="options">The write options.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the bulk insert.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">An option value is not positive.</exception>
    public static BulkInsertBuilder<TEntity> BulkInsertInto<TEntity>(this IDataContext dataContext, BulkInsertOptions options, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(options);

        return new(dataContext, ResolveMetadata(configEntity), options);
    }

    /// <summary>
    /// Starts a bulk insert configured with the fluent <see cref="BulkInsertOptionsBuilder"/>. The type's
    /// metadata is resolved lazily and cached per process, exactly like <see cref="InsertInto{TEntity}"/>.
    /// The callback must be an expression lambda that returns the builder (for example
    /// <c>o =&gt; o.MaxBatchSize(1_000)</c>), which keeps it distinct from the
    /// <see cref="EntityMetadataBuilder{TEntity}"/> overload.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type to write.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="configure">Configures the write options; the returned builder is ignored.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the bulk insert.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="configure"/> is <see langword="null"/>.</exception>
    public static BulkInsertBuilder<TEntity> BulkInsertInto<TEntity>(this IDataContext dataContext, Func<BulkInsertOptionsBuilder, BulkInsertOptionsBuilder> configure, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(configure);

        var options = new BulkInsertOptionsBuilder();
        configure(options);

        return new(dataContext, ResolveMetadata(configEntity), options.Build());
    }

    /// <summary>
    /// Starts a key upsert over the mapping of <typeparamref name="TEntity"/> and returns its fluent
    /// builder. The source row set is supplied with <c>Using</c> and the match key with <c>OnKeys</c>;
    /// the statement is rendered as <c>INSERT ... ON CONFLICT ... DO UPDATE</c>,
    /// <c>INSERT ... ON DUPLICATE KEY UPDATE</c> or <c>MERGE</c> depending on the provider. Like
    /// <see cref="InsertInto{TEntity}"/>, the type's metadata is resolved lazily and cached per process,
    /// so <paramref name="configEntity"/> runs only on the first call for <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type upserted.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the key upsert.</returns>
    public static MergeBuilder<TEntity> MergeInto<TEntity>(this IDataContext dataContext, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        return new(dataContext, ResolveMetadata(configEntity));
    }

    /// <summary>
    /// Starts an <c>UPDATE</c> over the mapping of <typeparamref name="TEntity"/> and returns its fluent
    /// builder. Add the written columns with <c>Set</c> and the row filter with <c>Where</c>; omitting
    /// <c>Where</c> updates every row. Like <see cref="InsertInto{TEntity}"/>, the type's metadata is
    /// resolved lazily and cached per process, so <paramref name="configEntity"/> runs only on the first
    /// call for <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type to update.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the update.</returns>
    public static UpdateBuilder<TEntity> Update<TEntity>(this IDataContext dataContext, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        return new(dataContext, ResolveMetadata(configEntity));
    }

    /// <summary>
    /// Updates the row identified by the declared key of <paramref name="entity"/> in one command
    /// (<c>UPDATE ... SET ... WHERE &lt;pk&gt; = @p</c>), writing every non-key, non-identity,
    /// non-computed column. Uses the mapping's <c>[Key]</c>/<c>.Key()</c>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="entity">The entity whose key identifies the row and whose values are written.</param>
    /// <returns>The number of updated rows (0 or 1).</returns>
    /// <exception cref="InvalidOperationException">The entity type declares no key.</exception>
    /// <exception cref="NotSupportedException">The context does not support data modification (the in-memory provider), or the provider cannot express <c>UPDATE</c> (ClickHouse).</exception>
    public static int Update<TEntity>(this IDataContext dataContext, TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(entity);

        return new UpdateBuilder<TEntity>(dataContext, ResolveMetadata<TEntity>(null)).UpdateEntity(entity);
    }

    /// <summary>
    /// Asynchronously updates the row identified by the declared key of <paramref name="entity"/> in one
    /// command, using the mapping's <c>[Key]</c>/<c>.Key()</c>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="entity">The entity whose key identifies the row and whose values are written.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of updated rows (0 or 1).</returns>
    /// <exception cref="InvalidOperationException">The entity type declares no key.</exception>
    /// <exception cref="NotSupportedException">The context does not support data modification (the in-memory provider), or the provider cannot express <c>UPDATE</c> (ClickHouse).</exception>
    public static Task<int> UpdateAsync<TEntity>(this IDataContext dataContext, TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(entity);

        return new UpdateBuilder<TEntity>(dataContext, ResolveMetadata<TEntity>(null)).UpdateEntityAsync(entity, cancellationToken);
    }

    internal static IEntityMetadata ResolveMetadata<TEntity>(Action<EntityMetadataBuilder<TEntity>>? configEntity)
    {
        if (!DataContextCache.Metadata.TryGetValue(typeof(TEntity), out var metadata) || string.IsNullOrEmpty(metadata.TableName))
        {
            var eb = new EntityMetadataBuilder<TEntity>();
            configEntity?.Invoke(eb);
            metadata = eb.Build();
            DataContextCache.Metadata[typeof(TEntity)] = metadata;
        }

        return metadata;
    }

    /// <summary>
    /// Starts a <c>DELETE</c> over the mapping of <typeparamref name="TEntity"/> and returns its fluent
    /// builder. The type's metadata is resolved lazily and cached per process, exactly like
    /// <c>From&lt;T&gt;()</c>; <paramref name="configEntity"/> therefore runs only on the first call for
    /// <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type whose rows are deleted.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the delete.</returns>
    public static DeleteBuilder<TEntity> DeleteFrom<TEntity>(this IDataContext dataContext, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        return new(dataContext, ResolveMetadata(configEntity));
    }

    /// <summary>
    /// Deletes the row identified by the declared key of <paramref name="entity"/> in one command
    /// (<c>DELETE FROM ... WHERE &lt;pk&gt; = @p</c>), using the mapping's <c>[Key]</c>/<c>.Key()</c>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="entity">The entity whose key identifies the row to delete.</param>
    /// <returns>The number of deleted rows (0 or 1).</returns>
    /// <exception cref="InvalidOperationException">The entity type declares no key.</exception>
    public static int Delete<TEntity>(this IDataContext dataContext, TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(entity);

        return new DeleteBuilder<TEntity>(dataContext, ResolveMetadata<TEntity>(null)).DeleteEntity(entity);
    }

    /// <summary>
    /// Asynchronously deletes the row identified by the declared key of <paramref name="entity"/> in one
    /// command, using the mapping's <c>[Key]</c>/<c>.Key()</c>.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="entity">The entity whose key identifies the row to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows (0 or 1).</returns>
    /// <exception cref="InvalidOperationException">The entity type declares no key.</exception>
    public static Task<int> DeleteAsync<TEntity>(this IDataContext dataContext, TEntity entity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(entity);

        return new DeleteBuilder<TEntity>(dataContext, ResolveMetadata<TEntity>(null)).DeleteEntityAsync(entity, cancellationToken);
    }

    /// <summary>
    /// Deletes every row of the first table of a join that matches the join — a native multi-table
    /// <c>DELETE</c>. The join condition (and any <c>Where</c> added to the joined builder) selects the
    /// rows to remove. Only INNER <c>Join</c> joins are supported; outer/cross joins are rejected because
    /// they change which rows are deleted. PostgreSQL renders <c>DELETE ... USING</c>; SQL Server, MySQL
    /// and MariaDB render <c>DELETE &lt;alias&gt; FROM ... JOIN</c>. SQLite, ClickHouse and the in-memory
    /// provider throw <see cref="NotSupportedException"/>.
    /// </summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted (the first table).</typeparam>
    /// <typeparam name="T2">The joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The number of deleted rows, as reported by the provider.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c> (SQLite, ClickHouse, in-memory), or a join is not an INNER <c>Join</c>.</exception>
    public static int Delete<T1, T2>(this JoinedEntityBuilder<T1, T2> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Asynchronously deletes the rows of the first table matching the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static Task<int> DeleteAsync<T1, T2>(this JoinedEntityBuilder<T1, T2> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoinAsync(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)), cancellationToken);
    }

    /// <summary>Renders the multi-table <c>DELETE</c> the joined query would execute, without executing it. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static string ToSql<T1, T2>(this JoinedEntityBuilder<T1, T2> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RenderDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Deletes every row of the first table of a three-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static int Delete<T1, T2, T3>(this JoinedEntityBuilder<T1, T2, T3> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Asynchronously deletes the rows of the first table of a three-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static Task<int> DeleteAsync<T1, T2, T3>(this JoinedEntityBuilder<T1, T2, T3> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoinAsync(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)), cancellationToken);
    }

    /// <summary>Renders the multi-table <c>DELETE</c> a three-table join would execute. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static string ToSql<T1, T2, T3>(this JoinedEntityBuilder<T1, T2, T3> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RenderDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Deletes every row of the first table of a four-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static int Delete<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3, T4> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Asynchronously deletes the rows of the first table of a four-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static Task<int> DeleteAsync<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3, T4> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoinAsync(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)), cancellationToken);
    }

    /// <summary>Renders the multi-table <c>DELETE</c> a four-table join would execute. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static string ToSql<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3, T4> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RenderDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Deletes every row of the first table of a five-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static int Delete<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Asynchronously deletes the rows of the first table of a five-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static Task<int> DeleteAsync<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoinAsync(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)), cancellationToken);
    }

    /// <summary>Renders the multi-table <c>DELETE</c> a five-table join would execute. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static string ToSql<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RenderDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Deletes every row of the first table of a six-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static int Delete<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Asynchronously deletes the rows of the first table of a six-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static Task<int> DeleteAsync<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoinAsync(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)), cancellationToken);
    }

    /// <summary>Renders the multi-table <c>DELETE</c> a six-table join would execute. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static string ToSql<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RenderDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Deletes every row of the first table of a seven-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static int Delete<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Asynchronously deletes the rows of the first table of a seven-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static Task<int> DeleteAsync<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoinAsync(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)), cancellationToken);
    }

    /// <summary>Renders the multi-table <c>DELETE</c> a seven-table join would execute. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static string ToSql<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RenderDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Deletes every row of the first table of an eight-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <typeparam name="T8">The eighth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static int Delete<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>Asynchronously deletes the rows of the first table of an eight-table join that matches the join. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <typeparam name="T8">The eighth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static Task<int> DeleteAsync<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return ExecuteDeleteJoinAsync(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)), cancellationToken);
    }

    /// <summary>Renders the multi-table <c>DELETE</c> an eight-table join would execute. See <see cref="Delete{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are deleted.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <typeparam name="T8">The eighth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to delete.</param>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="NotSupportedException">The provider has no native multi-table <c>DELETE</c>, or a join is not an INNER <c>Join</c>.</exception>
    public static string ToSql<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return RenderDeleteJoin(query.DataProvider, new DeleteJoinCommand(typeof(T1), PrepareDeleteJoinSource(query)));
    }

    /// <summary>
    /// Starts a multi-table <c>UPDATE</c> over the join of <paramref name="query"/>: the target is the
    /// first table of the chain and the <c>SET</c> values may read the joined tables. Only INNER
    /// <c>Join</c> joins are supported; outer/cross joins are rejected because folding the join conditions
    /// into the filter would change which rows are updated. PostgreSQL and SQLite render
    /// <c>UPDATE ... FROM</c>; SQL Server renders <c>UPDATE &lt;alias&gt; ... FROM ... JOIN</c>;
    /// MySQL/MariaDB render <c>UPDATE ... JOIN ... SET</c>. ClickHouse and the in-memory provider reject it.
    /// </summary>
    /// <typeparam name="T1">The target entity type whose rows are updated (the first table).</typeparam>
    /// <typeparam name="T2">The joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to update.</param>
    /// <returns>A builder for the assignments and the filter.</returns>
    public static UpdateJoinBuilder<Projection<T1, T2>> UpdateJoin<T1, T2>(this JoinedEntityBuilder<T1, T2> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new UpdateJoinBuilder<Projection<T1, T2>>(query, typeof(T1));
    }

    /// <summary>Starts a multi-table <c>UPDATE</c> over a three-table join. See <see cref="UpdateJoin{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are updated.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to update.</param>
    /// <returns>A builder for the assignments and the filter.</returns>
    public static UpdateJoinBuilder<Projection<T1, T2, T3>> UpdateJoin<T1, T2, T3>(this JoinedEntityBuilder<T1, T2, T3> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new UpdateJoinBuilder<Projection<T1, T2, T3>>(query, typeof(T1));
    }

    /// <summary>Starts a multi-table <c>UPDATE</c> over a four-table join. See <see cref="UpdateJoin{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are updated.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to update.</param>
    /// <returns>A builder for the assignments and the filter.</returns>
    public static UpdateJoinBuilder<Projection<T1, T2, T3, T4>> UpdateJoin<T1, T2, T3, T4>(this JoinedEntityBuilder<T1, T2, T3, T4> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new UpdateJoinBuilder<Projection<T1, T2, T3, T4>>(query, typeof(T1));
    }

    /// <summary>Starts a multi-table <c>UPDATE</c> over a five-table join. See <see cref="UpdateJoin{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are updated.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to update.</param>
    /// <returns>A builder for the assignments and the filter.</returns>
    public static UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5>> UpdateJoin<T1, T2, T3, T4, T5>(this JoinedEntityBuilder<T1, T2, T3, T4, T5> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5>>(query, typeof(T1));
    }

    /// <summary>Starts a multi-table <c>UPDATE</c> over a six-table join. See <see cref="UpdateJoin{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are updated.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to update.</param>
    /// <returns>A builder for the assignments and the filter.</returns>
    public static UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5, T6>> UpdateJoin<T1, T2, T3, T4, T5, T6>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5, T6>>(query, typeof(T1));
    }

    /// <summary>Starts a multi-table <c>UPDATE</c> over a seven-table join. See <see cref="UpdateJoin{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are updated.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to update.</param>
    /// <returns>A builder for the assignments and the filter.</returns>
    public static UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>> UpdateJoin<T1, T2, T3, T4, T5, T6, T7>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5, T6, T7>>(query, typeof(T1));
    }

    /// <summary>Starts a multi-table <c>UPDATE</c> over an eight-table join. See <see cref="UpdateJoin{T1, T2}(JoinedEntityBuilder{T1, T2})"/> for the full contract.</summary>
    /// <typeparam name="T1">The target entity type whose rows are updated.</typeparam>
    /// <typeparam name="T2">The second joined entity type.</typeparam>
    /// <typeparam name="T3">The third joined entity type.</typeparam>
    /// <typeparam name="T4">The fourth joined entity type.</typeparam>
    /// <typeparam name="T5">The fifth joined entity type.</typeparam>
    /// <typeparam name="T6">The sixth joined entity type.</typeparam>
    /// <typeparam name="T7">The seventh joined entity type.</typeparam>
    /// <typeparam name="T8">The eighth joined entity type.</typeparam>
    /// <param name="query">The joined query selecting the rows to update.</param>
    /// <returns>A builder for the assignments and the filter.</returns>
    public static UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5, T6, T7, T8>> UpdateJoin<T1, T2, T3, T4, T5, T6, T7, T8>(this JoinedEntityBuilder<T1, T2, T3, T4, T5, T6, T7, T8> query)
    {
        ArgumentNullException.ThrowIfNull(query);
        return new UpdateJoinBuilder<Projection<T1, T2, T3, T4, T5, T6, T7, T8>>(query, typeof(T1));
    }

    private static QueryCommand PrepareDeleteJoinSource<TProjection>(EntityBuilder<TProjection> query)
        => JoinedMutationSource.Prepare(query, "DELETE");

    private static int ExecuteDeleteJoin(IDataContext dataContext, DeleteJoinCommand command)
    {
        if (dataContext is IMutationExecutor executor)
            return executor.Execute(command);

        throw UnsupportedDeleteJoin(dataContext);
    }

    private static Task<int> ExecuteDeleteJoinAsync(IDataContext dataContext, DeleteJoinCommand command, CancellationToken cancellationToken)
    {
        if (dataContext is IMutationExecutor executor)
            return executor.Execute(command, cancellationToken);

        throw UnsupportedDeleteJoin(dataContext);
    }

    private static string RenderDeleteJoin(IDataContext dataContext, DeleteJoinCommand command)
    {
        if (dataContext is IMutationExecutor executor)
            return executor.Render(command);

        throw UnsupportedDeleteJoin(dataContext);
    }

    private static NotSupportedException UnsupportedDeleteJoin(IDataContext dataContext)
        => new(
            $"{dataContext.GetType().Name} does not support deleting from a joined table. Use PostgreSQL, SQL Server, MySQL or MariaDB, or delete the rows through a correlated subquery (Where with EXISTS/IN).");

    /// <summary>
    /// Starts a <c>TRUNCATE TABLE</c> over the mapping of <typeparamref name="TEntity"/> and returns its
    /// terminal. Resets the table faster than <c>DeleteFrom&lt;T&gt;().All()</c>; providers without a
    /// native <c>TRUNCATE</c> (SQLite) reject it.
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type whose table is truncated.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for the truncate.</returns>
    public static TruncateBuilder<TEntity> Truncate<TEntity>(this IDataContext dataContext, Action<EntityMetadataBuilder<TEntity>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);

        return new(dataContext, ResolveMetadata(configEntity));
    }

    /// <summary>
    /// Starts a query over the mapping of <typeparamref name="T"/> and returns its fluent builder.
    /// The type's metadata is resolved lazily and cached per process; <paramref name="configEntity"/>
    /// therefore runs only on the first call for <typeparamref name="T"/>.
    /// </summary>
    public static EntityBuilder<T> From<T>(this IDataContext dataContext, Action<EntityMetadataBuilder<T>>? configEntity = null)
    {
        _ = ResolveMetadata(configEntity);

        return new(dataContext) { Logger = dataContext.CommandLogger };
    }

    /// <summary>
    /// Starts a query over the mapping of <typeparamref name="T"/> with per-query source options. The
    /// values set on <paramref name="options"/> (a <c>TABLESAMPLE</c> percentage or a ClickHouse
    /// <c>SAMPLE</c> ratio) are copied into the returned builder; the options object is not retained.
    /// The type's metadata is resolved exactly like <see cref="From{T}(IDataContext, Action{EntityMetadataBuilder{T}}?)"/>.
    /// </summary>
    /// <typeparam name="T">The mapped entity type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="options">Configures the primary source, for example <c>o =&gt; o.TableSample(10)</c>.</param>
    /// <param name="configEntity">Optional mapping configuration, run only when the type is first mapped.</param>
    /// <returns>A builder for composing the query.</returns>
    public static EntityBuilder<T> From<T>(this IDataContext dataContext, Action<FromOptions> options, Action<EntityMetadataBuilder<T>>? configEntity = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(options);

        var fromOptions = new FromOptions();
        options(fromOptions);

        _ = ResolveMetadata(configEntity);

        return new(dataContext)
        {
            Logger = dataContext.CommandLogger,
            TableSampleClause = fromOptions.TableSampleClause,
            SampleRatio = fromOptions.SampleRatio,
            SampleOffset = fromOptions.SampleOffset,
        };
    }

    /// <summary>
    /// Asynchronously determines whether the query produces at least one row. Stops after the first
    /// matching row rather than materializing the full result.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, object[]? @params, CancellationToken cancellationToken)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true, cancellationToken);

    /// <summary>
    /// Asynchronously determines whether the query produces at least one row, using no parameters.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="cancellationToken">Token used to cancel the operation.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, CancellationToken cancellationToken)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, null, true, cancellationToken);

    /// <summary>
    /// Asynchronously determines whether the query produces at least one row.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<bool> AnyAsync(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, params object[]? @params)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true, CancellationToken.None);

    /// <summary>
    /// Synchronously determines whether the query produces at least one row.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to evaluate.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns><see langword="true"/> when at least one row matches; otherwise <see langword="false"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool Any(this IDataContext dataContext, IPreparedQueryCommand<bool> preparedQueryCommand, params ReadOnlySpan<object?> @params)
        => dataContext.ExecuteScalar<bool>(preparedQueryCommand, @params, true);

    /// <summary>
    /// Asynchronously executes the query and materializes every row into a new list.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>A list containing the materialized rows; empty when the query matches nothing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<List<TResult>> ToListAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.ToListAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Synchronously executes the query and materializes every row into a new list, using no parameters.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <returns>A list containing the materialized rows; empty when the query matches nothing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static List<TResult> ToList<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand)
        => dataContext.ToList(preparedQueryCommand, ReadOnlySpan<object?>.Empty);

    /// <summary>
    /// Asynchronously returns the first row of the query.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The first materialized row.</returns>
    /// <exception cref="InvalidOperationException">The query returns no rows.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult> FirstAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.FirstAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the first row of the query, or the default value of
    /// <typeparamref name="TResult"/> when the query returns no rows.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The first materialized row, or <see langword="default"/> when there are none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> FirstOrDefaultAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.FirstOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the only row of the query.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The single materialized row.</returns>
    /// <exception cref="InvalidOperationException">The query returns no rows or more than one row.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult> SingleAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.SingleAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Asynchronously returns the only row of the query, or the default value of
    /// <typeparamref name="TResult"/> when the query returns no rows.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="preparedQueryCommand">The prepared query to execute.</param>
    /// <param name="params">Positional parameter values, in the order the SQL references them.</param>
    /// <returns>The single materialized row, or <see langword="default"/> when there are none.</returns>
    /// <exception cref="InvalidOperationException">The query returns more than one row.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Task<TResult?> SingleOrDefaultAsync<TResult>(this IDataContext dataContext, IPreparedQueryCommand<TResult> preparedQueryCommand, params object[]? @params)
        => dataContext.SingleOrDefaultAsync(preparedQueryCommand, @params, CancellationToken.None);

    /// <summary>
    /// Starts a query against a raw table (or CTE) name. Needed when the context is used through
    /// <see cref="IDataContext"/> and therefore has no concrete <c>From(string)</c> instance method.
    /// <para>
    /// Columns are read through <see cref="TableAlias"/> accessors
    /// (<c>t.GetInt64("id")</c> / <c>t["id"].AsInt</c>). The returned builder is generic so that the
    /// full operator set (<c>Where</c>/<c>Join</c>/<c>GroupBy</c>/<c>Having</c>/<c>OrderBy</c>/
    /// <c>Limit</c>/<c>Select</c>) is available, unlike the previous non-generic shape.
    /// </para>
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TableAlias> From(this IDataContext dataContext, string table)
        => new(dataContext, table) { Logger = dataContext.CommandLogger };

    /// <summary>
    /// Starts a query against a raw table (or CTE) name with per-query source options (a
    /// <c>TABLESAMPLE</c> percentage or a ClickHouse <c>SAMPLE</c> ratio). Columns are read through
    /// <see cref="TableAlias"/> accessors.
    /// </summary>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="table">The table or CTE name.</param>
    /// <param name="options">Configures the primary source, for example <c>o =&gt; o.TableSample(10)</c>.</param>
    /// <returns>A builder for composing the query.</returns>
    public static EntityBuilder<TableAlias> From(this IDataContext dataContext, string table, Action<FromOptions> options)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(table);
        ArgumentNullException.ThrowIfNull(options);

        var fromOptions = new FromOptions();
        options(fromOptions);

        return new(dataContext, table)
        {
            Logger = dataContext.CommandLogger,
            TableSampleClause = fromOptions.TableSampleClause,
            SampleRatio = fromOptions.SampleRatio,
            SampleOffset = fromOptions.SampleOffset,
        };
    }

    /// <summary>
    /// Starts a query from a raw SQL fragment used as a composable <c>FROM</c> source: it is rendered as
    /// <c>(&lt;sql&gt;) AS alias</c> and can be filtered, joined, projected, grouped and paged further.
    /// Columns are read through <see cref="TableAlias"/> accessors (<c>t["id"].AsInt</c>).
    /// <paramref name="parameters"/> is an object whose public properties become the named parameters
    /// referenced by the SQL (the same convention as <c>WithSql</c>). Requires a provider that supports
    /// a derived-table <c>FROM</c> source (<see cref="ISqlDialect.SupportsRawSqlSource"/>).
    /// </summary>
    /// <exception cref="NotSupportedException">
    /// The active provider reports <see cref="ISqlDialect.SupportsRawSqlSource"/> as <c>false</c>, or the
    /// context is the in-memory provider (which cannot evaluate raw SQL).
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TableAlias> FromSql(this IDataContext dataContext, string sql, object? parameters = null)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentException.ThrowIfNullOrEmpty(sql);

        return new EntityBuilder<TableAlias>(dataContext) { Logger = dataContext.CommandLogger, SourceFrom = new FromExpression(new RawSqlSourceExpression(sql, parameters)) };
    }

    /// <summary>
    /// Starts a query from an existing <see cref="QueryCommand{TResult}"/> and returns a fluent
    /// builder over its definition.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="query">The command whose definition drives the query.</param>
    /// <returns>A builder for composing the query further.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TResult> From<TResult>(this IDataContext dataContext, QueryCommand<TResult> query)
        => new(dataContext, query) { Logger = dataContext.CommandLogger };

    /// <summary>
    /// Starts a query over a lazy temporary table created by
    /// <see cref="TempTableExtensions.AsTempTable{TResult}(QueryCommand{TResult}, CreateTableOptions?)"/>.
    /// Executing the returned query runs one batch on a single session — it drops and re-creates the
    /// temporary table from the source query, then reads it — so the table is always freshly
    /// materialised and the read never depends on connection pinning. Columns are read through
    /// <see cref="TableAlias"/> accessors (<c>t.GetInt32("id")</c>).
    /// <para>
    /// The form is available on PostgreSQL, SQLite, MySQL and MariaDB, the providers whose dialect can
    /// express a temporary <c>CREATE TABLE ... AS SELECT</c>. SQL Server, ClickHouse and the in-memory
    /// context reject it when the query renders.
    /// </para>
    /// </summary>
    /// <typeparam name="TResult">The source query's projected row type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="tempTable">The lazy source to materialise and read.</param>
    /// <returns>A builder for composing the query over the temporary table.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="dataContext"/> or <paramref name="tempTable"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The context cannot execute a batch and therefore cannot materialise a temporary table (the in-memory provider).</exception>
    public static EntityBuilder<TableAlias> From<TResult>(this IDataContext dataContext, TempTableSource<TResult> tempTable)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(tempTable);

        if (dataContext is not IBatchExecutor)
            throw new NotSupportedException(
                $"{dataContext.GetType().Name} cannot materialise a temporary table; use a database-backed context (PostgreSQL, SQLite, MySQL or MariaDB).");

        return new EntityBuilder<TableAlias>(dataContext) { Logger = dataContext.CommandLogger, SourceFrom = new FromExpression(tempTable) };
    }

    /// <summary>
    /// Starts a new query builder that shares the source and definition of an existing builder.
    /// </summary>
    /// <typeparam name="TResult">The projected element type.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="builder">The builder to copy the query shape from.</param>
    /// <returns>A new builder bound to <paramref name="dataContext"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static EntityBuilder<TResult> From<TResult>(this IDataContext dataContext, EntityBuilder<TResult> builder)
        => new(dataContext, builder) { Logger = dataContext.CommandLogger };

    /// <summary>
    /// Starts a query from a table-valued function. <paramref name="call"/> must be a call to a static
    /// method annotated with <see cref="SqlTableFunctionAttribute"/> (or declared in an annotated
    /// type); its arguments are rendered as the function arguments and are parameterised like any
    /// other expression. The function must already exist in the target database - nextorm only emits
    /// the call.
    /// <para>
    /// Example: <c>ctx.FromTableFunction(() =&gt; Db.MyTvf(1, "x")).Select(r =&gt; new { r.Id })</c>.
    /// </para>
    /// </summary>
    public static EntityBuilder<T> FromTableFunction<T>(this IDataContext dataContext, Expression<Func<IQueryable<T>>> call)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(call);

        var body = call.Body;
        if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary)
            body = unary.Operand;

        if (body is not MethodCallExpression methodCall)
            throw new ArgumentException("The expression must be a call to a method mapped with [SqlTableFunction].", nameof(call));

        var entity = dataContext.From<T>();
        entity.SourceFrom = new FromExpression(TableFunctionExpression.Create(methodCall));
        return entity;
    }

    /// <summary>Starts a CTE scope with a single non-recursive declaration.</summary>
    public static CteQuery With(this IDataContext dataContext, string name, QueryCommand query)
        => new CteQuery(dataContext, [new CteDefinition(name, query)]);

    /// <summary>
    /// Starts a CTE scope whose first declaration is a data-modifying common table expression: the
    /// <paramref name="insert"/> runs as the CTE body and its <c>RETURNING</c> rows are read through
    /// <see cref="MutationCteQuery{TResult}.From(string)"/>. The scope is typed by
    /// <typeparamref name="TResult"/> so the returned rows can be filtered, joined and projected.
    /// <para>
    /// Example: <c>ctx.With("ins", ctx.InsertInto&lt;Order&gt;().Values(o).Returning(x =&gt; new { x.Id }))
    /// .From("ins").Select(r =&gt; new { r.Id })</c>.
    /// </para>
    /// </summary>
    /// <typeparam name="TEntity">The mapped entity type inserted by the CTE body.</typeparam>
    /// <typeparam name="TResult">The row shape the CTE returns through <c>RETURNING</c>.</typeparam>
    /// <param name="dataContext">The context to execute against.</param>
    /// <param name="name">The name the data-modifying CTE is declared under.</param>
    /// <param name="insert">The returning insert that forms the CTE body.</param>
    /// <returns>A scope that reads the mutation's returned rows and can declare more read CTEs.</returns>
    /// <exception cref="NotSupportedException">
    /// The active provider does not accept a data-modifying CTE body (only PostgreSQL does), or the
    /// context is the read-only in-memory provider.
    /// </exception>
    public static MutationCteQuery<TResult> With<TEntity, TResult>(this IDataContext dataContext, string name, InsertReturningBuilder<TEntity, TResult> insert)
    {
        ArgumentNullException.ThrowIfNull(dataContext);
        ArgumentNullException.ThrowIfNull(insert);
        ArgumentException.ThrowIfNullOrEmpty(name);

        return MutationCteQuery<TResult>.Create(dataContext, name, insert);
    }

    /// <summary>
    /// Starts a CTE scope with a single recursive declaration. <paramref name="maxRecursion"/>
    /// is rendered only by dialects that expose a depth option (SQL Server).
    /// </summary>
    public static CteQuery WithRecursive(this IDataContext dataContext, string name, QueryCommand query, int? maxRecursion = null)
        => new CteQuery(dataContext, [new CteDefinition(name, query, true, maxRecursion)]);
}
