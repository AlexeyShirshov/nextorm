using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent builder for a multi-table <c>UPDATE</c> (the <c>UPDATE ... FROM</c>/join form), started with
/// <see cref="DataContextExtensions.UpdateJoin{T1, T2}(JoinedEntityBuilder{T1, T2})"/>. The target is
/// the first table of the join chain; the columns to write are supplied with <c>Set(...)</c>, whose
/// right-hand side may read any joined table. The rows to change are selected by the join and an
/// optional <see cref="Where"/>.
/// <para>
/// Only INNER joins are supported: the <c>FROM</c>/join form folds the join conditions into the
/// <c>WHERE</c>, so an outer join would silently change which rows are updated. There is deliberately no
/// change tracking — the terminal issues exactly one explicit command.
/// </para>
/// </summary>
/// <typeparam name="TProjection">The positional join projection (<c>Projection&lt;T1, ...&gt;</c>).</typeparam>
public sealed class UpdateJoinBuilder<TProjection>
{
    private readonly Type _targetType;
    private readonly List<UpdateJoinAssignment> _assignments = [];
    private EntityBuilder<TProjection> _query;

    internal UpdateJoinBuilder(EntityBuilder<TProjection> query, Type targetType)
    {
        _query = query;
        _targetType = targetType;
    }

    /// <summary>
    /// Assigns a constant value to a mapped column of the target (first) table. The value is always bound
    /// as a parameter. Repeating the call for the same column replaces the earlier assignment.
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the target-table column to write (for example <c>p =&gt; p.Item1.Name</c>).</param>
    /// <param name="value">The value to write, or <see langword="null"/> for SQL <c>NULL</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException">The selector does not target a column of the first table.</exception>
    public UpdateJoinBuilder<TProjection> Set<TValue>(Expression<Func<TProjection, TValue>> column, TValue value)
    {
        ArgumentNullException.ThrowIfNull(column);
        SetAssignment(UpdateJoinAssignment.FromConstant(ResolveTarget(column), value));
        return this;
    }

