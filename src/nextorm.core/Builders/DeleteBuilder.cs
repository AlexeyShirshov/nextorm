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
        return new DeleteReturningBuilder<TEntity, TEntity>(this, columns, selectList, oneColumn);
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
        return new DeleteReturningBuilder<TEntity, TResult>(this, columns, selectList, oneColumn);
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

        // The predicate reuses the SELECT pipeline: the command's prepared condition is rendered as a
        // standalone WHERE by the planner. All() deletes the whole table (no condition, no keys).
        var condition = _filter?.ToCommand();

        return new DeleteCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, condition, null);
    }

    private DeleteCommand BuildKeyCommand(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        var keys = new List<DeleteKey>();
        foreach (var property in _metadata.Properties)
        {
            if (property.IsKey)
                keys.Add(new DeleteKey(property, property.PropertyInfo.GetValue(entity)));
        }

        if (keys.Count == 0)
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no key property. Mark one with [Key]/.Key() before deleting by entity, or use DeleteFrom<T>().Where(...).");

        return new DeleteCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, null, keys);
    }

    /// <summary>Builds the delete command carrying the columns to return through <c>RETURNING</c>/<c>OUTPUT</c>.</summary>
    /// <param name="returningColumns">The mapped columns to return.</param>
    /// <returns>The delete command carrying the returned columns.</returns>
    internal DeleteCommand BuildReturningCommand(IReadOnlyList<IPropertyMetadata> returningColumns)
    {
        if (_filter is null && !_all)
            throw new InvalidOperationException("A delete needs a predicate; call Where(...) or All() to delete every row.");

        var condition = _filter?.ToCommand();
        return new DeleteCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, condition, null, returningColumns);
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
