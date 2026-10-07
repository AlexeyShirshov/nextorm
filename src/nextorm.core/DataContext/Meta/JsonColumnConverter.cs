using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace NextORM.Core;

/// <summary>
/// The storage form of a property mapped to a JSON column.
/// </summary>
public enum JsonColumnStorage
{
    /// <summary>
    /// Resolve per dialect: a native JSON type where the provider supports it
    /// (<see cref="ISqlDialect.SupportsJson"/>), otherwise a text column. The native representation is
    /// <see cref="ISqlDialect.NativeJsonProviderType"/> (<c>JsonElement</c> on PostgreSQL,
    /// <c>JsonObject</c> on ClickHouse).
    /// </summary>
    Auto,
    /// <summary>A provider-native JSON type; requires <see cref="ISqlDialect.SupportsJson"/>.</summary>
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
/// <typeparamref name="TProvider"/> must be <see cref="string"/> (a text column),
/// <see cref="JsonElement"/> (a provider-native JSON column exposed as an element) or
/// <see cref="JsonObject"/> (ClickHouse's native <c>JSON</c> object-root transport). Named to avoid the
/// <c>System.Text.Json.Serialization.JsonConverter&lt;T&gt;</c> collision.
/// </summary>
/// <typeparam name="TModel">The CLR type stored in the column.</typeparam>
/// <typeparam name="TProvider">The provider representation: <see cref="string"/>, <see cref="JsonElement"/> or <see cref="JsonObject"/>.</typeparam>
public sealed class JsonColumnConverter<TModel, TProvider> : ValueConverter<TModel, TProvider>
{
    private const string ObjectRootOnlyMessage =
        "A native JSON column accepts object-root values only; the value does not serialize to a JSON object. "
        + "Use JsonColumnStorage.Text for a non-object root.";

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

    private static bool IsObject => typeof(TProvider) == typeof(JsonObject);

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
        if (IsObject)
            return (TProvider)(object)SerializeToObject(model);

        throw new NotSupportedException($"A JSON column provider type must be {typeof(string)}, {typeof(JsonElement)} or {typeof(JsonObject)}; {typeof(TProvider)} is not supported.");
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
        if (provider is JsonObject jsonObject)
            return jsonObject.Deserialize<TModel>(_options);

        throw new NotSupportedException($"A JSON column provider type must be {typeof(string)}, {typeof(JsonElement)} or {typeof(JsonObject)}; {typeof(TProvider)} is not supported.");
    }

    // ClickHouse's native JSON is object-rooted: a JSON array/primitive/string/null root (and an
    // undefined JsonElement) cannot be stored, so the mismatch is surfaced here instead of producing a
    // document the server rejects.
    private JsonObject SerializeToObject(TModel model)
    {
        if (model is JsonElement element && element.ValueKind != JsonValueKind.Object)
            throw new NotSupportedException(ObjectRootOnlyMessage);

        return JsonSerializer.SerializeToNode(model, _options) as JsonObject
            ?? throw new NotSupportedException(ObjectRootOnlyMessage);
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
    private JsonColumnConverter<TModel, JsonObject>? _nativeObject;

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
                return ResolveNative(dialect);
            default:
                return dialect.SupportsJson ? ResolveNative(dialect) : _text;
        }
    }

    // The dialect advertises which CLR type its native JSON representation materializes as. PostgreSQL
    // uses JsonElement (Npgsql) and ClickHouse uses JsonObject (the driver's object-root DOM), so the
    // resolved native converter is selected from NativeJsonProviderType rather than fixed at construction.
    private IPropertyValueConverter ResolveNative(ISqlDialect dialect)
    {
        var providerType = dialect.NativeJsonProviderType;
        if (providerType == typeof(JsonObject))
            return _nativeObject ??= new JsonColumnConverter<TModel, JsonObject>(_options);
        if (providerType == typeof(JsonElement))
            return _native;

        throw new NotSupportedException(
            $"{dialect.GetType().Name} advertises native JSON storage with provider type {providerType}, "
            + $"but the supported native representations are {typeof(JsonElement)} and {typeof(JsonObject)}.");
    }
}
