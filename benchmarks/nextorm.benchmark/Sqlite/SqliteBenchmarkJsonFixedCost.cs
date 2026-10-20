using System.Data;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// Mirror of <see cref="SqliteBenchmarkCsvFixedCost"/> for the JSON terminal: how much of one
/// <c>WriteJson</c> call is paid before the first row. <c>PrepareJsonStream</c> clones the command,
/// prepares it with <c>storeInCache: false</c>, builds a fresh <c>JsonShapePlan</c> and compiles a new row writer on every invocation, so a repeated small export pays that work each time.
/// The <see cref="Rows"/> sweep makes the intercept of <c>T(N) = fixed + N * perRow</c> readable; the
/// 1-/3-/8-column writes give the per-column share of that intercept.
/// </summary>
/// <remarks>
/// The warmed <c>ToList</c> arm is the reference for a command whose plan and materializer come from the
/// cache. Tagged <c>json-fixed</c> only, so the frozen perf category is not touched.
/// </remarks>
[Config(typeof(NextormConfig))]
[BenchmarkCategory("json-fixed")]
[MemoryDiagnoser]
[HideColumns(Column.Job, Column.RatioSD, Column.Error, Column.StdDev)]
public class SqliteBenchmarkJsonFixedCost
{
    // Limit(0) means "no limit", hence the sweep starts at one row; the 10 000 arm is the row-heavy
    // reference.
    [Params(1, 10, 100, 1000, 10000)]
    public int Rows { get; set; }

    private readonly IDataContext _db;
    private readonly TestDataRepository _ctx;
    private readonly MemoryStream _destination = new(1 << 18);
    private long _sink;

    // D247-01 narrowed compile-only spike: the shape is built in setup, the warm reusable entry is
    // compiled once before any measurement, and the steady-writer arm drives that precompiled entry
    // over a hand-written three-column record. Neither arm re-plans, prepares or rebuilds a shape.
    private JsonShapePlan _shapePlan = null!;
    private JsonRowWriter _warmWriter = null!;
    private JsonRowWriter _compileSink = null!;
    private FlatJsonRecord _flatRecord = null!;

