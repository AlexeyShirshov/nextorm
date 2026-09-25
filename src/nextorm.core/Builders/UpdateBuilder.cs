using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent builder for an <c>UPDATE</c> statement, started with
/// <see cref="DataContextExtensions.Update{TEntity}(IDataContext, Action{EntityMetadataBuilder{TEntity}}?)"/>.
/// The columns to write are supplied with <see cref="Set{TValue}(Expression{Func{TEntity, TValue}}, TValue)"/>,
/// <see cref="Set{TValue}(Expression{Func{TEntity, TValue}}, Expression{Func{TEntity, TValue}})"/> or
/// <see cref="Set(TEntity)"/>; the rows to change are selected with <see cref="Where"/>. Omitting
/// <see cref="Where"/> updates every row of the table.
/// <para>
/// There is deliberately no change tracking: the terminal issues exactly one explicit command and
/// updates only the columns named in the <c>SET</c> list, never "the changed properties".
/// </para>
/// </summary>
/// <typeparam name="TEntity">The mapped entity type whose rows are updated.</typeparam>
public sealed class UpdateBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private readonly List<UpdateAssignment> _assignments = [];
    private EntityBuilder<TEntity>? _filter;

    internal UpdateBuilder(IDataContext dataContext, IEntityMetadata metadata)
    {
        _dataContext = dataContext;
        _metadata = metadata;
    }

    /// <summary>The context the update executes on.</summary>
    internal IDataContext DataContext => _dataContext;

    /// <summary>
    /// Assigns a constant value to a mapped column. The value is always bound as a parameter, never
    /// inlined into the SQL. Repeating the call for the same column replaces the earlier assignment.
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the mapped column to write.</param>
    /// <param name="value">The value to write, or <see langword="null"/> for SQL <c>NULL</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="NotSupportedException">The selected column is computed and cannot be written.</exception>
    public UpdateBuilder<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> column, TValue value)
    {
        ArgumentNullException.ThrowIfNull(column);
        var property = ResolveWritableProperty(column, nameof(column));
        SetAssignment(UpdateAssignment.FromConstant(property, value));
        return this;
    }

    /// <summary>
    /// Assigns the result of an expression to a mapped column. The expression is rendered by the same
    /// translator as a <c>SELECT</c> projection, so it may reference other mapped columns
    /// (<c>Set(x =&gt; x.Counter, x =&gt; x.Counter + 1)</c>, <c>Set(x =&gt; x.UpdatedAt, x =&gt; x.CreatedAt)</c>)
    /// and captured variables (which become parameters). A parameter-free expression is folded into a
    /// parameter.
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the mapped column to write.</param>
    /// <param name="value">The value expression to evaluate.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="NotSupportedException">The selected column is computed, or the value expression cannot be translated.</exception>
    public UpdateBuilder<TEntity> Set<TValue>(Expression<Func<TEntity, TValue>> column, Expression<Func<TEntity, TValue>> value)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(value);
        var property = ResolveWritableProperty(column, nameof(column));
        SetAssignment(BuildAssignment(property, value));
        return this;
    }

    /// <summary>
    /// Writes every mapped, writable column of <paramref name="entity"/>. Key, identity and computed
    /// columns are excluded automatically. Combine with <see cref="Where"/> to select the row(s); this
    /// form does not build the filter from the entity's key (use
    /// <see cref="DataContextExtensions.Update{TEntity}(IDataContext, TEntity)"/> for that).
    /// </summary>
    /// <param name="entity">The entity whose column values are written.</param>
    /// <returns>This builder, for chaining.</returns>
    public UpdateBuilder<TEntity> Set(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        foreach (var property in _metadata.Properties)
        {
            if (property.IsKey || property.IsIdentity || property.IsComputed)
                continue;

            SetAssignment(UpdateAssignment.FromConstant(property, property.PropertyInfo.GetValue(entity)));
        }

        return this;
    }

    /// <summary>
    /// Restricts the update to the rows satisfying <paramref name="predicate"/>. The predicate is
    /// translated by the same expression pipeline as a <c>SELECT</c> <c>WHERE</c>; captured variables
    /// become parameters, inline literals are emitted verbatim. Repeating the call combines the
    /// predicates with <c>and</c>.
    /// </summary>
    /// <param name="predicate">The condition each updated row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public UpdateBuilder<TEntity> Where(Expression<Func<TEntity, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);

        _filter = (_filter ?? _dataContext.From<TEntity>()).Where(predicate);
        return this;
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the whole updated row through
    /// the provider's <c>RETURNING</c>/<c>OUTPUT</c> form (the equivalent of <c>RETURNING *</c> over the
    /// mapped columns). The predicate form is required: the key form (<c>Update(entity)</c>) does not
    /// expose <c>Returning</c>.
    /// </summary>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TEntity"/>.</returns>
    public UpdateReturningBuilder<TEntity, TEntity> Returning()
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var identity = Expression.Lambda<Func<TEntity, TEntity>>(parameter, parameter);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(identity, _metadata.Properties, FindProperty);
        return new UpdateReturningBuilder<TEntity, TEntity>(this, columns, selectList, oneColumn);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises a projection of the updated row
    /// through the provider's <c>RETURNING</c>/<c>OUTPUT</c> form. The projection may be the identity, a
    /// single mapped property, an anonymous type, a positional constructor or a member-init; it may only
    /// reference mapped properties.
    /// </summary>
    /// <typeparam name="TResult">The projected row shape.</typeparam>
    /// <param name="projection">Selects the mapped columns to return.</param>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TResult"/>.</returns>
    /// <exception cref="NotSupportedException">The projection references something other than mapped properties.</exception>
    public UpdateReturningBuilder<TEntity, TResult> Returning<TResult>(Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var (columns, selectList, oneColumn) = ReturningProjection.Parse(projection, _metadata.Properties, FindProperty);
        return new UpdateReturningBuilder<TEntity, TResult>(this, columns, selectList, oneColumn);
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="InvalidOperationException">No assignment was specified.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed (for example the in-memory provider), or the provider cannot express <c>UPDATE</c>.</exception>
    public string ToSql()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    /// <summary>Executes the update and returns the number of affected rows.</summary>
    /// <returns>The number of updated rows, as reported by the provider.</returns>
    /// <exception cref="InvalidOperationException">No assignment was specified.</exception>
    /// <exception cref="NotSupportedException">The context does not support data modification, or the provider cannot express <c>UPDATE</c>.</exception>
    public int Update()
        => RequireExecutor().Execute(BuildCommand());

    /// <summary>Asynchronously executes the update and returns the number of affected rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of updated rows.</returns>
    /// <exception cref="InvalidOperationException">No assignment was specified.</exception>
    /// <exception cref="NotSupportedException">The context does not support data modification, or the provider cannot express <c>UPDATE</c>.</exception>
    public Task<int> UpdateAsync(CancellationToken cancellationToken = default)
        => RequireExecutor().Execute(BuildCommand(), cancellationToken);

    internal int UpdateEntity(TEntity entity)
    {
        var command = BuildEntityCommand(entity);
        return Execute(command);
    }

    internal Task<int> UpdateEntityAsync(TEntity entity, CancellationToken cancellationToken)
    {
        var command = BuildEntityCommand(entity);
        return ExecuteAsync(command, cancellationToken);
    }

    private UpdateCommand BuildEntityCommand(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        Set(entity);

        var keys = new List<KeyValue>();
        foreach (var property in _metadata.Properties)
        {
            if (property.IsKey)
                keys.Add(new KeyValue(property, property.PropertyInfo.GetValue(entity)));
        }

        if (keys.Count == 0)
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no key property. Mark one with [Key]/.Key() before updating by entity, or use Update<T>().Set(...).Where(...).");

        return BuildCommand(keys);
    }

    private UpdateCommand BuildCommand(IReadOnlyList<KeyValue>? keys = null)
    {
        if (_assignments.Count == 0)
            throw new InvalidOperationException("An update needs at least one assignment; call Set(...) or Set(entity).");

        var source = (_filter ?? _dataContext.From<TEntity>()).ToCommand();
        return new UpdateCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, _assignments, source, keys);
    }

    /// <summary>Builds the update command for use as a side-effecting step of a batch.</summary>
    /// <returns>The update command.</returns>
    internal MutationCommand BuildBatchCommand() => BuildCommand();

    /// <summary>Builds the update command carrying the columns to return through <c>RETURNING</c>/<c>OUTPUT</c>.</summary>
    /// <param name="returningColumns">The mapped columns to return.</param>
    /// <param name="outputInto">The <c>OUTPUT ... INTO</c> target, or <see langword="null"/>.</param>
    /// <returns>The update command carrying the returned columns.</returns>
    internal UpdateCommand BuildReturningCommand(IReadOnlyList<IPropertyMetadata> returningColumns, OutputIntoClause? outputInto = null)
    {
        if (_assignments.Count == 0)
            throw new InvalidOperationException("An update needs at least one assignment; call Set(...) or Set(entity).");

        var source = (_filter ?? _dataContext.From<TEntity>()).ToCommand();
        return new UpdateCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, _assignments, source, null, returningColumns, outputInto);
    }

    /// <summary>Builds the update command for an <c>OUTPUT ... INTO</c>-only terminal: the updated rows are written into the target and nothing is returned to the client.</summary>
    /// <param name="outputColumns">The mapped columns written into the target.</param>
    /// <param name="targetTable">The raw (unquoted) target table name.</param>
    /// <returns>The update command carrying the output-into target.</returns>
    internal UpdateCommand BuildOutputIntoCommand(IReadOnlyList<IPropertyMetadata> outputColumns, string targetTable)
    {
        if (_assignments.Count == 0)
            throw new InvalidOperationException("An update needs at least one assignment; call Set(...) or Set(entity).");

        var source = (_filter ?? _dataContext.From<TEntity>()).ToCommand();
        return new UpdateCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, _assignments, source, null, null, new OutputIntoClause(targetTable, outputColumns));
    }

    private void SetAssignment(UpdateAssignment assignment)
    {
        for (var i = 0; i < _assignments.Count; i++)
        {
            if (_assignments[i].Property.PropertyInfo == assignment.Property.PropertyInfo)
            {
                _assignments[i] = assignment;
                return;
            }
        }

        _assignments.Add(assignment);
    }

    private UpdateAssignment BuildAssignment<TValue>(IPropertyMetadata property, Expression<Func<TEntity, TValue>> value)
    {
        var body = UnwrapConvert(value.Body);

        // Only a member read off the lambda parameter is a column reference. A member read off a captured
        // object (x => holder.Name) or a static property (x => Config.Default) happens to be a
        // MemberExpression too, but it is a value, not the entity's column; it is constant-folded below.
        if (body is MemberExpression { Expression: ParameterExpression source, Member: PropertyInfo valueProperty }
            && source == value.Parameters[0])
        {
            var mapped = FindProperty(valueProperty)
                ?? throw new BuildSqlCommandException($"Property {valueProperty.Name} of {typeof(TEntity)} is not mapped.");
            return UpdateAssignment.FromColumn(property, mapped);
        }

        if (!body.Has<ParameterExpression>())
        {
            var constant = Expression.Lambda<Func<object?>>(Expression.Convert(body, typeof(object))).Compile()();
            return UpdateAssignment.FromConstant(property, constant);
        }

        return UpdateAssignment.FromExpression(property, body);
    }

    private IPropertyMetadata ResolveWritableProperty(LambdaExpression column, string parameterName)
    {
        var body = UnwrapConvert(column.Body);

        if (body is not MemberExpression { Member: PropertyInfo property })
            throw new ArgumentException("The column selector must select a mapped property.", parameterName);

        var mapped = FindProperty(property)
            ?? throw new BuildSqlCommandException($"Property {property.Name} of {typeof(TEntity)} is not mapped.");

        if (mapped.IsComputed)
            throw new NotSupportedException($"Property {property.Name} of {typeof(TEntity)} is computed and cannot be written.");

        return mapped;
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

    private static Expression UnwrapConvert(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? UnwrapConvert(unary.Operand)
            : expression;

    private int Execute(UpdateCommand command)
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command);

        throw Unsupported();
    }

    private Task<int> ExecuteAsync(UpdateCommand command, CancellationToken cancellationToken)
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Execute(command, cancellationToken);

        throw Unsupported();
    }

    private IMutationExecutor RequireExecutor()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor;

        throw Unsupported();
    }

    private NotSupportedException Unsupported()
        => new(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");
}
