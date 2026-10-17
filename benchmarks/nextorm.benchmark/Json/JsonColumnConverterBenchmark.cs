using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// Focused #198 (D198) micro-benchmark for the paths the r=2 storage/surface split changed: the
/// object-root <see cref="JsonColumnConverter{TModel,TProvider}"/> serialize/deserialize used by
/// ClickHouse's native <c>JSON</c> storage, the dialect-resolved native converter selection, and the
/// DOM adaptation the ClickHouse read seam applies to a bare <c>JsonDocument</c>/<c>JsonElement</c>.
/// <para>
/// The benchmark harness has no ClickHouse project reference or driver, so the real
/// <c>ClickHouseDataContext.CreateParam</c>/<c>MapColumnExpression</c> paths cannot be exercised here
/// without disproportionate new infrastructure; the ClickHouse parameter normalization is therefore
/// omitted and recorded as a documented deviation. What is measured is exactly the changed
/// converter/DOM code in-process (no database, no driver): the same
/// <see cref="JsonColumnConverter{TModel,TProvider}"/> instances and the same
/// <c>JsonDocument.Parse(JsonObject.ToJsonString())</c>/<c>JsonSerializer.SerializeToElement(JsonObject)</c>
/// expressions the ClickHouse read seam emits. The PostgreSQL column shows the unchanged native path.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("D198JsonRead")]
public class JsonColumnConverterBenchmark
{
    /// <summary>The object-root model stored in the JSON column; large enough for a non-trivial document.</summary>
    public sealed class BenchPoco
    {
        /// <summary>A nested string member.</summary>
        public string? Name { get; set; }

        /// <summary>A nested numeric array member.</summary>
        public List<int> Values { get; set; } = [];
    }

    private static readonly BenchPoco Model = new()
    {
        Name = "nextorm-benchmark-payload",
        Values = [1, 2, 3, 5, 8, 13, 21, 34, 55, 89],
    };

    private static readonly JsonObject ModelObject = JsonSerializer.SerializeToNode(Model) as JsonObject
        ?? throw new InvalidOperationException("The benchmark model must serialize to a JSON object.");

    private static readonly string ModelText = JsonSerializer.Serialize(Model);
    private static readonly JsonElement ModelElement = JsonSerializer.SerializeToElement(Model);
    private static readonly NativeJsonDialect Dialect = new();

    private IPropertyValueConverter _text = null!;
    private IPropertyValueConverter _nativeElement = null!;
    private IPropertyValueConverter _nativeObject = null!;
    private object _autoWrapper = null!;
    private Func<object, ISqlDialect, IPropertyValueConverter> _resolve = null!;

    /// <summary>
    /// Resolves the <c>Auto</c> JSON converter the way the mapping does on ClickHouse
    /// (<see cref="ISqlDialect.SupportsJson"/> is <c>true</c> and
    /// <see cref="ISqlDialect.NativeJsonProviderType"/> is <see cref="JsonObject"/>). The factory and the
    /// resolver are internal, so they are reached once by reflection/compiled expression outside the
    /// measured region.
    /// </summary>
    [GlobalSetup]
    public void Setup()
    {
        var factory = typeof(IPropertyValueConverter).Assembly.GetType(
            "NextORM.Core.JsonColumnConverterFactory", throwOnError: true)!;
        var create = factory.GetMethod(
            "Create", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public)!;
        _autoWrapper = create.Invoke(null, new object?[] { typeof(BenchPoco), JsonColumnStorage.Auto, null })!;

        var resolveMethod = _autoWrapper.GetType().GetMethod(
            "Resolve", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)!;
        var wrapperParam = Expression.Parameter(typeof(object));
        var dialectParam = Expression.Parameter(typeof(ISqlDialect));
        var call = Expression.Call(
            Expression.Convert(wrapperParam, _autoWrapper.GetType()), resolveMethod, dialectParam);
        _resolve = Expression.Lambda<Func<object, ISqlDialect, IPropertyValueConverter>>(
            call, wrapperParam, dialectParam).Compile();

        _text = new JsonColumnConverter<BenchPoco, string>();
        _nativeElement = new JsonColumnConverter<BenchPoco, JsonElement>();
        _nativeObject = _resolve(_autoWrapper, Dialect);
    }

    /// <summary>Text storage baseline: model to a JSON string.</summary>
    /// <returns>The serialized provider value.</returns>
    [Benchmark(Baseline = true)]
    public object? Text_ConvertToProvider() => _text.ConvertToProvider(Model);

    /// <summary>Text storage: JSON string back to the model.</summary>
    /// <returns>The deserialized model.</returns>
    [Benchmark]
    public object? Text_ConvertFromProvider() => _text.ConvertFromProvider(ModelText);

    /// <summary>PostgreSQL native (unchanged) path: model to <see cref="JsonElement"/>.</summary>
    /// <returns>The serialized provider value.</returns>
    [Benchmark]
    public object? PostgresNativeElement_ConvertToProvider() => _nativeElement.ConvertToProvider(Model);

    /// <summary>PostgreSQL native (unchanged) path: <see cref="JsonElement"/> back to the model.</summary>
    /// <returns>The deserialized model.</returns>
    [Benchmark]
    public object? PostgresNativeElement_ConvertFromProvider() => _nativeElement.ConvertFromProvider(ModelElement);

    /// <summary>ClickHouse native path: model to object-root <see cref="JsonObject"/>.</summary>
    /// <returns>The serialized provider value.</returns>
    [Benchmark]
    public object? ClickHouseNativeObject_ConvertToProvider() => _nativeObject.ConvertToProvider(Model);

    /// <summary>ClickHouse native path: object-root <see cref="JsonObject"/> back to the model.</summary>
    /// <returns>The deserialized model.</returns>
    [Benchmark]
    public object? ClickHouseNativeObject_ConvertFromProvider() => _nativeObject.ConvertFromProvider(ModelObject);

    /// <summary>Native resolution: the <c>Auto</c> converter resolved against the ClickHouse-shaped dialect.</summary>
    /// <returns>The resolved native converter.</returns>
    [Benchmark]
    public IPropertyValueConverter Resolve_Auto_ClickHouse() => _resolve(_autoWrapper, Dialect);

    /// <summary>ClickHouse read adaptation of a bare <see cref="JsonDocument"/> from the driver's object-root transport.</summary>
    /// <returns>The adapted document.</returns>
    [Benchmark]
    public JsonDocument Adapt_ToJsonDocument() => JsonDocument.Parse(ModelObject.ToJsonString());

    /// <summary>ClickHouse read adaptation of a bare <see cref="JsonElement"/> from the driver's object-root transport.</summary>
    /// <returns>The adapted element.</returns>
    [Benchmark]
    public JsonElement Adapt_ToJsonElement() => JsonSerializer.SerializeToElement(ModelObject);

    /// <summary>
    /// A minimal dialect advertising the ClickHouse storage shape only
    /// (<see cref="ISqlDialect.SupportsJson"/> native, provider representation <see cref="JsonObject"/>);
    /// it never renders SQL, so the SQL members are unreachable defaults.
    /// </summary>
    private sealed class NativeJsonDialect : SqlDialectBase
    {
        /// <inheritdoc/>
        public override bool SupportsJson => true;

        /// <inheritdoc/>
        public override Type NativeJsonProviderType => typeof(JsonObject);

        /// <inheritdoc/>
        public override string MakeParam(string name) => "@" + name;

        /// <inheritdoc/>
        public override void MakePage(Paging paging, StringBuilder sqlBuilder, KeywordCase keywordCase = KeywordCase.Lower)
        {
        }
    }
}
