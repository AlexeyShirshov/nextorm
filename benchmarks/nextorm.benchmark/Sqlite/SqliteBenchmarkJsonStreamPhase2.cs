using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text.Json;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Benchmark;

/// <summary>
/// C08 focused benchmark for #176 JSON streaming phase 2 (category
/// <c>json-stream-phase2</c>). It contrasts the direct streaming terminal
/// (<c>WriteJson</c>, which writes each row straight to the caller-owned sink) with the
/// <em>materialize + STJ</em> oracle on the same data (<c>ToList()</c> and then
/// <c>JsonSerializer.Serialize</c>), so the row-loop and allocation difference of the new
/// nested/array/joined-slot paths is visible. Row count is a parameter: the per-call preparation
/// cost is flat, while the row-loop cost scales with <see cref="RowCount"/>.
/// </summary>
/// <remarks>
/// The workloads run on SQLite only — the category is deliberately container-free. Native rank-one
/// arrays have no SQLite source, so their recursion is exercised by <see cref="JsonStreamPhase2WriterBenchmark"/>
/// below, which drives the real recursive writer over a hand-written <see cref="IDataRecord"/> (the
/// same mechanism the core direct-writer tests use). The provider-native end-to-end array scenarios
/// live in the integration suite (E176.04).
/// </remarks>
[BenchmarkCategory("json-stream-phase2")]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class SqliteBenchmarkJsonStreamPhase2
{
    private const int InsertBatchSize = 500;
    private const int BlobSize = 64;

    private readonly MemoryStream _sink = new();

    private IDataContext _db = null!;
    private EntityBuilder<Phase2JsonItem> _items = null!;
    private EntityBuilder<Phase2JsonParent> _parents = null!;
    private EntityBuilder<Phase2JsonChild> _children = null!;
    private string _path = null!;

    /// <summary>The number of seeded rows; the pair shows whether a workload's cost is flat (preparation) or linear (row loop).</summary>
    [Params(1_000, 10_000)]
    public int RowCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        var dir = Path.Combine(Path.GetTempPath(), "nextorm-bench");
        Directory.CreateDirectory(dir);
        _path = Path.Combine(dir, $"json-stream-phase2-{RowCount}-{Guid.NewGuid():N}.db");

        using (var conn = new SqliteConnection($"Data Source={_path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table phase2_json_item (id integer primary key, name text, value integer, blob blob);" +
                "create table phase2_json_parent (id integer primary key, name text);" +
                "create table phase2_json_child (id integer primary key, parent_id integer, name text);";
            cmd.ExecuteNonQuery();
        }

        var builder = new DataContextBuilder();
        builder.UseSqlite(_path);
        _db = builder.CreateDataContext();
        ((IConnectionManager)_db).EnsureConnectionOpen();

        Seed();

        _items = _db.From<Phase2JsonItem>();
        _parents = _db.From<Phase2JsonParent>();
        _children = _db.From<Phase2JsonChild>();

        // Warm every shape once so a shape rejection fails setup instead of the measured region, and so
        // the sink's backing buffer is allocated outside the measurement.
        _ = Flat_Stream();
        _ = Nested_Stream();
        _ = ConditionalNullArm_Stream();
        _ = JoinedSlots_Stream();
        _ = ByteArrayBase64_Stream();
        _sink.SetLength(0);
    }

    private void Seed()
    {
        var items = new Phase2JsonItem[RowCount];
        for (var i = 1; i <= RowCount; i++)
        {
            // Even rows carry a null name so the conditional workload produces an all-null object on the
            // construction arm (explicit presence), distinct from the JSON null of the null arm.
            items[i - 1] = new Phase2JsonItem
            {
                Id = i,
                Name = i % 2 == 0 ? null : "item-" + i,
                Value = i % 2 == 0 ? null : i,
                Blob = MakeBlob(i),
            };
        }

        Insert(items);

        var parentCount = Math.Max(1, RowCount / 2);
        var parents = new Phase2JsonParent[parentCount];
        for (var i = 1; i <= parentCount; i++)
            parents[i - 1] = new Phase2JsonParent { Id = i, Name = "parent-" + i };
        Insert(parents);

        var children = new Phase2JsonChild[parentCount];
        for (var i = 1; i <= parentCount; i++)
        {
            // The last parent has no child, so the outer join exposes the absent Item2 slot.
            var parentId = i == parentCount ? i - 1 : i;
            children[i - 1] = new Phase2JsonChild { Id = 100_000 + i, ParentId = parentId, Name = "child-" + i };
        }
        Insert(children);

        void Insert<T>(T[] rows)
        {
            for (var offset = 0; offset < rows.Length; offset += InsertBatchSize)
            {
                var take = Math.Min(InsertBatchSize, rows.Length - offset);
                var batch = new T[take];
                Array.Copy(rows, offset, batch, 0, take);
                _db.CreateInsertBuilder<T>().Values(batch).Insert();
            }
        }
    }

    private static byte[] MakeBlob(int i)
    {
        var blob = new byte[BlobSize];
        for (var j = 0; j < blob.Length; j++)
            blob[j] = (byte)((i + j) & 0xFF);
        return blob;
    }

    // ---- Flat phase 1 ----------------------------------------------------------------------------

    /// <summary>Streams the flat projection; never materialises a row list.</summary>
    [Benchmark]
    public long Flat_Stream()
    {
        _sink.SetLength(0);
        _items.Select(x => new { x.Id, x.Name }).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Materializes the same rows and serializes them with STJ (the oracle).</summary>
    [Benchmark(Baseline = true)]
    public long Flat_MaterializeStj()
        => JsonSerializer.SerializeToUtf8Bytes(_items.Select(x => new { x.Id, x.Name }).ToList()).LongLength;

    // ---- Nested object ---------------------------------------------------------------------------

    /// <summary>Streams a nested construction; the ordinary materializer cannot build it (#172).</summary>
    [Benchmark]
    public long Nested_Stream()
    {
        _sink.SetLength(0);
        _items.Select(x => new { x.Id, Child = new { x.Name, x.Value } }).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Materializes the flat rows, reshapes them into the same nested object and serializes with STJ.</summary>
    [Benchmark]
    public long Nested_MaterializeStj()
    {
        var rows = _items.Select(x => new { x.Id, x.Name, x.Value }).ToList();
        var nested = new object[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            nested[i] = new { rows[i].Id, Child = new { rows[i].Name, rows[i].Value } };
        return JsonSerializer.SerializeToUtf8Bytes(nested).LongLength;
    }

    // ---- Conditional / all-null object -----------------------------------------------------------

    /// <summary>Streams a conditional nested construction (null arm and all-null object arm).</summary>
    [Benchmark]
    public long ConditionalNullArm_Stream()
    {
        _sink.SetLength(0);
        _items.Select(x => new { x.Id, Child = x.Id % 2 == 0 ? new { x.Name } : null }).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Materializes and serializes the same conditional shape with STJ.</summary>
    [Benchmark]
    public long ConditionalNullArm_MaterializeStj()
    {
        var rows = _items.Select(x => new { x.Id, x.Name }).ToList();
        var shaped = new object[rows.Count];
        for (var i = 0; i < rows.Count; i++)
            shaped[i] = new { rows[i].Id, Child = rows[i].Id % 2 == 0 ? new { rows[i].Name } : null };
        return JsonSerializer.SerializeToUtf8Bytes(shaped).LongLength;
    }

    // ---- Joined slots (Projection<T1,T2>) --------------------------------------------------------

    /// <summary>Streams a bare left-join projection: top-level Item1/Item2 with an absent Item2 on the unmatched row.</summary>
    [Benchmark]
    public long JoinedSlots_Stream()
    {
        _sink.SetLength(0);
        _parents.LeftJoin(_children, (p, c) => p.Id == c.ParentId).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Materializes the same join and serializes the Projection&lt;T1,T2&gt; with STJ.</summary>
    [Benchmark]
    public long JoinedSlots_MaterializeStj()
        => JsonSerializer.SerializeToUtf8Bytes(_parents.LeftJoin(_children, (p, c) => p.Id == c.ParentId).ToList()).LongLength;

    // ---- byte[] Base64 (array dispatch precedent) ------------------------------------------------

    /// <summary>Streams a scalar byte[] column, which must stay Base64.</summary>
    [Benchmark]
    public long ByteArrayBase64_Stream()
    {
        _sink.SetLength(0);
        _items.Select(x => x.Blob).WriteJson(_sink);
        return _sink.Length;
    }

    /// <summary>Materializes the same byte[] column and serializes it with STJ (Base64).</summary>
    [Benchmark]
    public long ByteArrayBase64_MaterializeStj()
        => JsonSerializer.SerializeToUtf8Bytes(_items.Select(x => x.Blob).ToList()).LongLength;
}

/// <summary>
/// C08 writer-level native-array workload. The provider-native rank-one array recursion (including
/// jagged arrays) is the phase-2 writer path; SQLite has no native array source and the acceptance
/// host runs no containers, so this benchmark drives the real recursive writer over a hand-written
/// <see cref="IDataRecord"/> and compares it with the materialize + STJ oracle. Live memory stays
/// O(buffer): each row is written straight to the sink and no result list is built.
/// </summary>
[BenchmarkCategory("json-stream-phase2")]
[HideColumns(Column.Job, Column.Runtime, Column.Error, Column.StdDev, Column.RatioSD)]
[MemoryDiagnoser]
[Config(typeof(NextormConfig))]
public class JsonStreamPhase2WriterBenchmark
{
    private readonly MemoryStream _sink = new();

    private int[] _ints = null!;
    private int[][] _jagged = null!;
    private JsonRowWriter _intArrayWriter = null!;
    private JsonRowWriter _jaggedWriter = null!;
    private FakeArrayRecord _intRecord = null!;
    private FakeArrayRecord _jaggedRecord = null!;

    [GlobalSetup]
    public void Setup()
    {
        _ints = new int[256];
        for (var i = 0; i < _ints.Length; i++)
            _ints[i] = i * 3;

        _jagged = new int[16][];
        for (var i = 0; i < _jagged.Length; i++)
        {
            _jagged[i] = new int[(i % 5) + 1];
            for (var j = 0; j < _jagged[i].Length; j++)
                _jagged[i][j] = i * 10 + j;
        }

        _intArrayWriter = BuildArrayWriter(typeof(int[]), _ints);
        _jaggedWriter = BuildArrayWriter(typeof(int[][]), _jagged);
        _intRecord = new FakeArrayRecord(_ints);
        _jaggedRecord = new FakeArrayRecord(_jagged);

        // Smoke both paths once (a rejected shape must fail setup, not the measured region).
        _ = NativeArrayInt_Stream();
        _ = NativeArrayJagged_Stream();
        _sink.SetLength(0);
    }

    private static JsonRowWriter BuildArrayWriter(Type arrayType, object value)
    {
        // Root object with one member "Items" bound to the whole-column array ordinal 0.
        var element = Element(arrayType.GetElementType()!);
        var items = JsonShapeNode.Array("Items", arrayType, element, new JsonShapeBinding(0, nullable: true, defaultOnNull: false));
        var root = JsonShapeNode.Object(null, value.GetType(), [items], JsonShapePresence.Always, slot: null, member: null);
        var plan = JsonShapePlan.Build(
            [new SelectExpression(arrayType) { Index = 0, PropertyName = "Items" }],
            oneColumn: false,
            new JsonStreamOptions(),
            root);
        return JsonRowWriterFactory.Build(plan);
    }

    private static JsonShapeNode Element(Type elementType)
    {
        var valueType = Nullable.GetUnderlyingType(elementType) ?? elementType;
        return valueType != typeof(byte[]) && valueType.IsArray
            ? JsonShapeNode.Array(null, elementType, Element(valueType.GetElementType()!), binding: null)
            : JsonShapeNode.Scalar(null, elementType, binding: null);
    }

    /// <summary>Streams a rank-one int[] member through the recursive array writer.</summary>
    [Benchmark]
    public long NativeArrayInt_Stream() => Write(_intArrayWriter, _intRecord);

    /// <summary>STJ oracle for the same int[] member.</summary>
    [Benchmark]
    public long NativeArrayInt_MaterializeStj()
        => JsonSerializer.SerializeToUtf8Bytes(new { Items = _ints }).LongLength;

    /// <summary>Streams a jagged int[][] member through the recursive array writer.</summary>
    [Benchmark]
    public long NativeArrayJagged_Stream() => Write(_jaggedWriter, _jaggedRecord);

    /// <summary>STJ oracle for the same jagged int[][] member.</summary>
    [Benchmark]
    public long NativeArrayJagged_MaterializeStj()
        => JsonSerializer.SerializeToUtf8Bytes(new { Items = _jagged }).LongLength;

    private long Write(JsonRowWriter rowWriter, FakeArrayRecord record)
    {
        _sink.SetLength(0);
        using (var writer = new Utf8JsonWriter(_sink))
        {
            writer.WriteStartArray();
            rowWriter(record, writer);
            writer.WriteEndArray();
        }

        return _sink.Length;
    }

    /// <summary>A single-column hand-written record returning the raw array value.</summary>
    private sealed class FakeArrayRecord(object value) : IDataRecord
    {
        public int FieldCount => 1;

        public object this[int i] => GetValue(i);

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => throw new NotSupportedException();

        public byte GetByte(int i) => throw new NotSupportedException();

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => value.GetType().Name;

        public DateTime GetDateTime(int i) => throw new NotSupportedException();

        public decimal GetDecimal(int i) => throw new NotSupportedException();

        public double GetDouble(int i) => throw new NotSupportedException();

        public Type GetFieldType(int i) => value.GetType();

        public float GetFloat(int i) => throw new NotSupportedException();

        public Guid GetGuid(int i) => throw new NotSupportedException();

        public short GetInt16(int i) => throw new NotSupportedException();

        public int GetInt32(int i) => throw new NotSupportedException();

        public long GetInt64(int i) => throw new NotSupportedException();

        public string GetName(int i) => "Items";

        public int GetOrdinal(string name) => throw new NotSupportedException();

        public string GetString(int i) => throw new NotSupportedException();

        public object GetValue(int i) => value;

        public int GetValues(object[] values)
        {
            values[0] = value;
            return 1;
        }

        public bool IsDBNull(int i) => false;
    }
}

[SqlTable("phase2_json_item")]
public sealed class Phase2JsonItem
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }

    [Column("value")]
    public int? Value { get; set; }

    [Column("blob")]
    public byte[]? Blob { get; set; }
}

[SqlTable("phase2_json_parent")]
public sealed class Phase2JsonParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}

[SqlTable("phase2_json_child")]
public sealed class Phase2JsonChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }

    [Column("name")]
    public string? Name { get; set; }
}
