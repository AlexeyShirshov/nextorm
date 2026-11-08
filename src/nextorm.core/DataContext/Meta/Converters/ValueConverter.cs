using System.Linq.Expressions;
using System.Reflection;

namespace NextORM.Core;

/// <summary>
/// Converts a mapped property's value between its CLR model type and the representation stored by the
/// provider. A converter is attached to a property with <see cref="ValueConverterAttribute"/> or with
/// the fluent <see cref="EntityPropertyBuilder{T}.HasConversion(IPropertyValueConverter)"/> mapping, and
/// is applied on both the read and the write path. The name avoids the <c>System.Windows.Data</c>
/// <c>IValueConverter</c> collision.
/// </summary>
public interface IPropertyValueConverter
{
    /// <summary>
    /// The provider-side type produced by <see cref="ConvertToProvider"/> and consumed by
    /// <see cref="ConvertFromProvider"/>. The reader uses it to select the typed accessor and the write
    /// path uses it to type the bound parameter.
    /// </summary>
    Type ProviderType { get; }

    /// <summary>
    /// Converts a model value to the representation written to the database.
    /// </summary>
    /// <param name="model">The model value, or <see langword="null"/>.</param>
    /// <returns>The provider value.</returns>
    object? ConvertToProvider(object? model);

    /// <summary>
    /// Converts a provider value read from the database back to the model representation.
    /// </summary>
    /// <param name="provider">The provider value, or <see langword="null"/>.</param>
    /// <returns>The model value.</returns>
    object? ConvertFromProvider(object? provider);
    /// <summary>
    /// Whether a SQL <c>NULL</c> is passed through the converter instead of being mapped to a
    /// <see langword="null"/> model value. The policy lives on the converter instance so there is a
    /// single source of truth.
    /// </summary>
    bool ConvertsNulls { get; }
}

/// <summary>
/// Base class for a strongly typed property value converter. Subclasses override the typed
/// <see cref="ConvertToProvider"/> and <see cref="ConvertFromProvider"/> methods; the
/// <see cref="IPropertyValueConverter"/> members are implemented here through a cached typed invoker so
/// value types are not boxed on every row.
/// </summary>
/// <typeparam name="TModel">The CLR model type of the property.</typeparam>
/// <typeparam name="TProvider">The provider-side type stored in the column.</typeparam>
public abstract class ValueConverter<TModel, TProvider> : IPropertyValueConverter
{
    private readonly Func<TModel, TProvider?> _toProvider;
    private readonly Func<TProvider?, TModel?> _fromProvider;

    /// <summary>Initializes the converter and caches its typed invokers.</summary>
    protected ValueConverter()
    {
        _toProvider = ConvertToProvider;
        _fromProvider = ConvertFromProvider;
    }

    /// <summary>Converts a model value to the provider representation.</summary>
    /// <param name="model">The model value, or <see langword="null"/>.</param>
    /// <returns>The provider value.</returns>
    public abstract TProvider? ConvertToProvider(TModel? model);

    /// <summary>Converts a provider value back to the model representation.</summary>
    /// <param name="provider">The provider value, or <see langword="null"/>.</param>
    /// <returns>The model value.</returns>
    public abstract TModel? ConvertFromProvider(TProvider? provider);

    /// <summary>
    /// Whether a SQL <c>NULL</c> is passed through the converter instead of being mapped to a
    /// <see langword="null"/> model value. Defaults to <see langword="false"/>.
    /// </summary>
    public virtual bool ConvertsNulls => false;

    /// <inheritdoc/>
    public Type ProviderType => typeof(TProvider);

    object? IPropertyValueConverter.ConvertToProvider(object? model)
        => model is null
            ? ConvertsNulls ? _toProvider(default!) : default(TProvider)
            : _toProvider((TModel)model);

    object? IPropertyValueConverter.ConvertFromProvider(object? provider)
        => provider is null
            ? ConvertsNulls ? _fromProvider(default!) : default(TModel)
            : _fromProvider((TProvider)provider);
}

/// <summary>
/// Declares the <see cref="IPropertyValueConverter"/> applied to a mapped property. The converter type
/// must be a non-abstract class implementing <see cref="IPropertyValueConverter"/> with a public
/// parameterless constructor.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class ValueConverterAttribute : Attribute
{
    /// <summary>Creates the attribute for the given converter type.</summary>
    /// <param name="converterType">The converter type; a non-abstract <see cref="IPropertyValueConverter"/> implementation.</param>
    public ValueConverterAttribute(Type converterType)
    {
        ConverterType = converterType ?? throw new ArgumentNullException(nameof(converterType));
    }

    /// <summary>The converter type applied to the property.</summary>
    public Type ConverterType { get; }

    internal IPropertyValueConverter Create(Type propertyType)
    {
        if (ConverterType.IsGenericTypeDefinition)
            throw new InvalidOperationException($"Value converter {ConverterType} must be a closed type.");
        if (!typeof(IPropertyValueConverter).IsAssignableFrom(ConverterType))
            throw new InvalidOperationException($"Value converter {ConverterType} must implement {nameof(IPropertyValueConverter)}.");

        var instance = Activator.CreateInstance(ConverterType) as IPropertyValueConverter
            ?? throw new InvalidOperationException($"Value converter {ConverterType} must have a public parameterless constructor.");

        var modelType = ValueConverterReflection.GetModelType(ConverterType);
        var expectedModelType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;
        if (modelType is not null && modelType != propertyType && modelType != expectedModelType)
            throw new InvalidOperationException($"Value converter {ConverterType} maps {modelType} but property '{propertyType.Name}' has type {propertyType}.");

        return instance;
    }
}

/// <summary>
/// Reflection helpers shared by the converter attribute and the row mapper: locates the closed
/// <see cref="ValueConverter{TModel,TProvider}"/> base of a converter and its typed conversion methods.
/// </summary>
internal static class ValueConverterReflection
{
    internal static Type? GetModelType(Type converterType)
        => GetClosedBase(converterType)?.GetGenericArguments()[0];

    internal static MethodInfo? GetFromProviderMethod(Type converterType)
        => GetClosedBase(converterType)?.GetMethod(nameof(ValueConverter<object, object>.ConvertFromProvider), BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);

    private static Type? GetClosedBase(Type converterType)
    {
        for (var type = converterType; type is not null; type = type.BaseType!)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ValueConverter<,>))
                return type;
        }

        return null;
    }
}

/// <summary>
/// Adapts a pair of conversion expressions to <see cref="ValueConverter{TModel,TProvider}"/>, backing
/// the fluent expression form of <c>HasConversion</c>.
/// </summary>
/// <typeparam name="TModel">The CLR model type of the property.</typeparam>
/// <typeparam name="TProvider">The provider-side type stored in the column.</typeparam>
internal sealed class ExpressionValueConverter<TModel, TProvider> : ValueConverter<TModel, TProvider>
{
    private readonly Func<TModel, TProvider> _toProvider;
    private readonly Func<TProvider, TModel> _fromProvider;

    internal ExpressionValueConverter(Expression<Func<TModel, TProvider>> toProvider, Expression<Func<TProvider, TModel>> fromProvider)
    {
        _toProvider = toProvider.Compile();
        _fromProvider = fromProvider.Compile();
    }

    public override TProvider? ConvertToProvider(TModel? model) => model is null ? default : _toProvider(model);

    public override TModel? ConvertFromProvider(TProvider? provider) => provider is null ? default : _fromProvider(provider);
}
