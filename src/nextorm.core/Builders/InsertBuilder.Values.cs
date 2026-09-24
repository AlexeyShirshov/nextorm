using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace NextORM.Core;

public sealed partial class InsertBuilder<TEntity>
{
    /// <summary>
    /// Adds a column and its constant value. Chain one call per column for a single-row insert.
    /// The value is always bound as a parameter, never inlined into the SQL.
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the mapped column to write.</param>
    /// <param name="value">The value to write, or <see langword="null"/> for SQL <c>NULL</c>.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses <see cref="Values(TEntity)"/>.</exception>
    public InsertBuilder<TEntity> Value<TValue>(Expression<Func<TEntity, TValue>> column, TValue value)
    {
        ArgumentNullException.ThrowIfNull(column);
        EnterSingleValueMode();

        GetOrAddColumn(ResolveWritableColumn(column, nameof(column))).Values.Add(InsertValue.FromConstant(value));
        _rowCount = 1;
        return this;
    }

    /// <summary>
    /// Adds a column and writes its database <c>DEFAULT</c> instead of a value, for a single-row insert.
    /// A provider whose dialect cannot render the <c>DEFAULT</c> keyword rejects it with
    /// <see cref="NotSupportedException"/>; omit the column then to obtain the same default.
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the mapped column whose default is written.</param>
    /// <param name="value">The <see cref="SqlDefault.Value"/> marker; it carries no data.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses another values form.</exception>
    /// <exception cref="NotSupportedException">The provider cannot render the <c>DEFAULT</c> keyword.</exception>
    public InsertBuilder<TEntity> Value<TValue>(Expression<Func<TEntity, TValue>> column, SqlDefault value)
    {
        ArgumentNullException.ThrowIfNull(column);
        EnterSingleValueMode();

        GetOrAddColumn(ResolveWritableColumn(column, nameof(column))).Values.Add(InsertValue.FromDefault());
        _rowCount = 1;
        return this;
    }

    /// <summary>
    /// Adds a column and copies the value of another mapped column of the same entity into it, or writes
    /// a parameter-free expression (for example <c>x =&gt; DateTime.Now</c>) folded into a parameter.
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the mapped column to write.</param>
    /// <param name="value">Selects the mapped column to read the value from.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses <see cref="Values(TEntity)"/>.</exception>
    /// <exception cref="NotSupportedException">The value expression is neither a mapped property nor a constant.</exception>
    public InsertBuilder<TEntity> Value<TValue>(Expression<Func<TEntity, TValue>> column, Expression<Func<TEntity, TValue>> value)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(value);
        EnterSingleValueMode();

        var body = UnwrapConvert(value.Body);

        // Only a member read off the lambda parameter is a column reference. A static property
        // (x => DateTime.Now) or a member read off a captured object (x => holder.Name) happens to be a
        // MemberExpression too, but it is a value, not the entity's column; it is constant-folded below.
        if (body is MemberExpression { Expression: ParameterExpression source, Member: PropertyInfo valueProperty }
            && source == value.Parameters[0])
        {
            var mapped = FindProperty(valueProperty)
                ?? throw new BuildSqlCommandException($"Property {valueProperty.Name} of {typeof(TEntity)} is not mapped.");
            GetOrAddColumn(ResolveWritableColumn(column, nameof(column))).Values.Add(InsertValue.FromColumn(mapped));
        }
        else if (!body.Has<ParameterExpression>())
        {
            var constant = Expression.Lambda<Func<object?>>(Expression.Convert(body, typeof(object))).Compile()();
            GetOrAddColumn(ResolveWritableColumn(column, nameof(column))).Values.Add(InsertValue.FromConstant(constant));
        }
        else
        {
            throw new NotSupportedException("An INSERT value expression must be a mapped property reference or a parameter-free expression.");
        }

