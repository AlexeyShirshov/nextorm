using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

/// <summary>
/// Fluent builder for an <c>INSERT</c> statement over a mapped entity, started with
/// <see cref="DataContextExtensions.InsertInto{TEntity}"/>. It collects the columns and values, renders
/// them as a parameterised statement through the active dialect and executes it on the context.
/// <para>
/// There is deliberately no change tracking: every terminal (<see cref="Insert"/>, <see cref="Returning()"/>,
/// <see cref="ReturningIdentity{TKey}()"/>, <see cref="ReturningKey{TKey}()"/>) issues one explicit command, as
/// with linq2db or <c>ExecuteNonQuery</c>. The builder is single-use: values accumulate until a terminal runs.
/// </para>
/// </summary>
/// <typeparam name="TEntity">The mapped entity type inserted.</typeparam>
public sealed partial class InsertBuilder<TEntity>
{
    private readonly IDataContext _dataContext;
    private readonly IEntityMetadata _metadata;
    private readonly List<ColumnAccumulator> _columns = [];
    private int _rowCount;
    private ValueMode _mode;
    private QueryCommand? _source;
    private IReadOnlyList<IPropertyMetadata>? _selectColumns;

    private enum ValueMode
    {
        None,
        Single,
        Entity,
        Mapped,
        Scalar,
        ColumnSequence,
        Select,
    }

    internal InsertBuilder(IDataContext dataContext, IEntityMetadata metadata)
    {
        _dataContext = dataContext;
        _metadata = metadata;
    }

    /// <summary>The context the insert executes on; used by <see cref="InsertReturningBuilder{TEntity, TResult}"/>.</summary>
    internal IDataContext DataContext => _dataContext;

    /// <summary>The number of rows this builder writes.</summary>
    internal int RowCount => _rowCount;

    /// <summary>Executes the insert and returns the number of affected rows.</summary>
    /// <returns>The number of rows inserted, as reported by the provider.</returns>
    public int Insert()
    {
        var executor = RequireExecutor();
        return executor.Execute(BuildCommand(null));
    }

