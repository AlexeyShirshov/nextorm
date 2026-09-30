using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent builder for a <c>DELETE</c> statement, started with
/// <see cref="DataContextExtensions.DeleteFrom{TEntity}"/>. The rows to remove are selected either by a
/// predicate (<see cref="Where"/>) or explicitly as the whole table (<see cref="All"/>); both render a
/// parameterised <c>DELETE FROM &lt;table&gt; [WHERE ...]</c>. There is deliberately no change tracking:
/// the terminal issues exactly one explicit command.
/// </summary>
/// <typeparam name="TEntity">The mapped entity type whose rows are deleted.</typeparam>
public sealed class DeleteBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private EntityBuilder<TEntity>? _filter;
    private QueryFilterScope _filterScope = QueryFilterScope.None;
    private bool _all;

    internal DeleteBuilder(IDataContext dataContext, IEntityMetadata metadata)
    {
        _dataContext = dataContext;
        _metadata = metadata;
    }

    /// <summary>The context the delete executes on; used by <see cref="DeleteReturningBuilder{TEntity, TResult}"/>.</summary>
    internal IDataContext DataContext => _dataContext;

    /// <summary>
    /// Restricts the delete to the rows satisfying <paramref name="predicate"/>. The predicate is
    /// translated by the same expression pipeline as a <c>SELECT</c> <c>WHERE</c>; captured variables
    /// become parameters, inline literals are emitted verbatim. Repeating the call combines the
    /// predicates with <c>and</c> (matching <c>From&lt;T&gt;().Where(...)</c>).
    /// </summary>
    /// <param name="predicate">The condition each deleted row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><see cref="All"/> was already called.</exception>
    public DeleteBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (_all)
            throw new InvalidOperationException("Where(...) cannot be combined with All(); a delete is either filtered or full-table.");

        _filter = (_filter ?? _dataContext.From<TEntity>()).Where(predicate);
        return this;
    }

    /// <summary>
    /// Disables <b>all</b> global query filters declared for the target entity type, so the delete (in
    /// the predicate form and the key form) removes the unfiltered rows. <see cref="All"/> stays an
    /// explicit full-table delete and is never filtered. Repeatable: a later call accumulates with the
    /// earlier scope.
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    public DeleteBuilder<TEntity> IgnoreFilters()
    {
        _filterScope = _filterScope.Union(QueryFilterScope.AllFilters);
        return this;
    }

    /// <summary>
    /// Disables every global query filter declared for the given entity types. An empty or
    /// <see langword="null"/> <paramref name="entityTypes"/> disables nothing. Repeatable: a later call
    /// accumulates (union) with the earlier scope.
    /// </summary>
    /// <param name="entityTypes">The entity types whose filters are disabled.</param>
    /// <returns>This builder, for chaining.</returns>
    public DeleteBuilder<TEntity> IgnoreFilters(params Type[] entityTypes)
    {
        var scope = QueryFilterScope.FromTypes(entityTypes);
        if (!scope.IsEmpty)
            _filterScope = _filterScope.Union(scope);

        return this;
    }

    /// <summary>
    /// Disables the named global query filters identified by <paramref name="filterKeys"/> on the target
    /// entity type. An empty or <see langword="null"/> <paramref name="filterKeys"/> disables nothing.
    /// Repeatable: a later call accumulates (union) with the earlier scope.
    /// </summary>
    /// <param name="filterKeys">The filter keys to disable.</param>
    /// <returns>This builder, for chaining.</returns>
    public DeleteBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys)
    {
        var scope = QueryFilterScope.FromKeys(filterKeys);
        if (!scope.IsEmpty)
            _filterScope = _filterScope.Union(scope);

        return this;
    }

    /// <summary>
    /// Disables the named global query filters identified by <paramref name="filterKeys"/> only on the
    /// given <paramref name="entityTypes"/> (the intersection of keys and types); an empty
    /// <paramref name="entityTypes"/> means any entity type. The key list is the gate: an empty or
    /// <see langword="null"/> <paramref name="filterKeys"/> disables nothing even when entity types are
    /// supplied. Repeatable: a later call accumulates (union) with the earlier scope.
    /// </summary>
    /// <param name="filterKeys">The filter keys to disable.</param>
    /// <param name="entityTypes">The entity types the disable is scoped to; empty means any entity type.</param>
    /// <returns>This builder, for chaining.</returns>
    public DeleteBuilder<TEntity> IgnoreFilters(IEnumerable<string> filterKeys, params Type[] entityTypes)
    {
        var scope = QueryFilterScope.FromKeysAndTypes(filterKeys, entityTypes);
        if (!scope.IsEmpty)
            _filterScope = _filterScope.Union(scope);

        return this;
    }

    /// <summary>
    /// Deletes every row of the table. Required (and only meaningful) when no <see cref="Where"/> is
    /// given, so an accidental full-table delete cannot be written by omitting the predicate.
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException"><see cref="Where"/> was already called.</exception>
    public DeleteBuilder<TEntity> All()
    {
        if (_filter is not null)
            throw new InvalidOperationException("All() cannot be combined with Where(...); a delete is either filtered or full-table.");

        _all = true;
        return this;
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="InvalidOperationException">Neither <see cref="Where"/> nor <see cref="All"/> was called.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed (for example the in-memory provider).</exception>
    public string ToSql()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the whole deleted row through
    /// the provider's <c>RETURNING</c>/<c>OUTPUT</c> form.
    /// </summary>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TEntity"/>.</returns>
    public DeleteReturningBuilder<TEntity, TEntity> Returning()
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var identity = Expression.Lambda<Func<TEntity, TEntity>>(parameter, parameter);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(identity, _metadata.Properties, FindProperty);
        return new DeleteReturningBuilder<TEntity, TEntity>(this, columns, selectList, oneColumn, projection: identity);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises a projection of the deleted
    /// row through the provider's <c>RETURNING</c>/<c>OUTPUT</c> form. The projection may be the
    /// identity, a single mapped property, an anonymous type, a positional constructor or a
    /// member-init; it may only reference mapped properties.
    /// </summary>
    /// <typeparam name="TResult">The projected row shape.</typeparam>
    /// <param name="projection">Selects the mapped columns to return.</param>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TResult"/>.</returns>
    /// <exception cref="NotSupportedException">The projection references something other than mapped properties.</exception>
    public DeleteReturningBuilder<TEntity, TResult> Returning<TResult>(Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(projection, _metadata.Properties, FindProperty);
        return new DeleteReturningBuilder<TEntity, TResult>(this, columns, selectList, oneColumn, projection: projection);
    }

    /// <summary>Executes the delete and returns the number of affected rows.</summary>
    /// <returns>The number of deleted rows, as reported by the provider.</returns>
    /// <exception cref="InvalidOperationException">Neither <see cref="Where"/> nor <see cref="All"/> was called.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed (for example the in-memory provider).</exception>
    public int Delete()
    {
        var command = BuildCommand();
        return Execute(command);
    }

    /// <summary>Asynchronously executes the delete and returns the number of affected rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of deleted rows.</returns>
    /// <exception cref="InvalidOperationException">Neither <see cref="Where"/> nor <see cref="All"/> was called.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed (for example the in-memory provider).</exception>
    public Task<int> DeleteAsync(CancellationToken cancellationToken = default)
    {
        var command = BuildCommand();
        return ExecuteAsync(command, cancellationToken);
    }

    internal int DeleteEntity(TEntity entity)
    {
        var command = BuildKeyCommand(entity);
        return Execute(command);
    }

    internal Task<int> DeleteEntityAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var command = BuildKeyCommand(entity);
        return ExecuteAsync(command, cancellationToken);
    }

    private DeleteCommand BuildCommand()
    {
        if (_filter is null && !_all)
            throw new InvalidOperationException("A delete needs a predicate; call Where(...) or All() to delete every row.");

        // The predicate reuses the SELECT pipeline: the command's prepared condition (user predicate
        // plus the injected target filter) is rendered as a standalone WHERE by the planner. All()
        // deletes the whole table (no condition, no keys).
        var condition = _filter is null ? null : ApplyFilterScope(_filter).ToCommand();

        return new DeleteCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, condition, null);
    }

    internal DeleteCommand BuildKeyCommand(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var keys = new List<KeyValue>();
        foreach (var property in _metadata.Properties)
        {
            if (property.IsKey)
                keys.Add(new KeyValue(property, property.PropertyInfo.GetValue(entity)));
        }

        if (keys.Count == 0)
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no key property. Mark one with [Key]/.Key() before deleting by entity, or use DeleteFrom<T>().Where(...).");

        // The key form carries the target entity's global filter through a no-predicate source command:
        // preparing it injects the filter (minus the IgnoreFilters scope) into the condition, which the
        // planner ANDs to the key equalities.
        var condition = ApplyFilterScope(_filter ?? _dataContext.From<TEntity>()).ToCommand();
        return new DeleteCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, condition, keys);
    }

    /// <summary>Builds the delete command for use as a side-effecting step of a batch.</summary>
    /// <returns>The delete command.</returns>
    internal MutationCommand BuildBatchCommand() => BuildCommand();

    /// <summary>Builds the delete command carrying the columns to return through <c>RETURNING</c>/<c>OUTPUT</c>.</summary>
    /// <param name="returningColumns">The mapped columns to return.</param>
    /// <param name="outputInto">The <c>OUTPUT ... INTO</c> target, or <see langword="null"/>.</param>
    /// <returns>The delete command carrying the returned columns.</returns>
    internal DeleteCommand BuildReturningCommand(IReadOnlyList<IPropertyMetadata> returningColumns, OutputIntoClause? outputInto = null)
    {
        if (_filter is null && !_all)
            throw new InvalidOperationException("A delete needs a predicate; call Where(...) or All() to delete every row.");

        var condition = _filter is null ? null : ApplyFilterScope(_filter).ToCommand();
        return new DeleteCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, condition, null, returningColumns, outputInto);
    }

    /// <summary>Builds the delete command for an <c>OUTPUT ... INTO</c>-only terminal: the removed rows are written into the target and nothing is returned to the client.</summary>
    /// <param name="outputColumns">The mapped columns written into the target.</param>
    /// <param name="targetTable">The raw (unquoted) target table name.</param>
    /// <returns>The delete command carrying the output-into target.</returns>
    internal DeleteCommand BuildOutputIntoCommand(IReadOnlyList<IPropertyMetadata> outputColumns, string targetTable)
    {
        if (_filter is null && !_all)
            throw new InvalidOperationException("A delete needs a predicate; call Where(...) or All() to delete every row.");

        var condition = _filter is null ? null : ApplyFilterScope(_filter).ToCommand();
        return new DeleteCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, condition, null, null, new OutputIntoClause(targetTable, outputColumns));
    }

    // Applies the builder's selective filter scope to the source command that carries the DELETE's
    // condition, so both the predicate form and the key form honor IgnoreFilters. The scope is folded
    // into a copy, never onto the retained source builder, so repeated terminal builds re-derive from
    // the current state instead of accumulating a stale scope.
    private EntityBuilder<TEntity> ApplyFilterScope(EntityBuilder<TEntity> source)
        => source.WithFilterScope(_filterScope);

    private IPropertyMetadata? FindProperty(PropertyInfo property)
    {
        foreach (var candidate in _metadata.Properties)
        {
            if (candidate.PropertyInfo == property)
                return candidate;
        }

        return null;
    }

    private int Execute(DeleteCommand command)
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command);

        throw Unsupported();
    }

    private Task<int> ExecuteAsync(DeleteCommand command, CancellationToken cancellationToken)
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command, cancellationToken);

        throw Unsupported();
    }

    private NotSupportedException Unsupported()
        => new(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");
}
