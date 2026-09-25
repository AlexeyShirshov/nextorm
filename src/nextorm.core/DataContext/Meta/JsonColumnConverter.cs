using System.Text;
using System.Text.Json;

namespace NextORM.Core;

/// <summary>
/// The storage form of a property mapped to a JSON column.
/// </summary>
public enum JsonColumnStorage
{
    /// <summary>
    /// Resolve per dialect: a native JSON type (<c>jsonb</c>) where the provider supports it
    /// (<see cref="ISqlDialect.SupportsJson"/>), otherwise a text column.
    /// </summary>
    Auto,
    /// <summary>A provider-native JSON type (<c>jsonb</c>); requires <see cref="ISqlDialect.SupportsJson"/>.</summary>
    Native,
    /// <summary>A text column holding the JSON document.</summary>
    Text,
}

/// <summary>
/// Marks a property as stored in a JSON column and transparently serialized with
/// <c>System.Text.Json</c>. The property's CLR type is the serialized model; the provider
/// representation is chosen by <see cref="Storage"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class JsonColumnAttribute : Attribute
{
    /// <summary>
    /// The storage form of the column; <see cref="JsonColumnStorage.Auto"/> by default.
    /// </summary>
    public JsonColumnStorage Storage { get; set; } = JsonColumnStorage.Auto;
}

/// <summary>
/// Configuration for a JSON column declared fluently with
/// <see cref="EntityPropertyBuilder{T}.JsonColumn(Action{JsonColumnOptions}?)"/>.
/// </summary>
public sealed class JsonColumnOptions
{
    /// <summary>
    /// The storage form of the column; <see cref="JsonColumnStorage.Auto"/> by default.
    /// </summary>
    public JsonColumnStorage Storage { get; set; } = JsonColumnStorage.Auto;

    /// <summary>
    /// The serializer options used for this column, or <see langword="null"/> for the defaults. The
    /// options are held by reference and must not be mutated after the mapping is built.
    /// </summary>
    public JsonSerializerOptions? Options { get; set; }
}

/// <summary>
/// Serializes a property of <typeparamref name="TModel"/> to a JSON column. The
/// <typeparamref name="TProvider"/> must be <see cref="string"/> (a text column) or
/// <see cref="JsonElement"/> (a provider-native JSON column). Named to avoid the
/// <c>System.Text.Json.Serialization.JsonConverter&lt;T&gt;</c> collision.
/// </summary>
/// <typeparam name="TModel">The CLR type stored in the column.</typeparam>
/// <typeparam name="TProvider">The provider representation: <see cref="string"/> or <see cref="JsonElement"/>.</typeparam>
public sealed class JsonColumnConverter<TModel, TProvider> : ValueConverter<TModel, TProvider>
{
    private readonly JsonSerializerOptions? _options;

    /// <summary>Creates a converter using the default serializer options.</summary>
    public JsonColumnConverter()
        : this(options: null)
    {
    }

    /// <summary>Creates a converter using the options carried by <paramref name="options"/>.</summary>
    /// <param name="options">The fluent JSON column options, or <see langword="null"/> for the defaults.</param>
    public JsonColumnConverter(JsonColumnOptions? options)
    {
        _options = options?.Options;
    }

    private static bool IsText => typeof(TProvider) == typeof(string);

    private static bool IsElement => typeof(TProvider) == typeof(JsonElement);

    /// <inheritdoc/>
    public override TProvider? ConvertToProvider(TModel? model)
    {
        if (model is null)
            return default;

        if (IsText)
        {
            var json = JsonSerializer.SerializeToUtf8Bytes(model, _options);
            return (TProvider)(object)Encoding.UTF8.GetString(json);
        }
        if (IsElement)
            return (TProvider)(object)JsonSerializer.SerializeToElement(model, _options);

        throw new NotSupportedException($"A JSON column provider type must be {typeof(string)} or {typeof(JsonElement)}; {typeof(TProvider)} is not supported.");
    }

    /// <inheritdoc/>
    public override TModel? ConvertFromProvider(TProvider? provider)
    {
        if (provider is null)
            return default;

        if (provider is string text)
            return JsonSerializer.Deserialize<TModel>(text, _options);
        if (provider is JsonElement element)
            return element.Deserialize<TModel>(_options);

        throw new NotSupportedException($"A JSON column provider type must be {typeof(string)} or {typeof(JsonElement)}; {typeof(TProvider)} is not supported.");
    }
}

/// <summary>
/// A dialect-aware converter produced by <c>[JsonColumn]</c>/<c>.JsonColumn()</c> whose concrete
/// storage is resolved from the active dialect (<see cref="JsonColumnStorage.Auto"/>).
/// </summary>
internal interface IJsonColumnConverter
{
    IPropertyValueConverter Resolve(ISqlDialect dialect);
}

/// <summary>
/// Resolves a JSON column converter for the active dialect and creates the converter instances used by
/// the attribute and the fluent mapping. Kept internal so the dialect-dependent resolution stays an
/// implementation detail of the read and write seams.
/// </summary>
internal static class JsonColumnConverterFactory
{
    internal static IPropertyValueConverter Create(Type modelType, JsonColumnStorage storage, JsonSerializerOptions? options = null)
    {
        var wrapperType = typeof(JsonColumnAutoConverter<>).MakeGenericType(modelType);
        var wrapperOptions = new JsonColumnOptions { Storage = storage, Options = options };
        return (IPropertyValueConverter)Activator.CreateInstance(wrapperType, wrapperOptions)!;
    }
}

/// <summary>
/// The unresolved converter stored in metadata for a JSON column. It holds a text and a native
/// converter and picks one from the dialect on each read/write, so the metadata stays dialect
/// independent.
/// </summary>
/// <typeparam name="TModel">The CLR type stored in the column.</typeparam>
internal sealed class JsonColumnAutoConverter<TModel> : IPropertyValueConverter, IJsonColumnConverter
{
    private readonly JsonColumnOptions _options;
    private readonly JsonColumnConverter<TModel, string> _text;
    private readonly JsonColumnConverter<TModel, JsonElement> _native;

    public JsonColumnAutoConverter(JsonColumnOptions options)
    {
        _options = options;
        _text = new JsonColumnConverter<TModel, string>(options);
        _native = new JsonColumnConverter<TModel, JsonElement>(options);
    }

    /// <inheritdoc/>
    public Type ProviderType => _options.Storage == JsonColumnStorage.Native ? typeof(JsonElement) : typeof(string);

    /// <inheritdoc/>
    public bool ConvertsNulls => false;

    /// <inheritdoc/>
    public object? ConvertToProvider(object? model)
        => _options.Storage == JsonColumnStorage.Native
            ? ((IPropertyValueConverter)_native).ConvertToProvider(model)
            : ((IPropertyValueConverter)_text).ConvertToProvider(model);

    /// <inheritdoc/>
    public object? ConvertFromProvider(object? provider)
        => _options.Storage == JsonColumnStorage.Native
            ? ((IPropertyValueConverter)_native).ConvertFromProvider(provider)
            : ((IPropertyValueConverter)_text).ConvertFromProvider(provider);

    /// <inheritdoc/>
    public IPropertyValueConverter Resolve(ISqlDialect dialect)
    {
        switch (_options.Storage)
        {
            case JsonColumnStorage.Text:
                return _text;
            case JsonColumnStorage.Native:
                if (!dialect.SupportsJson)
                    throw new NotSupportedException($"{dialect.GetType().Name} does not support a native JSON column; use {nameof(JsonColumnStorage.Text)}.");
                return _native;
            default:
                return dialect.SupportsJson ? _native : _text;
        }
    }
}
