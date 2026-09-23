using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Fluent builder for a single property mapping, returned by
/// <see cref="EntityMetadataBuilder{T}.Property(System.Linq.Expressions.Expression{System.Func{T, object}})"/>.
/// </summary>
public class EntityPropertyBuilder<T>
{
    private readonly Expression<Func<T, object>> _propertySelector;
    private string? _columnName;
    private bool _isKey;
    private bool _isIdentity;
    private bool _isComputed;

    /// <summary>
    /// Creates a builder for the property selected by <paramref name="propertySelector"/>.
    /// </summary>
    /// <param name="propertySelector">Selects the property to map; the expression must produce a <see cref="PropertyInfo"/>.</param>
    public EntityPropertyBuilder(Expression<Func<T, object>> propertySelector)
    {
        _propertySelector = propertySelector;
    }

    /// <summary>
    /// Sets the column name the selected property maps to.
    /// </summary>
    /// <param name="columnName">The target column name.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> HasColumnName(string columnName)
    {
        _columnName = columnName;
        return this;
    }

    /// <summary>
    /// Marks the selected property as (part of) the entity key. The DML builders use the key to
    /// address a row; declaring it here avoids the <c>Id</c>/<c>&lt;TypeName&gt;Id</c> convention.
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> Key()
    {
        _isKey = true;
        return this;
    }

    /// <summary>
    /// Marks the selected property as database-generated (an identity/auto-increment column). The
    /// insert builders exclude it from the written values.
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> Identity()
    {
        _isIdentity = true;
        return this;
    }

    /// <summary>
    /// Marks the selected property as a database-generated computed column that can never be written.
    /// The insert builders exclude it from the written values.
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> Computed()
    {
        _isComputed = true;
        return this;
    }

    /// <summary>
    /// Resolves the selected property's <see cref="PropertyInfo"/> and produces its mapping metadata.
    /// </summary>
    /// <returns>The property's mapping metadata.</returns>
    /// <exception cref="InvalidOperationException">The selector does not produce a <see cref="PropertyInfo"/>.</exception>
    public IPropertyMetadata Build()
    {
        var miVisitor = new MemberExpressionVisitor();
        miVisitor.Visit(_propertySelector);
        var pi = (PropertyInfo)miVisitor.MemberInfo! ?? throw new InvalidOperationException($"Expression {_propertySelector} does not produce PropertyInfo");
        var r = new PropertyMetadata() { ColumnName = _columnName!, PropertyInfo = pi, IsColumnNameAuto = false, IsKey = _isKey, IsIdentity = _isIdentity, IsComputed = _isComputed };
        return r;
    }
}