    public SqliteBenchmarkJsonFixedCost()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        // Warm the plan cache and compiled materializer of the reference arm: unlike WriteJson it does
        // not re-plan and does not compile a writer per call.
        _ctx.LargeEntity.Limit(10_000).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
    }

    private void ResetDestination()
    {
        _destination.SetLength(0);
        _destination.Position = 0;
    }

    /// <summary>
    /// D247-01 setup: builds the three-column JSON shape plan once and compiles the warm reusable entry
    /// once, so the compile-only arm measures <see cref="JsonRowWriterFactory.Build"/> itself (not the
    /// first JIT of the compiled lambda) and the steady-writer arm measures only the row loop.
    /// </summary>
    [GlobalSetup]
    public void SetupInstrumentation()
    {
        _shapePlan = JsonShapePlan.Build(
            [
                new SelectExpression(typeof(int)) { Index = 0, PropertyName = "Id" },
                new SelectExpression(typeof(string)) { Index = 1, PropertyName = "Str" },
                new SelectExpression(typeof(DateTime)) { Index = 2, PropertyName = "Dt" },
            ],
            oneColumn: false,
            new JsonStreamOptions());

        // Compile the warm reusable entry once, before any measurement.
        _warmWriter = JsonRowWriterFactory.Build(_shapePlan);
        _compileSink = _warmWriter;
        _flatRecord = new FlatJsonRecord();

        // Smoke the steady writer once: a rejected shape must fail setup, not the measured region.
        using (var probe = new Utf8JsonWriter(new MemoryStream()))
        {
            _warmWriter(_flatRecord, probe);
        }
    }

    /// <summary>Reference: a warmed buffered read of the three-column projection (no JSON, no re-plan).</summary>
    [Benchmark(Baseline = true)]
    public void Nextorm_ToList_3Col()
    {
        var rows = _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id, it.Str, it.Dt }).ToList();
        _sink = rows.Count;
    }

    /// <summary>The realistic shape: one <c>WriteJson</c> call over a three-column projection.</summary>
    [Benchmark]
    public void Nextorm_WriteJson_3Col()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id, it.Str, it.Dt }).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>Single-column scalar write: a different JSON shape (a bare value, not an object).</summary>
    [Benchmark]
    public void Nextorm_WriteJson_1Col()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => it.Str).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>One-member object with a string: object framing plus one string column.</summary>
    [Benchmark]
    public void Nextorm_WriteJson_ObjectStr()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Str }).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>One-member object with a numeric: object framing plus one numeric column (provider-aware read).</summary>
    [Benchmark]
    public void Nextorm_WriteJson_ObjectNum()
    {
        ResetDestination();
        _ctx.LargeEntity.Limit(Rows).Select(it => new { it.Id }).WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>Eight-column write: the high end of the per-column cost slope.</summary>
    [Benchmark]
    public void Nextorm_WriteJson_8Col()
    {
        ResetDestination();
        _ctx.LargeEntity
            .Limit(Rows)
            .Select(it => new
            {
                it.Id,
                S1 = it.Str,
                D1 = it.Dt,
                S2 = it.Str,
                D2 = it.Dt,
                S3 = it.Str,
                D3 = it.Dt,
                D4 = it.Dt,
            })
            .WriteJson(_destination);
        _sink = _destination.Length;
    }

    /// <summary>
    /// D247-01 compile-only spike: measures <see cref="JsonRowWriterFactory.Build"/> alone over the
    /// setup-built shape plan. No preparation, no shape construction and no row loop run here, so the
    /// public total can be decomposed into measured compile cost and an un-attributed residual.
    /// </summary>
    /// <returns>A stable value keeping the compiled delegate live (dead-code elimination guard).</returns>
    [Benchmark]
    public int Nextorm_CompileOnly_3Col()
    {
        _compileSink = JsonRowWriterFactory.Build(_shapePlan);
        return _compileSink.GetHashCode();
    }

    /// <summary>
    /// D247-01 steady writer: drives the precompiled warm reusable entry over a hand-written
    /// three-column record for <see cref="Rows"/> rows. No preparation, no shape construction and no
    /// compilation run here, so this is the writer-only share of the public total.
    /// </summary>
    /// <returns>The number of JSON bytes written.</returns>
    [Benchmark]
    public long Nextorm_SteadyWriter_3Col()
    {
        ResetDestination();
        using (var writer = new Utf8JsonWriter(_destination, new JsonWriterOptions { SkipValidation = true }))
        {
            writer.WriteStartArray();
            for (var i = 0; i < Rows; i++)
                _warmWriter(_flatRecord, writer);
            writer.WriteEndArray();
        }

        _sink = _destination.Length;
        return _destination.Length;
    }

    /// <summary>A hand-written three-column record (int, string, DateTime) for the steady-writer arm.</summary>
    private sealed class FlatJsonRecord : IDataRecord
    {
        private static readonly Type[] Types = [typeof(int), typeof(string), typeof(DateTime)];
        private static readonly string[] Names = ["Id", "Str", "Dt"];
        private readonly object[] _values = [42, "row", new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc)];

        public int FieldCount => 3;

        public object this[int i] => _values[i];

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => throw new NotSupportedException();

        public byte GetByte(int i) => throw new NotSupportedException();

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => Types[i].Name;

        public DateTime GetDateTime(int i) => (DateTime)_values[i];

        public decimal GetDecimal(int i) => throw new NotSupportedException();

        public double GetDouble(int i) => throw new NotSupportedException();

        public Type GetFieldType(int i) => Types[i];

        public float GetFloat(int i) => throw new NotSupportedException();

        public Guid GetGuid(int i) => throw new NotSupportedException();

        public short GetInt16(int i) => throw new NotSupportedException();

        public int GetInt32(int i) => (int)_values[i];

        public long GetInt64(int i) => throw new NotSupportedException();

        public string GetName(int i) => Names[i];

        public int GetOrdinal(string name) => throw new NotSupportedException();

        public string GetString(int i) => (string)_values[i];

        public object GetValue(int i) => _values[i];

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _values.Length);
            Array.Copy(_values, values, count);
            return count;
        }

        public bool IsDBNull(int i) => false;
    }
}
