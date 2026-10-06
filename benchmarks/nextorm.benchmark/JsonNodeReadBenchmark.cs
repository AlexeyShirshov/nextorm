using System.Collections;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;

namespace NextORM.Benchmark;

/// <summary>
/// Focused micro-benchmark for the #197 (D197) read path that materializes a bare
/// <see cref="JsonNode"/> from a native PostgreSQL <c>json</c>/<c>jsonb</c> column. The production
/// accessor (see <c>RowMapperFactory.GetReaderAccessor</c>) composes
/// <c>DbDataReader.GetFieldValue&lt;string&gt;(ordinal)</c> with
/// <c>JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)</c>; this benchmark compares that
/// composed path against the two raw DOM parses over three payload sizes (128 B / 4 KiB / 64 KiB,
/// prepared once in <c>GlobalSetup</c>, outside the measured region).
/// <para>
/// <c>JsonDocument_Parse</c> is the baseline: it parses the same text into the driver-native
/// <see cref="JsonDocument"/> DOM. <c>JsonNode_Parse</c> isolates <see cref="JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)"/>
/// alone. <c>CompiledAccessor_GetFieldValueString_JsonNodeParse</c> additionally pays the reader string
/// getter through a delegate compiled once (no per-row reflection), i.e. the exact per-row composition
/// the fix introduces; the <c>MapColumn</c> SQL-NULL <c>IsDBNull</c> guard is not included because it
/// short-circuits before the getter on NULL and is a single ordinal check on the non-null path.
/// No numeric SLA is asserted here: the measured cost is parse-dominated and scales with payload size.
/// </para>
/// </summary>
[HideColumns(Column.Job, Column.Runtime, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
[BenchmarkCategory("D197JsonRead")]
public class JsonNodeReadBenchmark
{
    private static readonly MethodInfo GetFieldValueStringMI =
        typeof(DbDataReader).GetMethod(nameof(DbDataReader.GetFieldValue))!.MakeGenericMethod(typeof(string));

    private static readonly MethodInfo JsonNodeParseMI =
        typeof(JsonNode).GetMethod(nameof(JsonNode.Parse), [typeof(string), typeof(JsonNodeOptions?), typeof(JsonDocumentOptions)])!;

    private string _text = null!;
    private SingleFieldReader _reader = null!;
    private Func<DbDataReader, JsonNode?> _accessor = null!;

    /// <summary>The approximate JSON document size in characters; the generated text is at least this long.</summary>
    [Params(128, 4 * 1024, 64 * 1024)]
    public int PayloadBytes { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _text = MakeJson(PayloadBytes);
        _reader = new SingleFieldReader(_text);
        _accessor = BuildAccessor();
    }

    /// <summary>Baseline: the driver-native DOM parse of the same text.</summary>
    /// <returns>A cheap projection of the parsed document, so the parse is not dead-code-eliminated.</returns>
    [Benchmark(Baseline = true)]
    public int JsonDocument_Parse()
    {
        using var doc = JsonDocument.Parse(_text);
        return doc.RootElement.ValueKind == JsonValueKind.Object ? 1 : 0;
    }

    /// <summary>The requested DOM: <see cref="JsonNode.Parse(string, JsonNodeOptions?, JsonDocumentOptions)"/> with default options.</summary>
    /// <returns>The parsed node, so the parse is not dead-code-eliminated.</returns>
    [Benchmark]
    public JsonNode? JsonNode_Parse()
    {
        return JsonNode.Parse(_text, null, default);
    }

    /// <summary>
    /// The composed production accessor: a delegate compiled once that calls the reader string getter
    /// and then parses the text, i.e. the same shape <c>RowMapperFactory.GetReaderAccessor</c> emits for
    /// an exact <c>JsonNode</c> projection (no per-row reflection).
    /// </summary>
    /// <returns>The parsed node.</returns>
    [Benchmark]
    public JsonNode? CompiledAccessor_GetFieldValueString_JsonNodeParse()
    {
        return _accessor(_reader);
    }

    private static Func<DbDataReader, JsonNode?> BuildAccessor()
    {
        var reader = Expression.Parameter(typeof(DbDataReader), "reader");
        var text = Expression.Call(reader, GetFieldValueStringMI, Expression.Constant(0));
        var parse = Expression.Call(
            JsonNodeParseMI,
            text,
            Expression.Constant(null, typeof(JsonNodeOptions?)),
            Expression.Constant(default(JsonDocumentOptions), typeof(JsonDocumentOptions)));
        return Expression.Lambda<Func<DbDataReader, JsonNode?>>(parse, reader).Compile();
    }

    /// <summary>Builds a valid JSON object root (with a numeric array payload) of at least <paramref name="targetBytes"/> characters.</summary>
    private static string MakeJson(int targetBytes)
    {
        var sb = new StringBuilder(targetBytes + 32);
        sb.Append("{\"id\":42,\"name\":\"bench\",\"values\":[");
        var i = 0;
        while (sb.Length < targetBytes)
        {
            if (i > 0)
                sb.Append(',');
            sb.Append(i);
            i++;
        }

        sb.Append("]}");
        return sb.ToString();
    }
}

/// <summary>
/// A minimal one-column <see cref="DbDataReader"/> that exposes its prepared text through the generic
/// typed getter, mimicking the way a real provider reader returns a native JSON column as a string.
/// The other members are unreachable on the measured path and throw.
/// </summary>
internal sealed class SingleFieldReader : DbDataReader
{
    private readonly string _text;

    public SingleFieldReader(string text) => _text = text;

    public override int FieldCount => 1;

    public override bool HasRows => true;

    public override bool IsClosed => false;

    public override int RecordsAffected => -1;

    public override int Depth => 0;

    public override object this[int ordinal] => throw new NotSupportedException();

    public override object this[string name] => throw new NotSupportedException();

    public override T GetFieldValue<T>(int ordinal)
    {
        if (typeof(T) == typeof(string))
            return (T)(object)_text;

        throw new NotSupportedException($"SingleFieldReader only exposes GetFieldValue<string>, not {typeof(T).Name}.");
    }

    public override bool IsDBNull(int ordinal) => false;

    public override string GetString(int ordinal) => _text;

    public override Type GetFieldType(int ordinal) => typeof(string);

    public override string GetDataTypeName(int ordinal) => nameof(String);

    public override string GetName(int ordinal) => "json";

    public override int GetOrdinal(string name) => throw new NotSupportedException();

    public override object GetValue(int ordinal) => throw new NotSupportedException();

    public override int GetValues(object[] values) => throw new NotSupportedException();

    public override bool Read() => throw new NotSupportedException();

    public override bool NextResult() => throw new NotSupportedException();

    public override IEnumerator GetEnumerator() => throw new NotSupportedException();

    public override bool GetBoolean(int ordinal) => throw new NotSupportedException();

    public override byte GetByte(int ordinal) => throw new NotSupportedException();

    public override short GetInt16(int ordinal) => throw new NotSupportedException();

    public override int GetInt32(int ordinal) => throw new NotSupportedException();

    public override long GetInt64(int ordinal) => throw new NotSupportedException();

    public override float GetFloat(int ordinal) => throw new NotSupportedException();

    public override double GetDouble(int ordinal) => throw new NotSupportedException();

    public override decimal GetDecimal(int ordinal) => throw new NotSupportedException();

    public override Guid GetGuid(int ordinal) => throw new NotSupportedException();

    public override DateTime GetDateTime(int ordinal) => throw new NotSupportedException();

    public override char GetChar(int ordinal) => throw new NotSupportedException();

    public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length)
        => throw new NotSupportedException();

    public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length)
        => throw new NotSupportedException();
}