    /// <summary>
    /// Assigns the result of an expression to a mapped column of the target (first) table. The expression
    /// is rendered by the same translator as a <c>SELECT</c> projection, so it may reference any joined
    /// table's columns (for example <c>Set(p =&gt; p.Item1.Total, p =&gt; p.Item1.Total + p.Item2.Amount)</c>)
    /// and captured variables (which become parameters).
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the target-table column to write (for example <c>p =&gt; p.Item1.Name</c>).</param>
    /// <param name="value">The value expression to evaluate.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="ArgumentException">The selector does not target a column of the first table.</exception>
    public UpdateJoinBuilder<TProjection> Set<TValue>(Expression<Func<TProjection, TValue>> column, Expression<Func<TProjection, TValue>> value)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(value);
        SetAssignment(BuildExpression(ResolveTarget(column), value));
        return this;
    }

    /// <summary>
    /// Restricts the update to the rows satisfying <paramref name="predicate"/>. The predicate is
    /// translated by the same expression pipeline as a <c>SELECT</c> <c>WHERE</c> and may reference any
    /// joined table. Repeating the call combines the predicates with <c>and</c>.
    /// </summary>
    /// <param name="predicate">The condition each updated row must satisfy.</param>
    /// <returns>This builder, for chaining.</returns>
    public UpdateJoinBuilder<TProjection> Where(Expression<Func<TProjection, bool>> predicate)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        _query = _query.Where(predicate);
        return this;
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute, without executing it. Useful for
    /// diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    /// <exception cref="InvalidOperationException">No assignment was specified.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed, the provider cannot express a multi-table <c>UPDATE</c>, or a join is not an INNER join.</exception>
    public string ToSql()
    {
        if (_query.DataProvider is IMutationExecutor executor)
            return executor.Render(BuildCommand());

        throw Unsupported();
    }

    /// <summary>Executes the update and returns the number of affected rows.</summary>
    /// <returns>The number of updated rows, as reported by the provider.</returns>
    /// <exception cref="InvalidOperationException">No assignment was specified.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed, the provider cannot express a multi-table <c>UPDATE</c>, or a join is not an INNER join.</exception>
    public int Update()
        => RequireExecutor().Execute(BuildCommand());

    /// <summary>Asynchronously executes the update and returns the number of affected rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of updated rows.</returns>
    /// <exception cref="InvalidOperationException">No assignment was specified.</exception>
    /// <exception cref="NotSupportedException">The context is not database-backed, the provider cannot express a multi-table <c>UPDATE</c>, or a join is not an INNER join.</exception>
    public Task<int> UpdateAsync(CancellationToken cancellationToken = default)
        => RequireExecutor().Execute(BuildCommand(), cancellationToken);

    private UpdateJoinCommand BuildCommand()
    {
        if (_assignments.Count == 0)
            throw new InvalidOperationException("An update needs at least one assignment; call Set(...).");

        var joins = _query.Joins;
        if (joins is not { Count: > 0 })
            throw new InvalidOperationException("A multi-table update needs a join; use Update<T>() for a single-table update.");

        ValidateTargets();

        // Only the source, joins and condition of this command are used; the projection is a placeholder
        // and column preparation is skipped (IgnoreColumns), exactly like the multi-table DELETE source.
        var source = JoinedMutationSource.Prepare(_query, "UPDATE");
        return new UpdateJoinCommand(_targetType, source, [.. _assignments]);
    }

    private void SetAssignment(UpdateJoinAssignment assignment)
    {
        for (var i = 0; i < _assignments.Count; i++)
        {
            if (SameTarget(_assignments[i].Target, assignment.Target))
            {
                _assignments[i] = assignment;
                return;
            }
        }

        _assignments.Add(assignment);
    }

    // The selector must name a mapped, writable column of the target (first) table. Unmapped and computed
    // columns are rejected here rather than letting an opaque SQL error surface at execution time.
    private void ValidateTargets()
    {
        var mapped = DataContextCache.Metadata.TryGetValue(_targetType, out var metadata) ? metadata : null;

        for (var i = 0; i < _assignments.Count; i++)
        {
            if (_assignments[i].Target is not MemberExpression { Member: PropertyInfo property })
                throw new BuildSqlCommandException("A multi-table UPDATE assignment must target a mapped column of the first table.");

            var writable = FindProperty(mapped, property);
            if (writable is null)
                throw new BuildSqlCommandException($"Property {property.Name} of {_targetType.Name} is not mapped.");

            if (writable.IsComputed)
                throw new NotSupportedException($"Property {property.Name} of {_targetType.Name} is computed and cannot be written.");
        }
    }

    private static IPropertyMetadata? FindProperty(IEntityMetadata? metadata, PropertyInfo property)
    {
        if (metadata is null)
            return null;

        for (var i = 0; i < metadata.Properties.Count; i++)
        {
            if (metadata.Properties[i].PropertyInfo == property)
                return metadata.Properties[i];
        }

        return null;
    }

    private static Expression ResolveTarget(LambdaExpression column)
    {
        var body = UnwrapConvert(column.Body);

        // The selector must read a column off the projection's first item (the target table).
        if (body is MemberExpression { Member: PropertyInfo, Expression: MemberExpression { Member: PropertyInfo item } } && item.Name == "Item1")
            return body;

        throw new ArgumentException(
            "The column selector must select a mapped column of the first (target) table, for example p => p.Item1.Name.", nameof(column));
    }

    private static UpdateJoinAssignment BuildExpression(Expression target, LambdaExpression value)
    {
        var body = UnwrapConvert(value.Body);

        // A value expression with no projection parameter is a captured constant (a local, a captured
        // object's member, a static property); fold it into a parameter, mirroring the single-table Set.
        if (!body.Has<ParameterExpression>())
        {
            var constant = Expression.Lambda<Func<object?>>(Expression.Convert(body, typeof(object))).Compile()();
            return UpdateJoinAssignment.FromConstant(target, constant);
        }

        return UpdateJoinAssignment.FromExpression(target, body);
    }

    private static Expression UnwrapConvert(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? UnwrapConvert(unary.Operand)
            : expression;

    private static bool SameTarget(Expression left, Expression right)
        => left is MemberExpression { Member: PropertyInfo leftProperty, Expression: MemberExpression leftItem }
            && right is MemberExpression { Member: PropertyInfo rightProperty, Expression: MemberExpression rightItem }
            && leftProperty == rightProperty
            && leftItem.Member == rightItem.Member;

    private IMutationExecutor RequireExecutor()
    {
        if (_query.DataProvider is IMutationExecutor executor)
            return executor;

        throw Unsupported();
    }

    private NotSupportedException Unsupported()
        => new(
            $"{_query.DataProvider.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL or MariaDB).");
}
