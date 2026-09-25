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
    private DurationUnit? _durationUnit;
    private int _durationPrecision;
    private string? _collation;
    private IPropertyValueConverter? _converter;
    private JsonColumnOptions? _jsonOptions;

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
    /// Declares the unit in which a <see cref="System.TimeSpan"/> property is stored on a provider
    /// without a native duration type. Providers with a native type (PostgreSQL <c>interval</c>,
    /// MySQL/MariaDB <c>TIME</c>) ignore the unit.
    /// </summary>
    /// <param name="unit">The storage unit of the integer value.</param>
    /// <param name="precision">The fractional-second precision of the native type; zero for the provider default.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> Duration(DurationUnit unit, int precision = 0)
    {
        _durationUnit = unit;
        _durationPrecision = precision;
        return this;
    }

    /// <summary>
    /// Declares the provider-native collation of the selected property's column. It is applied to the
    /// column in collation-sensitive query operations (comparison, <c>LIKE</c>, <c>ORDER BY</c>,
    /// <c>GROUP BY</c>) on a provider that supports a per-expression <c>COLLATE</c> clause
    /// (<see cref="ISqlDialect.SupportsCollation"/>).
    /// </summary>
    /// <param name="collation">The provider-native collation name.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> Collation(string collation)
    {
        _collation = collation;
        return this;
    }

    /// <summary>
    /// Declares the value converter that maps the selected property between its CLR type and the
    /// provider representation.
    /// </summary>
    /// <param name="converter">The converter to apply; must produce the property's CLR type on read.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> HasConversion(IPropertyValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converter = converter;
        _jsonOptions = null;
        return this;
    }

    /// <summary>
    /// Declares a strongly typed value converter for the selected property.
    /// </summary>
    /// <typeparam name="TModel">The CLR model type of the property.</typeparam>
    /// <typeparam name="TProvider">The provider-side representation.</typeparam>
    /// <param name="converter">The converter to apply.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> HasConversion<TModel, TProvider>(ValueConverter<TModel, TProvider> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        _converter = converter;
        _jsonOptions = null;
        return this;
    }

    /// <summary>
    /// Declares a value converter from a pair of conversion expressions instead of a converter class.
    /// </summary>
    /// <typeparam name="TModel">The CLR model type of the property.</typeparam>
    /// <typeparam name="TProvider">The provider-side representation.</typeparam>
    /// <param name="toProvider">Converts a model value to the provider representation.</param>
    /// <param name="fromProvider">Converts a provider value back to the model representation.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> HasConversion<TModel, TProvider>(
        Expression<Func<TModel, TProvider>> toProvider,
        Expression<Func<TProvider, TModel>> fromProvider)
    {
        ArgumentNullException.ThrowIfNull(toProvider);
        ArgumentNullException.ThrowIfNull(fromProvider);
        _converter = new ExpressionValueConverter<TModel, TProvider>(toProvider, fromProvider);
        _jsonOptions = null;
        return this;
    }

    /// <summary>
    /// Declares the selected property as stored in a JSON column and serialized with
    /// <c>System.Text.Json</c>.
    /// </summary>
    /// <param name="configure">Configures the storage form and serializer options, or <see langword="null"/> for the defaults.</param>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> JsonColumn(Action<JsonColumnOptions>? configure = null)
    {
        var options = new JsonColumnOptions();
        configure?.Invoke(options);
        _jsonOptions = options;
        _converter = null;
        return this;
    }

    /// <summary>
    /// Resolves the selected property's <see cref="PropertyInfo"/> and produces its mapping metadata.
    /// </summary>
    /// <returns>The property's mapping metadata.</returns>
    /// <exception cref="InvalidOperationException">The selector does not produce a <see cref="PropertyInfo"/>, or the converter's model type does not match the property type.</exception>
    public IPropertyMetadata Build()
    {
        var miVisitor = new MemberExpressionVisitor();
        miVisitor.Visit(_propertySelector);
        var pi = (PropertyInfo)miVisitor.MemberInfo! ?? throw new InvalidOperationException($"Expression {_propertySelector} does not produce PropertyInfo");
        var converter = _jsonOptions is not null
            ? JsonColumnConverterFactory.Create(pi.PropertyType, _jsonOptions.Storage, _jsonOptions.Options)
            : _converter;
        ValidateConverterModel(pi, converter);
        var r = new PropertyMetadata() { ColumnName = _columnName!, PropertyInfo = pi, IsColumnNameAuto = false, IsKey = _isKey, IsIdentity = _isIdentity, IsComputed = _isComputed, DurationUnit = _durationUnit, DurationPrecision = _durationPrecision, Collation = _collation, Converter = converter };
        return r;
    }

    private static void ValidateConverterModel(PropertyInfo property, IPropertyValueConverter? converter)
    {
        if (converter is null)
            return;

        var modelType = ValueConverterReflection.GetModelType(converter.GetType());
        var expectedModelType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;
        if (modelType is not null && modelType != property.PropertyType && modelType != expectedModelType)
            throw new InvalidOperationException($"Value converter {converter.GetType()} maps {modelType} but property '{property.Name}' has type {property.PropertyType}.");
    }
}
