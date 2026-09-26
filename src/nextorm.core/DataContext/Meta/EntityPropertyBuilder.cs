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
    private RangeColumnsMetadata? _rangeColumns;
    private bool _isDynamicColumnsStore;

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
        if (_rangeColumns is not null)
            throw new InvalidOperationException(
                "A property mapping cannot combine HasColumnName with RangeColumns; the range pair declares its own lower/upper column names.");
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
    /// provider representation. Cannot be combined with
    /// <see cref="RangeColumns(string, string, bool, bool)"/>.
    /// </summary>
    /// <param name="converter">The converter to apply; must produce the property's CLR type on read.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The property is already mapped with <see cref="RangeColumns(string, string, bool, bool)"/>.</exception>
    public EntityPropertyBuilder<T> HasConversion(IPropertyValueConverter converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        EnsureNoRangeColumns(nameof(HasConversion));
        _converter = converter;
        _jsonOptions = null;
        return this;
    }

    /// <summary>
    /// Declares a strongly typed value converter for the selected property. Cannot be combined with
    /// <see cref="RangeColumns(string, string, bool, bool)"/>.
    /// </summary>
    /// <typeparam name="TModel">The CLR model type of the property.</typeparam>
    /// <typeparam name="TProvider">The provider-side representation.</typeparam>
    /// <param name="converter">The converter to apply.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The property is already mapped with <see cref="RangeColumns(string, string, bool, bool)"/>.</exception>
    public EntityPropertyBuilder<T> HasConversion<TModel, TProvider>(ValueConverter<TModel, TProvider> converter)
    {
        ArgumentNullException.ThrowIfNull(converter);
        EnsureNoRangeColumns(nameof(HasConversion));
        _converter = converter;
        _jsonOptions = null;
        return this;
    }

    /// <summary>
    /// Declares a value converter from a pair of conversion expressions instead of a converter class.
    /// Cannot be combined with <see cref="RangeColumns(string, string, bool, bool)"/>.
    /// </summary>
    /// <typeparam name="TModel">The CLR model type of the property.</typeparam>
    /// <typeparam name="TProvider">The provider-side representation.</typeparam>
    /// <param name="toProvider">Converts a model value to the provider representation.</param>
    /// <param name="fromProvider">Converts a provider value back to the model representation.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The property is already mapped with <see cref="RangeColumns(string, string, bool, bool)"/>.</exception>
    public EntityPropertyBuilder<T> HasConversion<TModel, TProvider>(
        Expression<Func<TModel, TProvider>> toProvider,
        Expression<Func<TProvider, TModel>> fromProvider)
    {
        ArgumentNullException.ThrowIfNull(toProvider);
        ArgumentNullException.ThrowIfNull(fromProvider);
        EnsureNoRangeColumns(nameof(HasConversion));
        _converter = new ExpressionValueConverter<TModel, TProvider>(toProvider, fromProvider);
        _jsonOptions = null;
        return this;
    }

    /// <summary>
    /// Declares that the selected <see cref="Range{T}"/> property is stored as a pair of scalar columns
    /// on a provider without a native range type. Mutually exclusive with
    /// <see cref="HasConversion(IPropertyValueConverter)"/> and <see cref="JsonColumn"/>: whichever is
    /// configured first is retained and the other call fails.
    /// </summary>
    /// <param name="lowerColumn">The column that stores the lower bound.</param>
    /// <param name="upperColumn">The column that stores the upper bound.</param>
    /// <param name="lowerInclusive">Whether the lower bound is part of the range.</param>
    /// <param name="upperInclusive">Whether the upper bound is part of the range.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The property is already mapped with <see cref="HasConversion(IPropertyValueConverter)"/> or <see cref="JsonColumn"/>.</exception>
    public EntityPropertyBuilder<T> RangeColumns(string lowerColumn, string upperColumn, bool lowerInclusive = true, bool upperInclusive = false)
    {
        ArgumentException.ThrowIfNullOrEmpty(lowerColumn);
        ArgumentException.ThrowIfNullOrEmpty(upperColumn);
        if (_columnName is not null)
            throw new InvalidOperationException(
                "A property mapping cannot combine RangeColumns with HasColumnName; the range pair declares its own lower/upper column names.");
        if (_converter is not null || _jsonOptions is not null)
            throw new InvalidOperationException(
                "A property mapping cannot combine RangeColumns with HasConversion or JsonColumn; a Range<T> column pair is stored without a value or JSON converter.");
        _rangeColumns = new RangeColumnsMetadata(lowerColumn, upperColumn, lowerInclusive, upperInclusive);
        return this;
    }

    /// <summary>
    /// Declares the selected property as stored in a JSON column and serialized with
    /// <c>System.Text.Json</c>. Cannot be combined with
    /// <see cref="RangeColumns(string, string, bool, bool)"/>.
    /// </summary>
    /// <param name="configure">Configures the storage form and serializer options, or <see langword="null"/> for the defaults.</param>
    /// <returns>This builder, for chaining.</returns>
    /// <exception cref="InvalidOperationException">The property is already mapped with <see cref="RangeColumns(string, string, bool, bool)"/>.</exception>
    public EntityPropertyBuilder<T> JsonColumn(Action<JsonColumnOptions>? configure = null)
    {
        EnsureNoRangeColumns(nameof(JsonColumn));
        var options = new JsonColumnOptions();
        configure?.Invoke(options);
        _jsonOptions = options;
        _converter = null;
        return this;
    }

    private void EnsureNoRangeColumns(string member)
    {
        if (_rangeColumns is not null)
            throw new InvalidOperationException(
                $"A property mapping cannot combine {member} with RangeColumns; a Range<T> column pair is stored without a value or JSON converter.");
    }

    /// <summary>
    /// Marks the selected property as the entity's dynamic-columns store: it receives the row's
    /// unmapped columns when the entity is read instead of mapping to a column itself. The property
    /// must have a setter and be assignable from <see cref="Dictionary{TKey,TValue}"/> with
    /// <see cref="string"/> keys and <see cref="object"/> values. Cannot be combined with a column
    /// name, a value/JSON converter or <see cref="RangeColumns(string, string, bool, bool)"/>.
    /// </summary>
    /// <returns>This builder, for chaining.</returns>
    public EntityPropertyBuilder<T> DynamicColumnsStore()
    {
        _isDynamicColumnsStore = true;
        return this;
    }

    /// <summary>
    /// Resolves the selected property's <see cref="PropertyInfo"/> and produces its mapping metadata.
    /// </summary>
    /// <returns>The property's mapping metadata.</returns>
    /// <exception cref="InvalidOperationException">The selector does not produce a <see cref="PropertyInfo"/>, the converter's model type does not match the property type, or the dynamic-columns store has an invalid type.</exception>
    public IPropertyMetadata Build()
    {
        var miVisitor = new MemberExpressionVisitor();
        miVisitor.Visit(_propertySelector);
        var pi = (PropertyInfo)miVisitor.MemberInfo! ?? throw new InvalidOperationException($"Expression {_propertySelector} does not produce PropertyInfo");
        if (_isDynamicColumnsStore)
        {
            if (_columnName is not null || _converter is not null || _jsonOptions is not null || _rangeColumns is not null)
                throw new InvalidOperationException($"The dynamic-columns store property '{pi.Name}' cannot also declare a column mapping.");
            if (!pi.CanWrite)
                throw new InvalidOperationException($"The dynamic-columns store property '{pi.Name}' must have a setter.");
            if (!DynamicColumnsTypeFacts.IsStoreType(pi.PropertyType))
                throw new InvalidOperationException($"The dynamic-columns store property '{pi.Name}' must be assignable from Dictionary<string, object?>.");

            return new PropertyMetadata { ColumnName = pi.Name, PropertyInfo = pi, IsColumnNameAuto = true, IsDynamicColumnsStore = true };
        }

        var converter = _jsonOptions is not null
            ? JsonColumnConverterFactory.Create(pi.PropertyType, _jsonOptions.Storage, _jsonOptions.Options)
            : _converter;
        ValidateConverterModel(pi, converter);
        if (_rangeColumns is not null && converter is not null)
            throw new InvalidOperationException($"Property '{pi.Name}' cannot be mapped with both RangeColumns and a value/JSON converter.");
        if (_rangeColumns is not null && !RangeTypeFacts.IsRange(pi.PropertyType))
            throw new InvalidOperationException($"Property '{pi.Name}' is mapped with RangeColumns but its type {pi.PropertyType} is not Range<T>.");
        var r = new PropertyMetadata() { ColumnName = _rangeColumns?.LowerColumn ?? _columnName!, PropertyInfo = pi, IsColumnNameAuto = false, IsKey = _isKey, IsIdentity = _isIdentity, IsComputed = _isComputed, DurationUnit = _durationUnit, DurationPrecision = _durationPrecision, Collation = _collation, Converter = converter, RangeColumns = _rangeColumns };
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