        _rowCount = 1;
        return this;
    }

    /// <summary>
    /// Writes all mapped, writable columns of <paramref name="entity"/>. Identity and computed columns
    /// are excluded automatically.
    /// </summary>
    /// <param name="entity">The entity whose column values are written.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses <see cref="Value{TValue}(Expression{Func{TEntity, TValue}}, TValue)"/>.</exception>
    [OverloadResolutionPriority(1)]
    public InsertBuilder<TEntity> Values(TEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Values([entity]);
    }

    /// <summary>
    /// Writes all mapped, writable columns of every entity in a multi-row insert. Identity and computed
    /// columns are excluded automatically.
    /// </summary>
    /// <param name="entities">The entities whose column values are written.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses <see cref="Value{TValue}(Expression{Func{TEntity, TValue}}, TValue)"/>.</exception>
    [OverloadResolutionPriority(1)]
    public InsertBuilder<TEntity> Values(IEnumerable<TEntity> entities)
    {
        ArgumentNullException.ThrowIfNull(entities);
        EnterExclusiveMode(ValueMode.Entity);

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
            throw new BuildSqlCommandException($"Entity {typeof(TEntity)} has no writable column to insert.");

        _rowCount = list.Count;
        return this;
    }

    /// <summary>
    /// Writes one row per element of <paramref name="source"/>, mapping each row's columns through
    /// <paramref name="mapping"/>. A concrete entity uses an object initializer
    /// (<c>new Order { Id = d.Id, Name = d.Name }</c>); an interface-mapped entity uses an anonymous type
    /// (<c>new { Id = d.Id, Name = d.Name }</c>). Every mapped member's name must match a mapped property
    /// of <typeparamref name="TEntity"/>.
    /// </summary>
    /// <typeparam name="TSource">The source element type.</typeparam>
    /// <typeparam name="TResult">The mapping shape: the entity or an anonymous type.</typeparam>
    /// <param name="source">The rows to insert; iterated once.</param>
    /// <param name="mapping">An object initializer or anonymous type selecting the written columns.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses another values form, or a member is duplicated.</exception>
    /// <exception cref="NotSupportedException">A member is not a mapped write, or is a computed column.</exception>
    public InsertBuilder<TEntity> Values<TSource, TResult>(IEnumerable<TSource> source, Expression<Func<TSource, TResult>> mapping)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapping);
        EnterExclusiveMode(ValueMode.Mapped);

        var bindings = ParseMapping(mapping);
        var list = source as IReadOnlyList<TSource> ?? source.ToList();
        if (list.Count == 0)
            throw new ArgumentException("At least one row is required.", nameof(source));

        foreach (var (property, getValue) in bindings)
        {
            var accumulator = new ColumnAccumulator { Property = property };
            for (var i = 0; i < list.Count; i++)
                accumulator.Values.Add(getValue is null ? InsertValue.FromDefault() : InsertValue.FromConstant(getValue(list[i])));

            _columns.Add(accumulator);
        }

        _rowCount = list.Count;
        return this;
    }

    /// <summary>
    /// Writes the rows produced by a server-side query (<c>INSERT ... SELECT</c>). The mapping selects the
    /// target columns by member name exactly like <see cref="Values{TSource, TResult}(IEnumerable{TSource}, Expression{Func{TSource, TResult}})"/>,
    /// but the rows are read by the database rather than the client: the query's <c>SELECT</c> is embedded
    /// after the target column list. A concrete entity uses an object initializer
    /// (<c>new Order { Name = d.Name }</c>); an interface-mapped entity uses an anonymous type
    /// (<c>new { Name = d.Name }</c>).
    /// </summary>
    /// <typeparam name="TSource">The source element type of the query.</typeparam>
    /// <typeparam name="TResult">The mapping shape: the entity or an anonymous type.</typeparam>
    /// <param name="source">The query whose rows are inserted.</param>
    /// <param name="mapping">An object initializer or anonymous type selecting the written columns.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses another values form, or a member is duplicated.</exception>
    /// <exception cref="NotSupportedException">A member is computed, writes <c>DEFAULT</c>, or the query uses runtime <c>SqlFunctions.Parameter</c> placeholders.</exception>
    public InsertBuilder<TEntity> Values<TSource, TResult>(EntityBuilder<TSource> source, Expression<Func<TSource, TResult>> mapping)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(mapping);
        EnterExclusiveMode(ValueMode.Select);

        var columns = ParseSelectMapping(mapping);
        var query = source.Select(mapping);

        _source = query;
        _selectColumns = columns;
        _rowCount = 1;
        return this;
    }


    /// <summary>
    /// Writes a single scalar value into the entity's only writable column (the one that is neither
    /// identity nor computed), without a projection.
    /// </summary>
    /// <typeparam name="TScalar">The scalar value type.</typeparam>
    /// <param name="value">The value to write.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The entity does not have exactly one writable column.</exception>
    public InsertBuilder<TEntity> Value<TScalar>(TScalar value)
    {
        EnterExclusiveMode(ValueMode.Scalar);

        var accumulator = new ColumnAccumulator { Property = ResolveSingleWritableProperty() };
        accumulator.Values.Add(ToInsertValue(value));
        _columns.Add(accumulator);
        _rowCount = 1;
        return this;
    }

    /// <summary>
    /// Writes one row per value into the entity's only writable column (the one that is neither identity
    /// nor computed), without a projection.
    /// </summary>
    /// <typeparam name="TScalar">The scalar value type.</typeparam>
    /// <param name="values">The values to write, one per row.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The entity does not have exactly one writable column.</exception>
    public InsertBuilder<TEntity> Values<TScalar>(IEnumerable<TScalar> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        EnterExclusiveMode(ValueMode.Scalar);

        var list = values as IReadOnlyList<TScalar> ?? values.ToList();
        if (list.Count == 0)
            throw new ArgumentException("At least one value is required.", nameof(values));

        var accumulator = new ColumnAccumulator { Property = ResolveSingleWritableProperty() };
        for (var i = 0; i < list.Count; i++)
            accumulator.Values.Add(ToInsertValue(list[i]));

        _columns.Add(accumulator);
        _rowCount = list.Count;
        return this;
    }

    /// <summary>
    /// Adds one column's values to a multi-row insert, one value per row. Chain one call per column; every
    /// column must supply the same number of values. Use this when the entity has more than one writable
    /// column and the values are already column-oriented.
    /// </summary>
    /// <typeparam name="TValue">The column's CLR type.</typeparam>
    /// <param name="column">Selects the mapped column to write.</param>
    /// <param name="values">The values to write, one per row.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The builder already uses another values form, or the column is repeated.</exception>
    /// <exception cref="ArgumentException">The value count differs from the other columns.</exception>
    public InsertBuilder<TEntity> Values<TValue>(Expression<Func<TEntity, TValue>> column, IEnumerable<TValue> values)
    {
        ArgumentNullException.ThrowIfNull(column);
        ArgumentNullException.ThrowIfNull(values);
        EnterColumnSequenceMode();

        var property = ResolveWritableColumn(column, nameof(column));
        var list = values as IReadOnlyList<TValue> ?? values.ToList();
        if (list.Count == 0)
            throw new ArgumentException("At least one value is required.", nameof(values));

        foreach (var existing in _columns)
        {
            if (existing.Property.PropertyInfo == property.PropertyInfo)
                throw new InvalidOperationException($"Column {property.PropertyInfo.Name} is specified more than once.");
        }

        if (_columns.Count == 0)
            _rowCount = list.Count;
        else if (list.Count != _rowCount)
            throw new ArgumentException($"Every column must supply the same number of values; expected {_rowCount}, got {list.Count}.", nameof(values));

        var accumulator = new ColumnAccumulator { Property = property };
        for (var i = 0; i < list.Count; i++)
            accumulator.Values.Add(InsertValue.FromConstant(list[i]));

        _columns.Add(accumulator);
        return this;
    }

    private static InsertValue ToInsertValue<T>(T value)
        => value is SqlDefault ? InsertValue.FromDefault() : InsertValue.FromConstant(value);

}