    /// <summary>Asynchronously executes the insert and returns the number of affected rows.</summary>
    /// <param name="cancellationToken">Cancels execution.</param>
    /// <returns>A task producing the number of rows inserted.</returns>
    public Task<int> InsertAsync(CancellationToken cancellationToken = default)
    {
        var executor = RequireExecutor();
        return executor.Execute(BuildCommand(null), cancellationToken);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the generated identity of
    /// <paramref name="selector"/> through the provider's <c>RETURNING</c>, <c>OUTPUT</c> or
    /// <c>LAST_INSERT_ID</c> form.
    /// </summary>
    /// <typeparam name="TKey">The generated key's CLR type.</typeparam>
    /// <param name="selector">Selects the declared identity column.</param>
    /// <returns>A returning builder whose terminals produce the generated key.</returns>
    /// <exception cref="InvalidOperationException">The selected property is not declared as an identity.</exception>
    public InsertReturningBuilder<TEntity, TKey> ReturningIdentity<TKey>(Expression<Func<TEntity, TKey>> selector)
    {
        ArgumentNullException.ThrowIfNull(selector);
        var property = ResolveIdentityProperty(selector, nameof(selector));
        var (columns, selectList, oneColumn) = InsertReturningBuilder<TEntity, TKey>.ParseProjection(selector, _metadata.Properties, FindProperty);
        return new InsertReturningBuilder<TEntity, TKey>(this, columns, selectList, oneColumn, property, identityFunction: false, projection: selector);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the last generated identity
    /// read through the provider's scalar identity function (<c>SCOPE_IDENTITY()</c>, <c>lastval()</c>,
    /// <c>LAST_INSERT_ID()</c>, <c>last_insert_rowid()</c>), without naming the column. Useful when the
    /// identity is not mapped or the provider has no <c>RETURNING</c>/<c>OUTPUT</c>.
    /// </summary>
    /// <typeparam name="TKey">The generated key's CLR type.</typeparam>
    /// <returns>A returning builder whose terminals produce the generated key.</returns>
    /// <exception cref="NotSupportedException">The provider has no identity function (ClickHouse).</exception>
    public InsertReturningBuilder<TEntity, TKey> ReturningIdentity<TKey>()
        => new(this, [], [], false, identityColumn: null, identityFunction: true);

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the entity's key column,
    /// resolved from metadata (<c>[Key]</c>, the fluent <c>.Key()</c> or the <c>Id</c>/<c>&lt;Type&gt;Id</c>
    /// convention) through <c>RETURNING</c>, <c>OUTPUT</c> or the identity-function fallback, without a
    /// selector.
    /// </summary>
    /// <typeparam name="TKey">The key column's CLR type.</typeparam>
    /// <returns>A returning builder whose terminals produce the key value.</returns>
    /// <exception cref="InvalidOperationException">The entity has no key, or <typeparamref name="TKey"/> does not match it.</exception>
    public InsertReturningBuilder<TEntity, TKey> ReturningKey<TKey>()
    {
        var key = ResolveKeyProperty();
        EnsureKeyType<TKey>(key);
        var selectList = InsertReturningBuilder<TEntity, TKey>.BuildSelectList([key], [key.PropertyInfo]);
        return new InsertReturningBuilder<TEntity, TKey>(this, [key], selectList, true, key, identityFunction: false);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises the whole inserted entity
    /// through the provider's <c>RETURNING</c>/<c>OUTPUT</c> form (the equivalent of
    /// <c>RETURNING *</c> over the mapped columns).
    /// </summary>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TEntity"/>.</returns>
    public InsertReturningBuilder<TEntity, TEntity> Returning()
    {
        var parameter = Expression.Parameter(typeof(TEntity), "x");
        var identity = Expression.Lambda<Func<TEntity, TEntity>>(parameter, parameter);
        var (columns, selectList, oneColumn) = InsertReturningBuilder<TEntity, TEntity>.ParseProjection(identity, _metadata.Properties, FindProperty);
        return new InsertReturningBuilder<TEntity, TEntity>(this, columns, selectList, oneColumn, projection: identity);
    }

    /// <summary>
    /// Switches the builder to a row-returning terminal that materialises a projection of the inserted
    /// row through the provider's <c>RETURNING</c>/<c>OUTPUT</c> form. The projection may be the
    /// identity, a single mapped property, an anonymous type, a positional constructor or a
    /// member-init; it may only reference mapped properties.
    /// </summary>
    /// <typeparam name="TResult">The projected row shape.</typeparam>
    /// <param name="projection">Selects the mapped columns to return.</param>
    /// <returns>A returning builder whose terminals produce <typeparamref name="TResult"/>.</returns>
    /// <exception cref="NotSupportedException">The projection references something other than mapped properties.</exception>
    public InsertReturningBuilder<TEntity, TResult> Returning<TResult>(Expression<Func<TEntity, TResult>> projection)
    {
        ArgumentNullException.ThrowIfNull(projection);
        var (columns, selectList, oneColumn) = InsertReturningBuilder<TEntity, TResult>.ParseProjection(projection, _metadata.Properties, FindProperty);
        return new InsertReturningBuilder<TEntity, TResult>(this, columns, selectList, oneColumn, projection: projection);
    }

    /// <summary>
    /// Renders the parameterised SQL this builder would execute for a plain insert, without executing
    /// it. Useful for diagnostics and for verifying SQL generation without a database.
    /// </summary>
    /// <returns>The rendered SQL text.</returns>
    public string ToSql()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor.Render(BuildCommand(null));

        throw new NotSupportedException($"{_dataContext.GetType().Name} cannot render SQL: it is not a database-backed context.");
    }

    private void EnterSingleValueMode()
    {
        if (_mode is not (ValueMode.None or ValueMode.Single))
            throw new InvalidOperationException("Value cannot be combined with Values or another values form in the same insert.");

        _mode = ValueMode.Single;
    }

    private void EnterExclusiveMode(ValueMode mode)
    {
        if (_mode != ValueMode.None || _columns.Count > 0)
            throw new InvalidOperationException("Values cannot be combined with Value or another values form in the same insert.");

        _mode = mode;
    }

    private void EnterColumnSequenceMode()
    {
        if (_mode is not (ValueMode.None or ValueMode.ColumnSequence))
            throw new InvalidOperationException("Values cannot be combined with Value or another values form in the same insert.");

        _mode = ValueMode.ColumnSequence;
    }

    private (IPropertyMetadata Property, Func<TSource, object?>? GetValue)[] ParseMapping<TSource, TResult>(Expression<Func<TSource, TResult>> mapping)
    {
        var members = ExtractMappingMembers(mapping);
        var parameter = mapping.Parameters[0];

        var bindings = new (IPropertyMetadata Property, Func<TSource, object?>? GetValue)[members.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < members.Count; i++)
        {
            var (name, valueExpression) = members[i];
            EnsureUniqueColumn(seen, name);
            var property = ResolveWritableTarget(name);

            if (UnwrapConvert(valueExpression).Type == typeof(SqlDefault))
            {
                bindings[i] = (property, null);
                continue;
            }

            var getValue = Expression.Lambda<Func<TSource, object?>>(
                Expression.Convert(valueExpression, typeof(object)), parameter).Compile();
            bindings[i] = (property, getValue);
        }

        return bindings;
    }

    private IReadOnlyList<IPropertyMetadata> ParseSelectMapping<TSource, TResult>(Expression<Func<TSource, TResult>> mapping)
    {
        var members = ExtractMappingMembers(mapping);

        var columns = new IPropertyMetadata[members.Count];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var i = 0; i < members.Count; i++)
        {
            var (name, valueExpression) = members[i];
            EnsureUniqueColumn(seen, name);

            if (UnwrapConvert(valueExpression).Type == typeof(SqlDefault))
                throw new NotSupportedException("DEFAULT is not valid in an INSERT ... SELECT source; select a value instead.");

            columns[i] = ResolveWritableTarget(name);
        }

        return columns;
    }

    private static void EnsureUniqueColumn(HashSet<string> seen, string name)
    {
        if (!seen.Add(name))
            throw new InvalidOperationException($"Column {name} is mapped more than once.");
    }

    private IPropertyMetadata ResolveWritableTarget(string name)
    {
        var property = FindPropertyByName(name)
            ?? throw new BuildSqlCommandException($"Property {name} of {typeof(TEntity)} is not mapped.");

        if (property.IsComputed)
            throw new NotSupportedException($"Property {name} of {typeof(TEntity)} is computed and cannot be written.");

        return property;
    }

    private static List<(string Name, Expression Value)> ExtractMappingMembers(LambdaExpression mapping)
    {
        var body = UnwrapConvert(mapping.Body);
        var members = new List<(string Name, Expression Value)>();

        if (body is NewExpression { Members: { } newMembers } newExpression)
        {
            for (var i = 0; i < newExpression.Arguments.Count; i++)
                members.Add((newMembers[i].Name, newExpression.Arguments[i]));
        }
        else if (body is MemberInitExpression { Bindings.Count: > 0 } memberInit)
        {
            foreach (var binding in memberInit.Bindings)
            {
                if (binding is not MemberAssignment assignment)
                    throw new NotSupportedException("A Values mapping may only assign mapped properties.");

                members.Add((assignment.Member.Name, assignment.Expression));
            }
        }
        else
        {
            throw new NotSupportedException(
                "A Values mapping must be an object initializer (new TEntity { ... }) or an anonymous type (new { ... }).");
        }

        if (members.Count == 0)
            throw new NotSupportedException("A Values mapping must select at least one column.");

        return members;
    }

    private IPropertyMetadata ResolveSingleWritableProperty()
    {
        IPropertyMetadata? writable = null;
        var count = 0;
        foreach (var property in _metadata.Properties)
        {
            if (property.IsIdentity || property.IsComputed)
                continue;

            writable = property;
            count++;
        }

        return count switch
        {
            0 => throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no writable column; insert the all-defaults row with Insert() instead of a scalar value."),
            > 1 => throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has {count} writable columns; pass a mapping, e.g. Values(source, s => new {{ s.Column }}), or name the column, e.g. Values(x => x.Column, values)."),
            _ => writable!,
        };
    }

    private IPropertyMetadata? FindPropertyByName(string name)
    {
        foreach (var property in _metadata.Properties)
        {
            if (string.Equals(property.PropertyInfo.Name, name, StringComparison.Ordinal))
                return property;
        }

        return null;
    }

    private IPropertyMetadata ResolveProperty(LambdaExpression column, string parameterName)
    {
        var body = UnwrapConvert(column.Body);

        if (body is not MemberExpression { Member: PropertyInfo property })
            throw new ArgumentException("The column selector must select a mapped property.", parameterName);

        return FindProperty(property)
            ?? throw new BuildSqlCommandException($"Property {property.Name} of {typeof(TEntity)} is not mapped.");
    }

    private IPropertyMetadata ResolveWritableColumn(LambdaExpression column, string parameterName)
    {
        var property = ResolveProperty(column, parameterName);

        if (property.IsComputed)
            throw new NotSupportedException($"Property {property.PropertyInfo.Name} of {typeof(TEntity)} is computed and cannot be written.");

        return property;
    }

    // The generated key is read back either from the identity column itself (RETURNING/OUTPUT) or from
    // LAST_INSERT_ID() (MySQL/MariaDB, always the auto-increment column). The selector must therefore
    // name the declared identity column; otherwise the requested column and the returned value diverge.
    private IPropertyMetadata ResolveIdentityProperty(LambdaExpression keySelector, string parameterName)
    {
        var property = ResolveProperty(keySelector, parameterName);

        if (!property.IsIdentity)
            throw new InvalidOperationException(
                $"Property {property.PropertyInfo.Name} of {typeof(TEntity)} is not declared as database-generated. "
                + $"Mark it with [DatabaseGenerated(DatabaseGeneratedOption.Identity)] or .Identity() before using ReturningIdentity.");

        return property;
    }

    // The key column is resolved from metadata: an explicit [Key]/.Key() wins, otherwise the Id/<Type>Id
    // convention. A composite key cannot be addressed without a selector, so it is rejected.
    private IPropertyMetadata ResolveKeyProperty()
    {
        IPropertyMetadata? key = null;

        foreach (var property in _metadata.Properties)
        {
            if (!property.IsKey)
                continue;

            if (key is not null)
                throw new NotSupportedException(
                    $"Entity {typeof(TEntity)} declares more than one key; project the key column explicitly, e.g. Returning(x => x.Id).");

            key = property;
        }

        return key
            ?? throw new InvalidOperationException(
                $"Entity {typeof(TEntity)} has no key property. Mark one with [Key]/.Key() or use ReturningKey on a keyed entity.");
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

    /// <summary>Builds the insert command for use as a side-effecting step of a batch.</summary>
    /// <returns>The insert command.</returns>
    internal MutationCommand BuildBatchCommand() => BuildCommand(null);

    /// <summary>Builds the command for a <see cref="Returning()"/> terminal. Internal so the returning builder can reach the parent's state.</summary>
    /// <param name="returningColumns">The mapped columns to return through <c>RETURNING</c>/<c>OUTPUT</c>.</param>
    /// <param name="outputInto">The <c>OUTPUT ... INTO</c> target, or <see langword="null"/>.</param>
    /// <returns>The insert command carrying the returned columns.</returns>
    internal InsertCommand BuildReturningCommand(IReadOnlyList<IPropertyMetadata> returningColumns, OutputIntoClause? outputInto = null)
        => BuildCommand(null, returningColumns, outputInto);

    /// <summary>Builds the command for an <c>OUTPUT ... INTO</c>-only terminal: the rows are written into the target and nothing is returned to the client.</summary>
    /// <param name="outputColumns">The mapped columns written into the target.</param>
    /// <param name="targetTable">The raw (unquoted) target table name.</param>
    /// <returns>The insert command carrying the output-into target.</returns>
    internal InsertCommand BuildOutputIntoCommand(IReadOnlyList<IPropertyMetadata> outputColumns, string targetTable)
        => BuildCommand(null, null, new OutputIntoClause(targetTable, outputColumns));

    /// <summary>Builds the command carrying a single generated column for the identity/key terminals.</summary>
    /// <param name="identityColumn">The identity/key column to return.</param>
    /// <returns>The insert command carrying the column.</returns>
    internal InsertCommand BuildIdentityCommand(IPropertyMetadata identityColumn)
        => BuildCommand(identityColumn);

    private ColumnAccumulator GetOrAddColumn(IPropertyMetadata property)
    {
        foreach (var column in _columns)
        {
            if (column.Property.PropertyInfo == property.PropertyInfo)
                return column;
        }

        var accumulator = new ColumnAccumulator { Property = property };
        _columns.Add(accumulator);
        return accumulator;
    }

    private InsertCommand BuildCommand(IPropertyMetadata? identityColumn, IReadOnlyList<IPropertyMetadata>? returningColumns = null, OutputIntoClause? outputInto = null)
    {
        if (_source is not null)
            return new InsertCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, [], 1, identityColumn, returningColumns, _source, _selectColumns, outputInto: outputInto);

        if (_columns.Count == 0)
        {
            // No writable column means the row is defined entirely by column defaults, so an insert
            // with no values is an all-defaults row rather than a builder that forgot its values.
            if (HasWritableColumns())
                throw new InvalidOperationException("No values were specified for the insert.");

            return new InsertCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, [], 1, identityColumn, returningColumns, outputInto: outputInto);
        }

        foreach (var column in _columns)
        {
            if (column.Values.Count != _rowCount)
                throw new BuildSqlCommandException($"Column {column.Property.PropertyInfo.Name} has {column.Values.Count} values but the insert writes {_rowCount} row(s).");
        }

        var columns = new InsertColumn[_columns.Count];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = new InsertColumn(_columns[i].Property, _columns[i].Values);

        return new InsertCommand(typeof(TEntity), _metadata.TableName!, _metadata.IsTableNameAuto, columns, _rowCount, identityColumn, returningColumns, outputInto: outputInto);
    }

