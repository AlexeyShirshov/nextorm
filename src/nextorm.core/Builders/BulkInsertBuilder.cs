using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Builder for a bulk insert over a mapped entity, started with
/// <c>BulkInsertInto&lt;TEntity&gt;</c> (passing a <see cref="BulkInsertOptions"/> or a
/// <see cref="BulkInsertOptionsBuilder"/> callback). Supply the rows with
/// <see cref="Values(IEnumerable{TEntity})"/> and finish with a terminal. It writes
/// through the provider's native bulk API where one exists, and through a chunked parameterised
/// <c>INSERT ... VALUES</c> otherwise; the write shape is configured through <see cref="BulkInsertOptions"/>.
/// <para>
/// There is deliberately no change tracking: every terminal issues one explicit bulk write, as with
/// linq2db <c>BulkCopy</c>. The builder is single-use and the source is read once. Unlike
/// <see cref="InsertBuilder{TEntity}"/>, the native path does not return generated keys — request
/// <see cref="ReturningKey{TKey}"/> or <see cref="Returning{TResult}"/> for that (it forces the
/// portable, <c>RETURNING</c>/<c>OUTPUT</c>-capable path).
/// </para>
/// </summary>
/// <typeparam name="TEntity">The mapped entity type written.</typeparam>
public sealed class BulkInsertBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private readonly BulkInsertOptions _options;
    private readonly bool _keepIdentity;

    private IEnumerable<TEntity>? _syncSource;
    private IAsyncEnumerable<TEntity>? _asyncSource;

    internal BulkInsertBuilder(IDataContext dataContext, IEntityMetadata metadata, BulkInsertOptions options)
    {
        options.Validate();
        _dataContext = dataContext;
        _metadata = metadata;
        _options = options;
        _keepIdentity = options.KeepIdentity && HasIdentityColumn(metadata);
    }

    /// <summary>The context the bulk insert executes on; used by <see cref="BulkInsertReturningBuilder{TEntity, TResult}"/>.</summary>
    internal IDataContext DataContext => _dataContext;

    /// <summary>The mapped entity metadata; used by the returning builder.</summary>
    internal IEntityMetadata Metadata => _metadata;

    /// <summary>The synchronous source, or <see langword="null"/> when only an async source was supplied.</summary>
    internal IEnumerable<TEntity>? SyncSource => _syncSource;

    /// <summary>The asynchronous source, or <see langword="null"/> when only a sync source was supplied.</summary>
    internal IAsyncEnumerable<TEntity>? AsyncSource => _asyncSource;

    /// <summary>
    /// Sets the entities to write from a synchronous sequence. The sequence is iterated once; an empty
    /// sequence writes nothing and returns 0.
    /// </summary>
    /// <param name="entities">The entities to write.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entities"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A value source was already assigned.</exception>
    public BulkInsertBuilder<TEntity> Values(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        EnsureSourceUnset();

        _syncSource = entities;
        return this;
    }

    /// <summary>
    /// Sets the entities to write from an asynchronous sequence. The sequence is streamed once.
    /// </summary>
    /// <param name="entities">The entities to write.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="entities"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">A value source was already assigned.</exception>
    public BulkInsertBuilder<TEntity> Values(IAsyncEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        EnsureSourceUnset();

        _asyncSource = entities;
        return this;
    }

    /// <summary>Writes every row and returns the number written.</summary>
    /// <returns>The number of rows written (0 for an empty source).</returns>
    /// <exception cref="InvalidOperationException">No <c>Values</c> source was supplied, or the source is async.</exception>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider cannot express a requested form.</exception>
    public int BulkInsert()
    {
        EnsureSyncSource();
        var command = BuildCommand(ProjectSync(_syncSource!), null);
        return Execute(command);
    }

    /// <summary>Asynchronously writes every row and returns the number written.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows written (0 for an empty source).</returns>
    /// <exception cref="InvalidOperationException">No <c>Values</c> source was supplied.</exception>
    /// <exception cref="NotSupportedException">The context is read-only, or the provider cannot express a requested form.</exception>
    public Task<int> BulkInsertAsync(CancellationToken cancellationToken = default)
    {
        EnsureSourceSet();

        var command = _asyncSource is not null
            ? BuildCommand(null, ProjectAsync(_asyncSource))
            : BuildCommand(ProjectSync(_syncSource!), null);

        return ExecuteAsync(command, cancellationToken);
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute for the first batch on the portable path,
    /// without executing it. Useful for diagnostics and for verifying SQL generation without a database;
    /// the native path has no SQL and is still rendered as the portable <c>INSERT ... VALUES</c> form.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="InvalidOperationException">No <c>Values</c> source was supplied, the source is empty, or it is async.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed.</exception>
    public string ToSql()
    {
        EnsureSyncSource();

        if (_dataContext is not IMutationExecutor executor)
            throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");

        var command = BuildCommand(ProjectSync(_syncSource!), null);
        var rowsPerBatch = PortableBulkInsertExecutor.RowsPerBatch(command);
        var batch = new List<object?[]>(rowsPerBatch == int.MaxValue ? 4 : rowsPerBatch);

        foreach (var row in command.SyncRows!)
        {
            batch.Add(row);
            if (batch.Count == rowsPerBatch)
                break;
        }

        if (batch.Count == 0)
            throw new InvalidOperationException("Cannot render SQL for an empty bulk insert.");

        return executor.Render(PortableBulkInsertExecutor.BuildBatch(command, batch));
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the entity's key column from
    /// metadata through the provider's <c>RETURNING</c>/<c>OUTPUT</c> form. Forces the portable path and
    /// requires the provider to support <c>RETURNING</c>/<c>OUTPUT</c>.
    /// </summary>
    /// <typeparam name="TKey">The key column's CLR type.</typeparam>
    /// <returns>A returning builder whose terminals produce the generated keys.</returns>
    /// <exception cref="InvalidOperationException">The entity has no key, or <typeparamref name="TKey"/> does not match it.</exception>
    public BulkInsertReturningBuilder<TEntity, TKey> ReturningKey<TKey>()
    {
        var key = ResolveKeyProperty();
        EnsureKeyType<TKey>(key);
        var selectList = InsertReturningBuilder<TEntity, TKey>.BuildSelectList([key], [key.PropertyInfo]);
        return new BulkInsertReturningBuilder<TEntity, TKey>(this, [key], selectList, oneColumn: true);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises a projection of every written
    /// row through the provider's <c>RETURNING</c>/<c>OUTPUT</c> form. Forces the portable path and
    /// requires the provider to support <c>RETURNING</c>/<c>OUTPUT</c>.
    /// </summary>
    /// <typeparam name="TResult">The projected row shape.</typeparam>
    /// <param name="projection">Selects the mapped columns to return.</param>
    /// <returns>A returning builder whose terminals produce the projected rows.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">The projection references something other than mapped properties.</exception>
    public BulkInsertReturningBuilder<TEntity, TResult> Returning<TResult>(Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var (columns, selectList, oneColumn) = InsertReturningBuilder<TEntity, TResult>.ParseProjection(projection, _metadata.Properties, FindProperty);
        return new BulkInsertReturningBuilder<TEntity, TResult>(this, columns, selectList, oneColumn);
    }

    /// <summary>The mapped columns written by default: every property except computed ones and, unless identity values are kept, identity ones.</summary>
    /// <returns>The written columns, in declaration order.</returns>
    internal IReadOnlyList<IPropertyMetadata> WritableColumns()
    {
        var columns = new List<IPropertyMetadata>(_metadata.Properties.Count);

        foreach (var property in _metadata.Properties)
        {
            if (property.IsComputed)
                continue;

            if (property.IsIdentity && !_keepIdentity)
                continue;

            columns.Add(property);
        }

        return columns;
    }

    /// <summary>Projects one entity to the ordinal value array written for a row.</summary>
    /// <param name="entity">The entity to project.</param>
    /// <param name="columns">The written columns, in order.</param>
    /// <param name="dialect">The active dialect; used to convert a duration to its stored integer form.</param>
    /// <returns>The values, one per written column.</returns>
    internal static object?[] ToRow(TEntity entity, IReadOnlyList<IPropertyMetadata> columns, ISqlDialect dialect)
    {
        var row = new object?[columns.Count];
        for (var i = 0; i < columns.Count; i++)
            row[i] = DurationStorage.ToParameterValue(columns[i].PropertyInfo.GetValue(entity), columns[i], dialect);

        return row;
    }

    /// <summary>Projects the synchronous source to ordinal value arrays; only valid when a sync source was supplied.</summary>
    /// <returns>The projected rows.</returns>
    internal IEnumerable<object?[]> ProjectSyncRows() => ProjectSync(_syncSource!);

    /// <summary>Projects the asynchronous source to ordinal value arrays; only valid when an async source was supplied.</summary>
    /// <param name="cancellationToken">Cancels enumeration.</param>
    /// <returns>The projected rows.</returns>
    internal IAsyncEnumerable<object?[]> ProjectAsyncRows(CancellationToken cancellationToken) => ProjectAsync(_asyncSource!, cancellationToken);

    /// <summary>Builds the bulk-insert command from the accumulated options and the projected rows.</summary>
    /// <param name="syncRows">The synchronous projected rows, or <see langword="null"/>.</param>
    /// <param name="asyncRows">The asynchronous projected rows, or <see langword="null"/>.</param>
    /// <returns>The bulk-insert command.</returns>
    internal BulkInsertCommand BuildCommand(IEnumerable<object?[]>? syncRows, IAsyncEnumerable<object?[]>? asyncRows)
    {
        var columns = WritableColumns();
        if (columns.Count == 0)
            throw new InvalidOperationException($"Entity {typeof(TEntity)} has no writable column to insert.");

        var batch = _options.MaxBatchSize is null && _options.MaxParameters is null && _options.MaxSqlLength is null
            ? null
            : new BulkBatchOptions(_options.MaxBatchSize, _options.MaxParameters, _options.MaxSqlLength);

        var tableName = _options.TableName ?? _metadata.TableName!;
        var isTableNameAuto = _options.TableName is null && _metadata.IsTableNameAuto;

        return new BulkInsertCommand(
            typeof(TEntity),
            tableName,
            isTableNameAuto,
            columns,
            syncRows,
            asyncRows,
            _keepIdentity,
            _options.IgnoreDuplicates,
            batch,
            CreateProgressCallback(),
            _options.NotifyEvery,
            _options.TimeoutSeconds,
            _options.TableSchema);
    }

    /// <summary>Resolves the executor that can satisfy a returning terminal (the mutation executor).</summary>
    /// <returns>The context's mutation executor.</returns>
    /// <exception cref="NotSupportedException">The context is read-only.</exception>
    internal IMutationExecutor RequireMutationExecutor()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL, MariaDB or ClickHouse); the in-memory provider is read-only.");
    }

    /// <summary>Validates that a synchronous <c>Values</c> source was supplied; used by the returning terminal.</summary>
    /// <exception cref="InvalidOperationException">No source was supplied, or the source is async.</exception>
    internal void ValidateSyncSource() => EnsureSyncSource();

    /// <summary>Validates that a <c>Values</c> source was supplied; used by the returning terminal.</summary>
    /// <exception cref="InvalidOperationException">No source was supplied.</exception>
    internal void ValidateSource() => EnsureSourceSet();

    private int Execute(BulkInsertCommand command)
    {
        var db = _dataContext as DataContext;
        if (UseNative(db))
            return ((IBulkInsertExecutor)db!).BulkInsert(command);

        return new PortableBulkInsertExecutor(RequireMutationExecutor()).BulkInsert(command);
    }

    private Task<int> ExecuteAsync(BulkInsertCommand command, CancellationToken cancellationToken)
    {
        var db = _dataContext as DataContext;
        if (UseNative(db))
            return ((IBulkInsertExecutor)db!).BulkInsertAsync(command, cancellationToken);

        return new PortableBulkInsertExecutor(RequireMutationExecutor()).BulkInsertAsync(command, cancellationToken);
    }

    private bool UseNative(DataContext? db)
        => db is not null && db.Dialect.SupportsBulkCopy && !_options.IgnoreDuplicates && !_keepIdentity;

    private static bool HasIdentityColumn(IEntityMetadata metadata)
    {
        foreach (var property in metadata.Properties)
        {
            if (property.IsIdentity && !property.IsComputed)
                return true;
        }

        return false;
    }

    private IEnumerable<object?[]> ProjectSync(IEnumerable<TEntity> source)
    {
        var columns = WritableColumns();
        var dialect = ((DataContext)_dataContext).Dialect;
        foreach (var entity in source)
            yield return ToRow(entity, columns, dialect);
    }

    private async IAsyncEnumerable<object?[]> ProjectAsync(IAsyncEnumerable<TEntity> source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var columns = WritableColumns();
        var dialect = ((DataContext)_dataContext).Dialect;
        await foreach (var entity in source.WithCancellation(cancellationToken).ConfigureAwait(false))
            yield return ToRow(entity, columns, dialect);
    }

    private Action<int>? CreateProgressCallback()
    {
        var onProgress = _options.Progress;
        if (onProgress is null)
            return null;

        var every = _options.NotifyEvery;
        var token = _options.ProgressCancellationTokenSource?.Token ?? CancellationToken.None;
        var next = every;

        return total =>
        {
            if (total < next)
                return;

            if (token.IsCancellationRequested)
                throw new OperationCanceledException(token);

            onProgress(total, token);

            if (token.IsCancellationRequested)
                throw new OperationCanceledException(token);

            next = total + every;
        };
    }

    private IPropertyMetadata ResolveKeyProperty()
    {
        IPropertyMetadata? key = null;

        foreach (var property in _metadata.Properties)
        {
            if (!property.IsKey)
                continue;

            if (key is not null)
                throw new InvalidOperationException(
                    $"Entity {typeof(TEntity)} declares more than one key; project the key column explicitly, e.g. Returning(x => x.Id).");

            key = property;
        }

        return key
            ?? throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no key property. Mark one with [Key]/.Key() or use Returning on a keyed entity.");
    }

    private static void EnsureKeyType<TKey>(IPropertyMetadata key)
    {
        var requested = Nullable.GetUnderlyingType(typeof(TKey)) ?? typeof(TKey);
        var actual = Nullable.GetUnderlyingType(key.PropertyInfo.PropertyType) ?? key.PropertyInfo.PropertyType;

        if (requested != actual)
            throw new InvalidOperationException(
                $"The key property {key.PropertyInfo.Name} of {typeof(TEntity)} is {key.PropertyInfo.PropertyType}, which does not match ReturningKey<{typeof(TKey).Name}>.");
    }

    private IPropertyMetadata? FindProperty(PropertyInfo property)
    {
        foreach (var candidate in _metadata.Properties)
        {
            if (candidate.PropertyInfo == property)
                return candidate;
        }

        return null;
    }

    private void EnsureSourceSet()
    {
        if (_syncSource is null && _asyncSource is null)
            throw new InvalidOperationException("No values were specified for the bulk insert; call Values first.");
    }

    private void EnsureSourceUnset()
    {
        if (_syncSource is not null || _asyncSource is not null)
            throw new InvalidOperationException("Values was already called on this bulk insert builder.");
    }

    private void EnsureSyncSource()
    {
        EnsureSourceSet();

        if (_asyncSource is not null)
            throw new InvalidOperationException("BulkInsert is synchronous but the source is async; use BulkInsertAsync instead.");
    }
}
