using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Core.Tests;

/// <summary>
/// T247/D247-02 coverage for the context-owned compiled JSON row-writer cache: the provider-independent
/// semantic key (different shapes, naming policy, <c>IgnoreNull</c> and converter entries never
/// collide), the unrepresentable-metadata bypass, the bounded LRU, per-context isolation, the
/// process-wide invalidation and the storage-type independence the key relies on. A hand-written
/// <see cref="IDataRecord"/> is used where the runtime storage type is the variable under test.
/// </summary>
public class JsonRowWriterCacheTests
{
    private enum NumericState
    {
        Unknown = 0,
        Active = 7,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    private enum StringState
    {
        Unknown = 0,
        Active = 7,
    }

    // A mutable/opaque policy: JsonNamingPolicy has no immutable identity for a custom subclass, so the
    // cache must bypass it instead of guessing that two instances behave the same.
    private sealed class UpperCaseNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => name.ToUpperInvariant();
    }

    private sealed class FakeRecord : IDataRecord
    {
        private readonly object?[] _values;
        private readonly Type[] _types;

        public FakeRecord(object?[] values, Type[] types)
        {
            _values = values;
            _types = types;
        }

        public int FieldCount => _values.Length;

        public object this[int i] => GetValue(i);

        public object this[string name] => throw new NotSupportedException();

        public bool GetBoolean(int i) => (bool)_values[i]!;

        public byte GetByte(int i) => (byte)_values[i]!;

        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public char GetChar(int i) => throw new NotSupportedException();

        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length)
            => throw new NotSupportedException();

        public IDataReader GetData(int i) => throw new NotSupportedException();

        public string GetDataTypeName(int i) => _types[i].Name;

        public DateTime GetDateTime(int i) => (DateTime)_values[i]!;

        public decimal GetDecimal(int i) => (decimal)_values[i]!;

        public double GetDouble(int i) => (double)_values[i]!;

        public Type GetFieldType(int i) => _types[i];

        public float GetFloat(int i) => (float)_values[i]!;

        public Guid GetGuid(int i) => (Guid)_values[i]!;

        public short GetInt16(int i) => (short)_values[i]!;

        public int GetInt32(int i) => (int)_values[i]!;

        public long GetInt64(int i) => (long)_values[i]!;

        public string GetName(int i) => $"c{i}";

        public int GetOrdinal(string name) => throw new NotSupportedException();

        public string GetString(int i) => (string)_values[i]!;

        public object GetValue(int i) => _values[i] ?? DBNull.Value;

