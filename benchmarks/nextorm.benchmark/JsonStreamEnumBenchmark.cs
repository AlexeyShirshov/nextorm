using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;

namespace NextORM.Benchmark;

/// <summary>
/// D178 (#178) focused benchmark for enum JSON streaming. It contrasts the integral baseline, the
/// default numeric enum path and the stock string-enum converter path over the real compiled
/// <see cref="JsonRowWriter"/>, driven by a hand-written <see cref="IDataRecord"/> so the category is
/// container-free. The writer is compiled once (outside the measured region) and the per-row work is
/// the same for every case, so the <see cref="MemoryDiagnoser"/> numbers expose any per-row converter
/// resolution or boxing the enum path might introduce.
/// </summary>
[BenchmarkCategory("json-stream-enum")]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class JsonStreamEnumBenchmark
{
    public enum Status
    {
        Unknown = 0,
        Active = 7,
        Disabled = -3,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum StringStatus
    {
        Unknown = 0,
        Active = 7,
        Disabled = -3,
    }

    private sealed class IntRecord : IDataRecord
    {
        private readonly int _value;

        public IntRecord(int value) => _value = value;

        public int FieldCount => 1;
        public object this[int i] => GetValue(i);
        public object this[string name] => throw new NotSupportedException();
        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => throw new NotSupportedException();
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => "integer";
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public Type GetFieldType(int i) => typeof(int);
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => throw new NotSupportedException();
        public int GetInt32(int i) => _value;
        public long GetInt64(int i) => throw new NotSupportedException();
        public string GetName(int i) => "Value";
        public int GetOrdinal(string name) => 0;
        public string GetString(int i) => throw new NotSupportedException();
        public object GetValue(int i) => _value;
        public int GetValues(object[] values)
        {
            values[0] = _value;
            return 1;
        }

        public bool IsDBNull(int i) => false;
    }

    private readonly MemoryStream _sink = new();
    private IntRecord _record = null!;
    private JsonRowWriter _integral = null!;
    private JsonRowWriter _enumNumeric = null!;
    private JsonRowWriter _enumString = null!;

    /// <summary>The number of rows per measured invocation; the three cases pay the same row-loop cost.</summary>
    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _record = new IntRecord((int)Status.Active);
        _integral = BuildWriter(new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Value" });
        _enumNumeric = BuildWriter(new SelectExpression(typeof(Status)) { Index = 0, PropertyName = "Value" });
        _enumString = BuildWriter(new SelectExpression(typeof(StringStatus)) { Index = 0, PropertyName = "Value" });

        // Warm every path and allocate the sink backing buffer outside the measured region.
        Write(_integral);
        Write(_enumNumeric);
        Write(_enumString);
    }

    private static JsonRowWriter BuildWriter(SelectExpression column)
    {
        var plan = JsonShapePlan.Build([column], oneColumn: true, new JsonStreamOptions());
        return JsonRowWriterFactory.Build(plan);
    }

    private void Write(JsonRowWriter rowWriter)
    {
        _sink.SetLength(0);
        using var writer = new Utf8JsonWriter(_sink);
        writer.WriteStartArray();
        for (var i = 0; i < RowCount; i++)
            rowWriter(_record, writer);
        writer.WriteEndArray();
        writer.Flush();
    }

    [Benchmark(Baseline = true)]
    public void IntegralInt() => Write(_integral);

    [Benchmark]
    public void EnumNumeric() => Write(_enumNumeric);

    [Benchmark]
    public void EnumString() => Write(_enumString);
}
