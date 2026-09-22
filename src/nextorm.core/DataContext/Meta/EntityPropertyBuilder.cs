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
    /// Resolves the selected property's <see cref="PropertyInfo"/> and produces its mapping metadata.
    /// </summary>
    /// <returns>The property's mapping metadata.</returns>
    /// <exception cref="InvalidOperationException">The selector does not produce a <see cref="PropertyInfo"/>.</exception>
    public IPropertyMetadata Build()
    {
        var miVisitor = new MemberExpressionVisitor();
        miVisitor.Visit(_propertySelector);
        var pi = (PropertyInfo)miVisitor.MemberInfo! ?? throw new InvalidOperationException($"Expression {_propertySelector} does not produce PropertyInfo");
        var r = new PropertyMetadata() { ColumnName = _columnName!, PropertyInfo = pi, IsColumnNameAuto = false };
        return r;
    }
}