        public int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, _values.Length);
            for (var i = 0; i < count; i++)
                values[i] = GetValue(i);
            return count;
        }

        public bool IsDBNull(int i) => _values[i] is null;
    }

    private static SelectExpression Column(Type type, int ordinal, string? name)
        => new(type) { Index = ordinal, PropertyName = name };

    private static JsonShapeNode Scalar(string? name, Type type, int ordinal, bool nullable)
        => JsonShapeNode.Scalar(name, type, new JsonShapeBinding(ordinal, nullable, defaultOnNull: false));

    private static JsonShapeNode Obj(string? name, JsonShapeNode[] members, JsonShapePresence presence)
        => JsonShapeNode.Object(name, typeof(object), members, presence, slot: null, member: null);

    private static JsonShapePlan ShapePlan(JsonShapeNode shape, JsonStreamOptions options)
    {
        var columns = new SelectExpression[ColumnCountFor(shape)];
        for (var i = 0; i < columns.Length; i++)
            columns[i] = Column(typeof(int), i, $"c{i}");

        return JsonShapePlan.Build(columns, oneColumn: false, options, shape);
    }

    private static JsonShapePlan SingleMemberPlan(string name)
        => ShapePlan(Obj(null, [Scalar(name, typeof(int), 0, false)], JsonShapePresence.Always), new JsonStreamOptions());

    private static int ColumnCountFor(JsonShapeNode node)
    {
        var max = -1;

        void Visit(JsonShapeNode current)
        {
            if (current.Binding is { } binding)
                max = Math.Max(max, binding.Ordinal);

            if (current.Presence.Kind == JsonShapePresenceKind.AnyColumnNotNull)
                foreach (var ordinal in current.Presence.Ordinals)
                    max = Math.Max(max, ordinal);

            foreach (var member in current.Members)
                Visit(member);

            if (current.Element is not null)
                Visit(current.Element);
        }

        Visit(node);
        return max + 1;
    }

    private static string Write(JsonRowWriter rowWriter, FakeRecord record)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartArray();
            rowWriter(record, writer);
            writer.WriteEndArray();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void CachedWriter_ShouldProduceBytesIdenticalToUncached()
    {
        var cache = new JsonRowWriterCache();

        var flat = JsonShapePlan.Build(
            [Column(typeof(int), 0, "Id"), Column(typeof(string), 1, "Name")],
            oneColumn: false,
            new JsonStreamOptions());
        var flatRecord = new FakeRecord([7, "a"], [typeof(int), typeof(string)]);
        Write(cache.GetOrBuild(flat), flatRecord)
            .Should().Be(Write(JsonRowWriterFactory.Build(flat), flatRecord));

        var shape = Obj(null,
            [
                Scalar("Id", typeof(int), 0, false),
                Obj("Child", [Scalar("Name", typeof(string), 1, true)], JsonShapePresence.Always),
            ],
            JsonShapePresence.Always);
        var recursive = ShapePlan(shape, new JsonStreamOptions());
        var record = new FakeRecord([1, "b"], [typeof(int), typeof(string)]);
        var cached = Write(cache.GetOrBuild(recursive), record);

        cached.Should().Be("[{\"Id\":1,\"Child\":{\"Name\":\"b\"}}]");
        cached.Should().Be(Write(JsonRowWriterFactory.Build(recursive), record));
    }

    [Fact]
    public void WarmLookup_ShouldNotRecompile()
    {
        var cache = new JsonRowWriterCache();
        var plan = SingleMemberPlan("Value");

        var first = cache.GetOrBuild(plan);
        var second = cache.GetOrBuild(plan);

        second.Should().BeSameAs(first, "a resident entry is compiled once");
        cache.CompilationCount.Should().Be(1);
        cache.EntryCount.Should().Be(1);
    }

    [Fact]
    public void Key_ShouldSeparateShapeNamingPolicyIgnoreNullAndConverter()
    {
        var cache = new JsonRowWriterCache();

        cache.GetOrBuild(SingleMemberPlan("A"));
        cache.GetOrBuild(SingleMemberPlan("B"));
        cache.CompilationCount.Should().Be(2, "different shapes are different entries");

        // The raw member is already lower case, so null and CamelCase resolve to the same emitted
        // name; only the policy discriminator can keep the two entries apart.
        cache.GetOrBuild(ShapePlan(
            Obj(null, [Scalar("id", typeof(int), 0, false)], JsonShapePresence.Always),
            new JsonStreamOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        cache.GetOrBuild(ShapePlan(
            Obj(null, [Scalar("id", typeof(int), 0, false)], JsonShapePresence.Always),
            new JsonStreamOptions()));
        cache.CompilationCount.Should().Be(4, "the naming policy is part of the key");

        var nullableShape = Obj(null, [Scalar("V", typeof(int?), 0, true)], JsonShapePresence.Always);
        cache.GetOrBuild(ShapePlan(nullableShape, new JsonStreamOptions { IgnoreNull = true }));
        cache.GetOrBuild(ShapePlan(nullableShape, new JsonStreamOptions { IgnoreNull = false }));
        cache.CompilationCount.Should().Be(6, "IgnoreNull is part of the key");

        var numericPlan = ShapePlan(
            Obj(null, [Scalar("State", typeof(NumericState), 0, false)], JsonShapePresence.Always),
            new JsonStreamOptions());
        var stringPlan = ShapePlan(
            Obj(null, [Scalar("State", typeof(StringState), 0, false)], JsonShapePresence.Always),
            new JsonStreamOptions());

        var numericWriter = cache.GetOrBuild(numericPlan);
        var stringWriter = cache.GetOrBuild(stringPlan);

        cache.CompilationCount.Should().Be(8, "a string-enum converter is a different entry");
        Write(numericWriter, new FakeRecord([7], [typeof(int)])).Should().Be("[{\"State\":7}]");
        Write(stringWriter, new FakeRecord([7], [typeof(int)])).Should().Be("[{\"State\":\"Active\"}]");
    }

    [Fact]
    public void UnrepresentableNamingPolicy_ShouldBypassCacheWithCorrectBytes()
    {
        var cache = new JsonRowWriterCache();
        var plan = ShapePlan(
            Obj(null, [Scalar("id", typeof(int), 0, false)], JsonShapePresence.Always),
            new JsonStreamOptions { PropertyNamingPolicy = new UpperCaseNamingPolicy() });

        var writer = cache.GetOrBuild(plan);

        Write(writer, new FakeRecord([5], [typeof(int)])).Should().Be("[{\"ID\":5}]");
        cache.EntryCount.Should().Be(0, "an unrepresentable policy must not be published");
        cache.CompilationCount.Should().Be(0);

        cache.GetOrBuild(plan).Should().NotBeSameAs(writer, "a bypass compiles on every call");
        cache.EntryCount.Should().Be(0);
    }

    [Fact]
    public void Cache_ShouldStayBoundedAt64_AndEvictLeastRecentlyUsed()
    {
        var cache = new JsonRowWriterCache();
        const int capacity = JsonRowWriterCache.Capacity;

        for (var i = 0; i < capacity + 1; i++)
            cache.GetOrBuild(SingleMemberPlan($"M{i}"));

        cache.EntryCount.Should().Be(capacity);
        cache.CompilationCount.Should().Be(capacity + 1);

        // M0 was the least recently used entry when M64 was published, so it must have been evicted.
        cache.GetOrBuild(SingleMemberPlan("M0"));
        cache.CompilationCount.Should().Be(capacity + 2);

        // M1 is now the least recently used entry. Touch M2 (a hit) so M3 becomes the least recently
        // used survivor, then publish one more entry.
        cache.GetOrBuild(SingleMemberPlan("M2"));
        cache.CompilationCount.Should().Be(capacity + 2);
        cache.GetOrBuild(SingleMemberPlan("M65"));
        cache.CompilationCount.Should().Be(capacity + 3);
        cache.EntryCount.Should().Be(capacity);

        cache.GetOrBuild(SingleMemberPlan("M2"));
        cache.CompilationCount.Should().Be(capacity + 3, "the touched entry survived the eviction");

        cache.GetOrBuild(SingleMemberPlan("M3"));
        cache.CompilationCount.Should().Be(capacity + 4, "LRU (not FIFO) selects the evicted entry");
    }

    [Fact]
    public void IndependentContexts_ShouldNotShareEntries_AndClearInvalidatesThem()
    {
        using var first = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        using var second = new SqliteDataContext("Data Source=:memory:", new DataContextBuilder());
        var plan = SingleMemberPlan("Id");

        var firstWriter = first.JsonRowWriterCacheForTesting.GetOrBuild(plan);
        var secondWriter = second.JsonRowWriterCacheForTesting.GetOrBuild(plan);

        firstWriter.Should().NotBeSameAs(secondWriter, "each context owns its compiled writers");
        first.JsonRowWriterCacheForTesting.CompilationCount.Should().Be(1);
        second.JsonRowWriterCacheForTesting.CompilationCount.Should().Be(1);

        DataContextCache.Clear();
        first.JsonRowWriterCacheForTesting.GetOrBuild(plan);
        first.JsonRowWriterCacheForTesting.CompilationCount
            .Should().Be(2, "the process-wide Clear invalidates a context's resident writers lazily");

        first.PurgeQueryCache();
        first.JsonRowWriterCacheForTesting.EntryCount.Should().Be(0, "a purge releases the context's writers");
    }

    [Fact]
    public void DifferingRuntimeStorageTypes_ShouldReuseTheWriter_AndYieldIdenticalBytes()
    {
        var cache = new JsonRowWriterCache();
        var plan = JsonShapePlan.Build([Column(typeof(int), 0, "Value")], oneColumn: false, new JsonStreamOptions());
        var writer = cache.GetOrBuild(plan);

        var asInt = Write(writer, new FakeRecord([42], [typeof(int)]));
        var asLong = Write(writer, new FakeRecord([42L], [typeof(long)]));
        var asDecimal = Write(writer, new FakeRecord([42m], [typeof(decimal)]));
        var asShort = Write(writer, new FakeRecord([(short)42], [typeof(short)]));

        asInt.Should().Be("[{\"Value\":42}]");
        asLong.Should().Be(asInt);
        asDecimal.Should().Be(asInt);
        asShort.Should().Be(asInt);
        cache.CompilationCount.Should().Be(1, "the runtime provider field type is not part of the key");

        // Reader validation is per-reader and independent of the cache: an incompatible storage type is
        // still rejected by the existing binding check.
        var incompatible = new FakeRecord(["not-a-number"], [typeof(string)]);
        FluentActions.Invoking(() => JsonRowWriterFactory.ValidateReaderBinding(plan, incompatible))
            .Should().Throw<NotSupportedException>().WithMessage("*reader-binding*");
    }
}

/// <summary>
/// End-to-end coverage of the cache through the real (in-memory) SQLite context: the warm JSON write
/// reuses the resident writer, emits identical bytes, and leaves the caller's command and the context
/// plan cache untouched.
/// </summary>
public class JsonRowWriterCacheDataContextTests
{
    [SqlTable("json_cache_entity")]
    public interface IJsonCacheEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("name")]
        string? Name { get; set; }
    }

    public class JsonCacheEntity : IJsonCacheEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    private static (SqliteConnection Connection, SqliteDataContext Context) CreateSeededContext()
    {
        var connection = new SqliteConnection("Data Source=:memory:");
        connection.Open();
        using (var setup = connection.CreateCommand())
        {
            setup.CommandText =
                "create table json_cache_entity (id integer primary key, name text);" +
                "insert into json_cache_entity (id, name) values (1, 'a'), (2, 'b');";
            setup.ExecuteNonQuery();
        }

        return (connection, new SqliteDataContext(connection, new DataContextBuilder()));
    }

    [Fact]
    public void RepeatedWriteJson_ShouldReuseTheResidentWriterAndEmitIdenticalBytes()
    {
        var (connection, context) = CreateSeededContext();
        using (connection)
        using (context)
        {
            var first = new MemoryStream();
            context.From<JsonCacheEntity>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => new { x.Id, x.Name }).WriteJson(first);

            var second = new MemoryStream();
            context.From<JsonCacheEntity>().Where(x => x.Id > 0).OrderBy(x => x.Id).Select(x => new { x.Id, x.Name }).WriteJson(second);

            context.JsonRowWriterCacheForTesting.CompilationCount
                .Should().Be(1, "the second warm export reuses the resident writer");

            var secondBytes = Encoding.UTF8.GetString(second.ToArray());
            secondBytes.Should().Be(Encoding.UTF8.GetString(first.ToArray()));
            secondBytes.Should().Be("[{\"Id\":1,\"Name\":\"a\"},{\"Id\":2,\"Name\":\"b\"}]");
        }
    }

    [Fact]
    public void JsonWrite_ShouldPreserveSharedCommandStateAndPlanCache()
    {
        var (connection, context) = CreateSeededContext();
        using (connection)
        using (context)
        {
            var command = context.From<JsonCacheEntity>()
                .Where(x => x.Id > 0)
                .Select(x => new { x.Id, x.Name });
            var options = new JsonStreamOptions();

            command.Cache.Should().BeTrue("the caller's command starts cacheable");
            command.DocumentMode.Should().BeFalse();

            using (var first = new MemoryStream())
                command.WriteJson(first, options);
            using (var second = new MemoryStream())
                command.WriteJson(second, options);

            command.Cache.Should().BeTrue("the JSON path must not clear the sticky plan-cache flag");
            command.DocumentMode.Should().BeFalse("DocumentMode is set on the live clone only");
            command.JsonShape.Should().BeNull("the captured shape lives on the clone only");
            context.JsonRowWriterCacheForTesting.CompilationCount.Should().Be(1);

            // The other terminals on the same context still work and the plan cache is still live.
            context.From<JsonCacheEntity>().Any().Should().BeTrue();
            context.From<JsonCacheEntity>().Count().Should().Be(2);
            context.From<JsonCacheEntity>().ToList().Should().HaveCount(2);

            var prepared1 = context.GetPreparedQueryCommand(
                context.From<JsonCacheEntity>().ToCommand(),
                createEnumerator: false,
                storeInCache: true,
                CancellationToken.None);
            var prepared2 = context.GetPreparedQueryCommand(
                context.From<JsonCacheEntity>().ToCommand(),
                createEnumerator: false,
                storeInCache: true,
                CancellationToken.None);

            prepared2.Should().BeSameAs(prepared1, "plan caching stays enabled after a JSON write");
        }
    }
}
