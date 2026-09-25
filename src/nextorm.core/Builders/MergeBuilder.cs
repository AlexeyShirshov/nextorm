using System.Linq.Expressions;
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
public sealed partial class MergeBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private readonly List<ColumnAccumulator> _columns = [];
    private readonly List<MergeBranch> _branches = [];
    private IReadOnlyList<TEntity>? _sourceEntities;
    private QueryCommand? _source;
    private int _rowCount;
    private IReadOnlyList<IPropertyMetadata>? _keys;
    private bool _whenMatchedUpdate;
    private bool _whenNotMatchedInsert;
    private LambdaExpression? _matchCondition;
    private QueryCommand? _registry;

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
        if (_columns.Count > 0 || _source is not null)
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

        _sourceEntities = list;
        _rowCount = list.Count;
        return this;
    }

    /// <summary>
    /// Uses a server-side query as the source rows (<c>MERGE ... USING (&lt;select&gt;) AS source</c>).
    /// The query is an entity query over <typeparamref name="TEntity"/>, so its output columns line up
    /// with the target. Only the full-<c>MERGE</c> branch form is supported for a query source.
    /// </summary>
    /// <param name="source">The entity query supplying the source rows.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The source was already specified.</exception>
    public MergeBuilder<TEntity> Using(EntityBuilder<TEntity> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        return Using(source.ToCommand());
    }

    /// <summary>Uses a server-side query command as the source rows.</summary>
    /// <param name="source">The entity query command supplying the source rows.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The source was already specified.</exception>
    public MergeBuilder<TEntity> Using(QueryCommand<TEntity> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (_columns.Count > 0 || _source is not null)
            throw new InvalidOperationException("Using can only be specified once per merge.");

        _source = source;
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
        if (_matchCondition is not null)
            throw new InvalidOperationException("OnKeys can only be specified once; On(...) was already called.");

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

    /// <summary>
    /// Matches existing rows on an arbitrary condition over the <c>(target, source)</c> row instead of
    /// the declared key. Only the full-<c>MERGE</c> branch form renders it, on providers that support a
    /// search condition (<see cref="ISqlDialect.SupportsMergeConditionalBranches"/>); the others reject it.
    /// </summary>
    /// <param name="condition">The match condition, for example <c>(t, s) =&gt; t.Id == s.Id &amp;&amp; s.Age &gt; 0</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">A match key was already declared with <see cref="OnKeys"/>.</exception>
    public MergeBuilder<TEntity> On(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        if (_keys is { Count: > 0 })
            throw new InvalidOperationException("On can only be specified once; OnKeys was already called.");
        if (_matchCondition is not null)
            throw new InvalidOperationException("On can only be specified once.");

        _matchCondition = condition;
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
    /// Introduces a full-<c>MERGE</c> <c>WHEN MATCHED</c> branch; finish it with
    /// <c>ThenUpdate()</c>/<c>ThenDelete()</c>. Available only on providers that render a general
    /// <c>MERGE</c> (SQL Server, PostgreSQL 15+); the other dialects reject it with
    /// <see cref="NotSupportedException"/>.
    /// </summary>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeMatchedBuilder<TEntity> WhenMatched() => new(this);

    /// <summary>
    /// Introduces a conditional full-<c>MERGE</c> <c>WHEN MATCHED</c> branch (<c>WHEN MATCHED AND &lt;condition&gt;</c>);
    /// finish it with <c>ThenUpdate()</c>/<c>ThenDelete()</c>/<c>ThenDoNothing()</c>.
    /// </summary>
    /// <param name="condition">The branch condition over <c>(target, source)</c>, for example <c>(t, s) =&gt; t.Name != s.Name</c>.</param>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeMatchedBuilder<TEntity> WhenMatched(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new(this, condition);
    }

    /// <summary>
    /// Introduces a full-<c>MERGE</c> <c>WHEN NOT MATCHED [BY TARGET]</c> branch; finish it with
    /// <c>ThenInsert()</c>. Available only on providers that render a general <c>MERGE</c>.
    /// </summary>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBuilder<TEntity> WhenNotMatched() => new(this);

    /// <summary>
    /// Introduces a conditional full-<c>MERGE</c> <c>WHEN NOT MATCHED [BY TARGET]</c> branch
    /// (<c>WHEN NOT MATCHED AND &lt;condition&gt;</c>); finish it with <c>ThenInsert()</c>/<c>ThenDoNothing()</c>.
    /// </summary>
    /// <param name="condition">The branch condition over <c>(target, source)</c>.</param>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBuilder<TEntity> WhenNotMatched(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new(this, condition);
    }

    /// <summary>
    /// Introduces a full-<c>MERGE</c> <c>WHEN NOT MATCHED BY SOURCE</c> branch; finish it with
    /// <c>ThenDelete()</c>. Available only on SQL Server, the sole supported provider that renders this
    /// branch; every other dialect rejects it with <see cref="NotSupportedException"/>.
    /// </summary>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBySourceBuilder<TEntity> WhenNotMatchedBySource() => new(this);

    /// <summary>
    /// Introduces a conditional full-<c>MERGE</c> <c>WHEN NOT MATCHED BY SOURCE</c> branch
    /// (<c>WHEN NOT MATCHED BY SOURCE AND &lt;condition&gt;</c>); finish it with <c>ThenDelete()</c>/<c>ThenDoNothing()</c>.
    /// SQL Server only allows the condition to reference the target row.
    /// </summary>
    /// <param name="condition">The branch condition over <c>(target, source)</c>.</param>
    /// <returns>A branch builder whose terminal returns this merge builder.</returns>
    public MergeNotMatchedBySourceBuilder<TEntity> WhenNotMatchedBySource(Expression<Func<TEntity, TEntity, bool>> condition)
    {
        ArgumentNullException.ThrowIfNull(condition);
        return new(this, condition);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the whole merged row through the
    /// provider's <c>OUTPUT</c>/<c>RETURNING</c> form. Both the full-<c>MERGE</c> branch form and the
    /// key-upsert form (<c>ON CONFLICT ... DO UPDATE</c>, <c>MERGE</c>) are supported where the provider
    /// has a row-returning clause; MySQL/MariaDB reject the key-upsert form.
    /// </summary>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TEntity"/>.</returns>
    /// <exception cref="NotSupportedException">A mapped property of <typeparamref name="TEntity"/> cannot be projected.</exception>
    public MergeReturningBuilder<TEntity, TEntity> Returning()
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var identity = Expression.Lambda<Func<TEntity, TEntity>>(parameter, parameter);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(identity, _metadata.Properties, FindMapped);
        return new MergeReturningBuilder<TEntity, TEntity>(this, columns, selectList, oneColumn);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises a projection of the merged row
    /// through the provider's <c>OUTPUT</c>/<c>RETURNING</c> form. Both the full-<c>MERGE</c> branch form
    /// and the key-upsert form are supported where the provider has a row-returning clause.
    /// </summary>
    /// <typeparam name="TResult">The projected row shape.</typeparam>
    /// <param name="projection">Selects the mapped columns to return.</param>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TResult"/>.</returns>
    /// <exception cref="NotSupportedException">The projection references something other than mapped properties.</exception>
    public MergeReturningBuilder<TEntity, TResult> Returning<TResult>(Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(projection, _metadata.Properties, FindMapped);
        return new MergeReturningBuilder<TEntity, TResult>(this, columns, selectList, oneColumn);
    }

    /// <summary>Adds an update branch over <paramref name="columns"/> (or every non-key column when <see langword="null"/>).</summary>
    internal MergeBuilder<TEntity> AddMatchedUpdate(LambdaExpression? columns, LambdaExpression? condition = null)
        => AddBranch(MergeMatchKind.Matched, MergeActionKind.Update, columns, condition);

    /// <summary>Adds a delete branch of <paramref name="match"/> kind.</summary>
    internal MergeBuilder<TEntity> AddDelete(MergeMatchKind match, LambdaExpression? condition = null)
    {
        _branches.Add(new MergeBranch(match, MergeActionKind.Delete, [], condition));
        return this;
    }

    /// <summary>Adds a <c>DO NOTHING</c> branch of <paramref name="match"/> kind.</summary>
    internal MergeBuilder<TEntity> AddDoNothing(MergeMatchKind match, LambdaExpression? condition = null)
    {
        _branches.Add(new MergeBranch(match, MergeActionKind.Nothing, [], condition));
        return this;
    }

    /// <summary>Adds an insert branch over <paramref name="columns"/> (or every writable column when <see langword="null"/>).</summary>
    internal MergeBuilder<TEntity> AddNotMatchedInsert(LambdaExpression? columns, LambdaExpression? condition = null)
        => AddBranch(MergeMatchKind.NotMatchedByTarget, MergeActionKind.Insert, columns, condition);

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
    {
        if (_dataContext is InMemoryDataContext inMemory)
            return MergeInMemory(inMemory);

        return RequireExecutor().Execute(BuildCommand());
    }

    /// <summary>Asynchronously executes the merge and returns the number of affected rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows inserted or updated.</returns>
    public Task<int> MergeAsync(CancellationToken cancellationToken = default)
    {
        if (_dataContext is InMemoryDataContext inMemory)
            return Task.FromResult(MergeInMemory(inMemory));

        return RequireExecutor().Execute(BuildCommand(), cancellationToken);
    }

    private MergeCommand BuildCommand(IReadOnlyList<IPropertyMetadata>? returningColumns = null)
    {
        if (_columns.Count == 0 && _source is null)
            throw new InvalidOperationException("No source rows were specified; call Using first.");
        if (_keys is null && _matchCondition is null)
            throw new InvalidOperationException("No match condition was specified; call OnKeys() or On(...).");
        if (_matchCondition is not null && _branches.Count == 0)
            throw new InvalidOperationException("On(...) requires at least one branch; add WhenMatched()/WhenNotMatched().");

        foreach (var column in _columns)
        {
            if (column.Values.Count != _rowCount)
                throw new BuildSqlCommandException($"Column {column.Property.PropertyInfo.Name} has {column.Values.Count} values but the merge writes {_rowCount} row(s).");
        }

        var columns = new InsertColumn[_columns.Count];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new InsertColumn(_columns[i].Property, _columns[i].Values);

        if (_branches.Count > 0 || _matchCondition is not null)
        {
            var hasCondition = _matchCondition is not null;
            if (!hasCondition)
            {
                foreach (var branch in _branches)
                {
                    if (branch.Condition is not null)
                    {
                        hasCondition = true;
                        break;
                    }
                }
            }

            var registry = hasCondition ? _registry ??= new EntityBuilder<TEntity>(_dataContext).ToCommand() : null;
            return new MergeCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, columns, _rowCount, _keys ?? [], [], [.. _branches], returningColumns, _source, _matchCondition, registry);
        }

        if (_source is not null)
            throw new NotSupportedException("A query source requires the full-MERGE branch form (WhenMatched()/WhenNotMatched()); the key-upsert form only accepts an entity or batch.");

        if (!_whenMatchedUpdate || !_whenNotMatchedInsert)
            throw new InvalidOperationException("A key upsert requires both WhenMatchedUpdate() and WhenNotMatchedInsert().");

        var updateColumns = new List<IPropertyMetadata>();
        foreach (var column in _columns)
        {
            if (!column.Property.IsKey)
                updateColumns.Add(column.Property);
        }

        if (updateColumns.Count == 0)
            throw new NotSupportedException($"Entity {typeof(TEntity)} has only key columns; a key upsert needs at least one non-key column to update.");

        return new MergeCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, columns, _rowCount, _keys!, updateColumns, returningColumns: returningColumns);
    }

    /// <summary>Builds the command whose <c>RETURNING</c>/<c>OUTPUT</c> clause returns <paramref name="returningColumns"/>.</summary>
    internal MergeCommand BuildReturningCommand(IReadOnlyList<IPropertyMetadata> returningColumns) => BuildCommand(returningColumns);

    /// <summary>The context the merge executes on; used by <see cref="MergeReturningBuilder{TEntity, TResult}"/>.</summary>
    internal IDataContext DataContext => _dataContext;

    private IMutationExecutor RequireExecutor()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB); the in-memory provider supports only the key-upsert form and ClickHouse has no DML upsert.");
    }

    private sealed class ColumnAccumulator
    {
        public required IPropertyMetadata Property { get; init; }
        public List<InsertValue> Values { get; } = [];
    }
}