    private bool HasWritableColumns()
    {
        foreach (var property in _metadata.Properties)
        {
            if (!property.IsIdentity && !property.IsComputed)
                return true;
        }

        return false;
    }

    private IMutationExecutor RequireExecutor()
    {
        if (_dataContext is IMutationExecutor executor)
            return executor;

        throw new NotSupportedException(
            $"{_dataContext.GetType().Name} does not support data modification. Use a database-backed context (SQLite, PostgreSQL, SQL Server, MySQL, MariaDB or ClickHouse); the in-memory provider is read-only.");
    }

    private static Expression UnwrapConvert(Expression expression)
        => expression is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } unary
            ? UnwrapConvert(unary.Operand)
            : expression;

    /// <summary>Converts a boxed identity value to <typeparamref name="TKey"/>, tolerating <see langword="null"/>.</summary>
    /// <typeparam name="TKey">The target CLR type.</typeparam>
    /// <param name="value">The boxed provider value.</param>
    /// <returns>The converted value.</returns>
    internal static TKey ConvertIdentity<TKey>(object? value)
    {
        if (value is TKey key)
            return key;

        if (value is null or DBNull)
            return default!;

        var target = Nullable.GetUnderlyingType(typeof(TKey)) ?? typeof(TKey);
        return (TKey)Convert.ChangeType(value, target, CultureInfo.InvariantCulture)!;
    }

    private sealed class ColumnAccumulator
    {
        public required IPropertyMetadata Property { get; init; }
        public List<InsertValue> Values { get; } = [];
    }
}
