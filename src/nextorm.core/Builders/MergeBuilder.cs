using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Fluent builder for a key upsert (a "merge" of a source row set into the target table), started with
/// <see cref="DataContextExtensions.MergeInto{TEntity}"/>. The source is a mapped entity or a batch of
/// entities; the database decides, per key, whether to update the existing row or insert a new one. The
/// statement is rendered through the active dialect as <c>INSERT ... ON CONFLICT ... DO UPDATE</c>
/// (PostgreSQL, SQLite), <c>INSERT ... ON DUPLICATE KEY UPDATE</c> (MySQL, MariaDB) or <c>MERGE</c>
/// (SQL Server).
/// <para>
/// There is deliberately no change tracking: every terminal (<see cref="Merge"/>, <see cref="MergeAsync"/>)
/// issues one explicit command, as with the insert builder. The builder is single-use.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The mapped entity type upserted.</typeparam>
/// <remarks>
/// A key upsert is defined by the <see cref="Using(TEntity)"/> source, the <see cref="OnKeys"/> match
/// key and both branches <see cref="WhenMatchedUpdate"/> and <see cref="WhenNotMatchedInsert"/>; all
/// four are required. <see cref="OnKeys"/> resolves the key from the entity mapping, so it must be
/// declared before the merge; some providers (MySQL, MariaDB) render the update without naming the key
/// because their native <c>ON DUPLICATE KEY UPDATE</c> clause has no key list.
/// </remarks>
public sealed class MergeBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private readonly List<ColumnAccumulator> _columns = [];
    private int _rowCount;
    private IReadOnlyList<IPropertyMetadata>? _keys;
    private bool _whenMatchedUpdate;
    private bool _whenNotMatchedInsert;

    internal MergeBuilder(IDataContext dataContext, IEntityMetadata metadata)
    {
        _dataContext = dataContext;
        _metadata = metadata;
    }

    /// <summary>Uses a single mapped entity as the source row.</summary>
    /// <param name="entity">The entity whose column values form the source row.</param>
    /// <returns>This builder, for chaining.</returns>
    [OverloadResolutionPriority(1)]
    public MergeBuilder<TEntity> Using(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Using([entity]);
    }

    /// <summary>
    /// Uses a batch of mapped entities as the source rows. Identity and computed columns are excluded
    /// automatically.
    /// </summary>
    /// <param name="entities">The entities whose column values form the source rows.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The source was already specified.</exception>
    [OverloadResolutionPriority(1)]
    public MergeBuilder<TEntity> Using(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        if (_columns.Count > 0)
            throw new InvalidOperationException("Using can only be specified once per merge.");

        var list = entities as IReadOnlyList<TEntity> ?? entities.ToList();
        if (list.Count == 0)
            throw new ArgumentException("At least one entity is required.", nameof(entities));

        foreach (var property in _metadata.Properties)
        {
            if (property.IsIdentity || property.IsComputed)
                continue;

            var accumulator = new ColumnAccumulator { Property = property };
            for (var i = 0; i < list.Count; i++)
                accumulator.Values.Add(InsertValue.FromConstant(property.PropertyInfo.GetValue(list[i])));

            _columns.Add(accumulator);
        }

        if (_columns.Count == 0)
            throw new BuildSqlCommandException($"Entity {typeof(TEntity)} has no writable column to merge.");

        _rowCount = list.Count;
        return this;
    }

    /// <summary>
    /// Matches existing rows on the entity's declared key column(s), resolved from metadata
    /// (<c>[Key]</c>, the fluent <c>.Key()</c> or the <c>Id</c>/<c>&lt;Type&gt;Id</c> convention).
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The entity has no key property.</exception>
    /// <exception cref="NotSupportedException">A key column is database-generated.</exception>
    public MergeBuilder<TEntity> OnKeys()
    {
        var keys = new List<IPropertyMetadata>();
        foreach (var property in _metadata.Properties)
        {
            if (!property.IsKey)
                continue;

            if (property.IsIdentity || property.IsComputed)
                throw new NotSupportedException(
                    $"Key property {property.PropertyInfo.Name} of {typeof(TEntity)} is database-generated and cannot be used as an upsert match key.");

            keys.Add(property);
        }

        if (keys.Count == 0)
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no key property. Mark one with [Key]/.Key() before calling OnKeys().");

        _keys = keys;
        return this;
    }

    /// <summary>Updates every non-key writable column from the source when the row already exists.</summary>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> WhenMatchedUpdate()
    {
        _whenMatchedUpdate = true;
        return this;
    }

    /// <summary>Inserts a new row when no key matches.</summary>
    /// <returns>This builder, for chaining.</returns>
    public MergeBuilder<TEntity> WhenNotMatchedInsert()
    {
        _whenNotMatchedInsert = true;
        return this;
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    public string ToSql()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    /// <summary>Executes the merge and returns the number of affected rows.</summary>
    /// <returns>The number of rows inserted or updated, as reported by the provider.</returns>
    public int Merge()
        => RequireExecutor().Execute(BuildCommand());

    /// <summary>Asynchronously executes the merge and returns the number of affected rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows inserted or updated.</returns>
    public Task<int> MergeAsync(CancellationToken cancellationToken = default)
        => RequireExecutor().Execute(BuildCommand(), cancellationToken);

    private MergeCommand BuildCommand()
    {
        if (_columns.Count == 0)
            throw new InvalidOperationException("No source rows were specified; call Using first.");
        if (_keys is null)
            throw new InvalidOperationException("No match key was specified; call OnKeys first.");
        if (!_whenMatchedUpdate || !_whenNotMatchedInsert)
            throw new InvalidOperationException("A key upsert requires both WhenMatchedUpdate() and WhenNotMatchedInsert().");

        foreach (var column in _columns)
        {
            if (column.Values.Count != _rowCount)
                throw new BuildSqlCommandException($"Column {column.Property.PropertyInfo.Name} has {column.Values.Count} values but the merge writes {_rowCount} row(s).");
        }

        var columns = new InsertColumn[_columns.Count];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new InsertColumn(_columns[i].Property, _columns[i].Values);

        var updateColumns = new List<IPropertyMetadata>();
        foreach (var column in _columns)
        {
            if (!column.Property.IsKey)
                updateColumns.Add(column.Property);
        }

        if (updateColumns.Count == 0)
            throw new NotSupportedException($"Entity {typeof(TEntity)} has only key columns; a key upsert needs at least one non-key column to update.");

        return new MergeCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, columns, _rowCount, _keys, updateColumns);
    }

    private IMutationExecutor RequireExecutor()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB); the in-memory provider is read-only and ClickHouse has no DML upsert.");
    }

    private sealed class ColumnAccumulator
    {
        public required IPropertyMetadata Property { get; init; }
        public List<InsertValue> Values { get; } = [];
    }
}
