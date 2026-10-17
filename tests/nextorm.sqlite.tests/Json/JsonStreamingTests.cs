using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data;
using System.Data.Common;
using System.Linq.Expressions;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

/// <summary>
/// Behavioural coverage for the WriteJson streaming terminals against a real (in-memory) SQLite
/// database: the flat scalar/object container, NDJSON independence, shaping options, the fail-fast
/// type whitelist, the caller-owned destination contract and the bounded-buffer rollover. The fixture
/// owns one open connection so the seeded tables survive for the whole class.
/// </summary>
public sealed class JsonStreamingFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public JsonStreamingFixture()
    {
        _connection = new SqliteConnection("Data Source=:memory:");
        _connection.Open();
        using (var setup = _connection.CreateCommand())
        {
            setup.CommandText =
                "create table json_entity (id integer primary key, int_value integer, name text, data blob);" +
                "insert into json_entity (id, int_value, name, data) values (1, 10, 'alpha', X'010203');" +
                "insert into json_entity (id, int_value, name, data) values (2, null, null, X'0405');" +
                "insert into json_entity (id, int_value, name, data) values (3, 20, 'gamma', null);" +
                "create table json_typed (id integer primary key, flag boolean, amount numeric, when_col datetime, tiny tinyint, data blob);" +
                "insert into json_typed (id, flag, amount, when_col, tiny, data) values (1, 1, 12.34, '2023-01-01 10:00:00', 2, X'010203');" +
                "insert into json_typed (id, flag, amount, when_col, tiny, data) values (2, 0, null, null, 0, null);" +
                "create table json_big (id integer primary key, name text);" +
                "create table unsupported_entity (id integer primary key, when_col text);" +
                "create table json_parent (id integer primary key, name text);" +
                "insert into json_parent (id, name) values (1, 'p1'), (2, 'p2'), (3, 'p3');" +
                "create table json_child (id integer primary key, parent_id integer, name text);" +
                "insert into json_child (id, parent_id, name) values (10, 1, 'c1'), (11, 1, 'c2'), (12, 2, 'c3');" +
                "create table json_enum (id integer primary key, state integer, nullable_state integer);" +
                "insert into json_enum (id, state, nullable_state) values (1, 7, null);" +
                "insert into json_enum (id, state, nullable_state) values (2, -3, 7);" +
                "create table json_enum_empty (id integer primary key, state integer);" +
                "create table json_binding (id integer primary key, int_text text);" +
                "insert into json_binding (id, int_text) values (1, 'not-a-number');";
            setup.ExecuteNonQuery();
        }

        // The first row's JSON alone exceeds the 64 KiB internal buffer, so the rollover flush (not
        // just the per-row flush) is exercised by BufferRollover_ShouldStreamLargeResult.
        using (var insert = _connection.CreateCommand())
        {
            insert.CommandText = "insert into json_big (id, name) values (@id, @name);";
            var id = insert.CreateParameter();
            id.ParameterName = "@id";
            insert.Parameters.Add(id);
            var name = insert.CreateParameter();
            name.ParameterName = "@name";
            insert.Parameters.Add(name);
            for (var i = 0; i < 5; i++)
            {
                id.Value = i + 1;
                name.Value = "row-"
                    + i.ToString(System.Globalization.CultureInfo.InvariantCulture).PadLeft(3, '0')
                    + new string('x', i == 0 ? 80_000 : 240);
                insert.ExecuteNonQuery();
            }
        }

        Context = new SqliteDataContext(_connection, new DataContextBuilder());
    }

    public SqliteDataContext Context { get; }

    /// <summary>The shared open connection, so a test can build an isolated context without reseeding.</summary>
    public DbConnection Connection => _connection;

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}

public class JsonStreamingTests : IClassFixture<JsonStreamingFixture>
{
    [SqlTable("json_entity")]
    public interface IJsonEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("int_value")]
        int? IntValue { get; set; }

        [Column("name")]
        string? Name { get; set; }

        [Column("data")]
        byte[]? Data { get; set; }
    }

    public class JsonEntity : IJsonEntity
    {
        public int Id { get; set; }
        public int? IntValue { get; set; }
        public string? Name { get; set; }
        public byte[]? Data { get; set; }
    }

    [SqlTable("json_typed")]
    public interface IJsonTypedEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("flag")]
        bool? Flag { get; set; }

        [Column("amount")]
        decimal? Amount { get; set; }

        [Column("when_col")]
        DateTime? When { get; set; }

        [Column("tiny")]
        byte Tiny { get; set; }

        [Column("data")]
        byte[]? Data { get; set; }
    }

    public class JsonTypedEntity : IJsonTypedEntity
    {
        public int Id { get; set; }
        public bool? Flag { get; set; }
        public decimal? Amount { get; set; }
        public DateTime? When { get; set; }
        public byte Tiny { get; set; }
        public byte[]? Data { get; set; }
    }

    [SqlTable("json_big")]
    public interface IBigJsonEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("name")]
        string? Name { get; set; }
    }

    public class BigJsonEntity : IBigJsonEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [SqlTable("unsupported_entity")]
    public interface IUnsupportedJsonEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("when_col")]
        DateTimeOffset When { get; set; }
    }

    public class UnsupportedJsonEntity : IUnsupportedJsonEntity
    {
        public int Id { get; set; }
        public DateTimeOffset When { get; set; }
    }

    // D180.2 reader-binding: a TEXT column projected as int lowers to a numeric JSON leaf whose provider
    // field type (System.String) the compiled numeric reader cannot convert. The incompatibility is only
    // observable after the reader is opened, so it must be rejected before the destination is written.
    [SqlTable("json_binding")]
    public interface IJsonBindingEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("int_text")]
        int IntText { get; set; }
    }

    public class JsonBindingEntity : IJsonBindingEntity
    {
        public int Id { get; set; }
        public int IntText { get; set; }
    }

    // D176.4 joined-shape entities: both sides expose an Id and a Name so duplicate leaf names across
    // projection slots are exercised, and parent 3 has no child so the unmatched outer-join slot is
    // emitted as JSON null.
    [SqlTable("json_parent")]
    public interface IJsonParentEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("name")]
        string? Name { get; set; }
    }

    public class JsonParentEntity : IJsonParentEntity
    {
        public int Id { get; set; }
        public string? Name { get; set; }
    }

    [SqlTable("json_child")]
    public interface IJsonChildEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("parent_id")]
        int ParentId { get; set; }

        [Column("name")]
        string? Name { get; set; }
    }

    public class JsonChildEntity : IJsonChildEntity
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
        public string? Name { get; set; }
    }

    // D178: a plain enum projection over a numeric column; the ordinary materializer/JSON path must
    // stream the underlying number without a value converter.
    public enum JsonEnumState
    {
        Unknown = 0,
        Active = 7,
        Disabled = -3,
    }

    [SqlTable("json_enum")]
    public interface IJsonEnumEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [Column("state")]
        JsonEnumState State { get; set; }

        [Column("nullable_state")]
        JsonEnumState? NullableState { get; set; }
    }

    public class JsonEnumEntity : IJsonEnumEntity
    {
        public int Id { get; set; }
        public JsonEnumState State { get; set; }
        public JsonEnumState? NullableState { get; set; }
    }

    // T13 empty-result shape validation: the enum member carries an unsupported [JsonConverter] and the
    // mapped table has zero rows, so the shape must still be rejected at prepare time, never excused by
    // the empty result set.
    [SqlTable("json_enum_empty")]
    public interface IJsonEmptyEnumEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [JsonConverter(typeof(JsonCustomEnumConverter))]
        [Column("state")]
        JsonCustomEnum State { get; set; }
    }

    public class JsonEmptyEnumEntity : IJsonEmptyEnumEntity
    {
        public int Id { get; set; }

        // The projection lambda binds this property, so the unsupported attribute must live here too.
        [JsonConverter(typeof(JsonCustomEnumConverter))]
        public JsonCustomEnum State { get; set; }
    }

    // Named/member-init nested construction target types (not anonymous).
    public sealed class JsonNamedOuter
    {
        public int Id { get; set; }
        public JsonNamedInner? Child { get; set; }
    }

    public sealed class JsonNamedInner
    {
        public string? Name { get; set; }
        public int? Value { get; set; }
    }

    // A parameterless, member-less construction used to exercise an empty nested JSON object.
    public sealed class JsonEmpty
    {
    }

    // A policy that maps two distinct nested member names onto one effective name while leaving the
    // root members untouched, so the duplicate is inside the nested object scope, not the root.
    private sealed class SelectiveDuplicateNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name)
            => name is "Name" or "IntValue" ? "collision" : name;
    }

    private sealed class RecordingSqlInterceptor : IQueryInterceptor
    {
        public List<string> Sql { get; } = [];

        public void CommandInitialized(CommandEventData eventData, DbCommand command) { }

        public void CommandExecuting(CommandEventData eventData, DbCommand command) => Sql.Add(command.CommandText);

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed) { }

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception) { }
    }

    // Captures both the rendered SQL and the bound parameter values so the JSON terminal can be checked
    // against ordinary execution on the same enum command (T17).
    private sealed class RecordingParameterInterceptor : IQueryInterceptor
    {
        public List<(string Sql, List<(string Name, object? Value)> Parameters)> Executions { get; } = [];

        public void CommandInitialized(CommandEventData eventData, DbCommand command) { }

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
        {
            var parameters = new List<(string, object?)>();
            foreach (DbParameter parameter in command.Parameters)
                parameters.Add((parameter.ParameterName, parameter.Value));
            Executions.Add((command.CommandText, parameters));
        }

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed) { }

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception) { }
    }

    // A naming policy whose (invalid) result is the same for every column, so the second one collides.
    private sealed class ConstantNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => "collision";
    }

    // A policy that returns null; JSON object member names cannot be null.
    private sealed class NullNamingPolicy : JsonNamingPolicy
    {
        public override string ConvertName(string name) => null!;
    }

    // Destination that cancels the token after its first (async) write, then throws from every later
    // write because a well-behaved async Stream observes the cancellation token it is handed.
    private sealed class CancellingWriteStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly CancellationTokenSource _cts;

        public CancellingWriteStream(CancellationTokenSource cts) => _cts = cts;

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => _inner.Flush();
        public override Task FlushAsync(CancellationToken cancellationToken) => _inner.FlushAsync(cancellationToken);
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _inner.Write(buffer, offset, count);
            _cts.Cancel();
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _inner.Write(buffer.Span);
            _cts.Cancel();
            return ValueTask.CompletedTask;
        }
    }

    // Destination that fails on every write, to observe the partial-output error contract.
    private sealed class ThrowingWriteStream : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => 0;
        public override long Position { get => 0; set => throw new NotSupportedException(); }

        public override void Flush() { }
        public override Task FlushAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
            => throw new InvalidOperationException("destination failed");
    }

    // Destination that accepts a successful prefix and then fails every later write, so a public-surface
    // test can pin the partial-output contract across the row boundary: the already-written prefix stays
    // (no rollback), no closing bracket / recovery tail is added, no `Stream.Flush` is called and the
    // destination stays caller-owned/open. Each successful destination write is one full row frame.
    private sealed class ThrowingAfterBytesStream : Stream
    {
        private readonly MemoryStream _inner = new();
        private readonly int _writesBeforeThrow;
        private int _writeCalls;

        public ThrowingAfterBytesStream(int writesBeforeThrow) => _writesBeforeThrow = writesBeforeThrow;

        public int FlushCalls { get; private set; }

        public string Text => Encoding.UTF8.GetString(_inner.ToArray());

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _inner.Length;
        public override long Position { get => _inner.Position; set => throw new NotSupportedException(); }

        public override void Flush() => FlushCalls++;
        public override Task FlushAsync(CancellationToken cancellationToken) { FlushCalls++; return Task.CompletedTask; }
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count)
        {
            _writeCalls++;
            if (_writeCalls > _writesBeforeThrow)
                throw new InvalidOperationException("destination failed after prefix");
            _inner.Write(buffer, offset, count);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _writeCalls++;
            if (_writeCalls > _writesBeforeThrow)
                throw new InvalidOperationException("destination failed after prefix");
            _inner.Write(buffer.Span);
            return ValueTask.CompletedTask;
        }
    }

    private readonly IDataContext _ctx;
    private readonly JsonStreamingFixture _fixture;

    public JsonStreamingTests(JsonStreamingFixture fixture)
    {
        _fixture = fixture;
        _ctx = fixture.Context;
    }

    private static string Utf8(MemoryStream stream) => Encoding.UTF8.GetString(stream.ToArray());

    [Fact]
    public void Array_ScalarInt_ShouldEqualJsonSerializerSerialize()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    [Fact]
    public void Array_FlatDto_ShouldEqualJsonSerializerSerialize()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.IntValue, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    [Fact]
    public void NdJson_EachLineIsIndependentJson()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });

        // Every record (including the last) ends with '\n'; ignore the known trailing terminator here.
        var lines = Utf8(stream).Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines.Should().HaveCount(3);
        foreach (var line in lines)
        {
            using var document = JsonDocument.Parse(line);
            document.RootElement.ValueKind.Should().Be(JsonValueKind.Object);
        }

        // An empty result produces no records at all (not "null" and no stray newline).
        var empty = _ctx.From<JsonEntity>().Where(x => x.Id > 100).Select(x => new { x.Id });
        using var emptyStream = new MemoryStream();
        empty.WriteJson(emptyStream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });
        Utf8(emptyStream).Should().BeEmpty();
    }

    [Fact]
    public void Root_ShouldWrapArray()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions { Root = "items" });

        Utf8(stream).Should().Be("{\"items\":[1,2,3]}");
    }

    [Fact]
    public void IgnoreNull_ShouldOmitNullMembersButKeepAnonymousNulls()
    {
        // An object member whose SQL value is NULL is dropped when IgnoreNull is set...
        var obj = _ctx.From<JsonEntity>().Where(x => x.Id == 2).Select(x => new { x.Id, x.Name });
        using var objStream = new MemoryStream();
        obj.WriteJson(objStream, new JsonStreamOptions { IgnoreNull = true });
        Utf8(objStream).Should().Be("[{\"Id\":2}]");

        // ...but a NULL scalar value is the value itself and is still emitted.
        var scalar = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Name);
        using var scalarStream = new MemoryStream();
        scalar.WriteJson(scalarStream, new JsonStreamOptions { IgnoreNull = true });
        Utf8(scalarStream).Should().Be(JsonSerializer.Serialize(scalar.ToList()));
        Utf8(scalarStream).Should().Contain("null");
    }

    [Fact]
    public void WriteIndented_ShouldProduceIndentedArray()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions { WriteIndented = true });

        var text = Utf8(stream);
        text.Should().Contain("\n");
        text.Should().Be(JsonSerializer.Serialize(command.ToList(), new JsonSerializerOptions { WriteIndented = true }));
    }

    [Fact]
    public void PropertyNamingPolicy_CamelCase_ShouldApply()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        Utf8(stream).Should().Be(
            JsonSerializer.Serialize(command.ToList(), new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
    }

    [Fact]
    public void NamingPolicy_CollisionOrNull_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });

        using var collision = new MemoryStream();
        collision.WriteByte(1);
        var collisionPosition = collision.Position;
        var collisionAct = () => command.WriteJson(collision, new JsonStreamOptions { PropertyNamingPolicy = new ConstantNamingPolicy() });

        collisionAct.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [names]*");
        collision.Position.Should().Be(collisionPosition);
        collision.Length.Should().Be(collisionPosition);

        using var nullName = new MemoryStream();
        nullName.WriteByte(1);
        var nullPosition = nullName.Position;
        var nullAct = () => command.WriteJson(nullName, new JsonStreamOptions { PropertyNamingPolicy = new NullNamingPolicy() });

        nullAct.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [names]*");
        nullName.Position.Should().Be(nullPosition);
        nullName.Length.Should().Be(nullPosition);
    }

    [Fact]
    public void UnknownJsonStreamMode_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream, new JsonStreamOptions { Mode = (JsonStreamMode)999 });

        act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [mode-options]*");
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    [Fact]
    public void EmptyResult_ShouldProduceEmptyArray()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id > 100).OrderBy(x => x.Id).Select(x => x.Id);
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be("[]");
    }

    [Fact]
    public void UnsupportedType_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<UnsupportedJsonEntity>().Select(x => new { x.Id, x.When });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;
        var length = stream.Length;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(length);
    }

    [Fact]
    public void NdJson_WithRoot_ShouldThrow()
    {
        var command = _ctx.From<JsonEntity>().Select(x => x.Id);
        using var stream = new MemoryStream();

        var act = () => command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson, Root = "items" });

        act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [mode-options]*");
    }

    [Fact]
    public void NdJson_WithWriteIndented_ShouldThrow()
    {
        var command = _ctx.From<JsonEntity>().Select(x => x.Id);
        using var stream = new MemoryStream();

        var act = () => command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson, WriteIndented = true });

        act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [mode-options]*");
    }

    [Fact]
    public void DestinationIsNotClosed()
    {
        var command = _ctx.From<JsonEntity>().Select(x => x.Id);
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        stream.CanWrite.Should().BeTrue();
        var write = () => stream.WriteByte(1);
        write.Should().NotThrow();
    }

    [Fact]
    public async Task Async_DestinationNotClosed()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        using var stream = new MemoryStream();

        await command.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

        stream.CanWrite.Should().BeTrue();
        var write = () => stream.WriteByte(1);
        write.Should().NotThrow();
    }

    [Fact]
    public async Task SyncAndAsync_ShouldProduceIdenticalBytes()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    // D179 V12 async NdJson success parity: the async terminal must emit the exact same NDJSON bytes as
    // the sync mode (every row an independent JSON object, trailing newline included).
    [Fact]
    public async Task NdJson_Async_ShouldProduceIdenticalBytes()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });
        await command.WriteJsonAsync(async, new JsonStreamOptions { Mode = JsonStreamMode.NdJson }, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
        Utf8(async).Should().Be("{\"Id\":1,\"Name\":\"alpha\"}\n{\"Id\":2,\"Name\":null}\n{\"Id\":3,\"Name\":\"gamma\"}\n");
    }

    [Fact]
    public async Task BufferRollover_ShouldStreamLargeResult()
    {
        var command = _ctx.From<BigJsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var sync = new MemoryStream();

        command.WriteJson(sync);

        // More than one 64 KiB buffer full, so the rollover flush (not just the per-row flush) runs.
        sync.Length.Should().BeGreaterThan(64 * 1024);
        Utf8(sync).Should().Be(JsonSerializer.Serialize(command.ToList()));

        using var async = new MemoryStream();
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public void PartialOutput_OnMidWriteFailure()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new ThrowingWriteStream();

        var act = () => command.WriteJson(stream);

        act.Should().Throw<InvalidOperationException>().WithMessage("destination failed");
        // The destination stays caller-owned (not closed) and the context is immediately reusable,
        // proving the reader and the rented buffer were released.
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public async Task Cancellation_DuringWrite_ShouldAbort()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var cts = new CancellationTokenSource();
        using var stream = new CancellingWriteStream(cts);

        var act = () => command.WriteJsonAsync(stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.CanWrite.Should().BeTrue();
    }

    // D179: the async sink-I/O cell of the array route (the throwing destination fails the awaited
    // flush); the destination stays caller-owned and the context stays usable.
    [Fact]
    public async Task PartialOutputAsync_OnMidWriteFailure()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new ThrowingWriteStream();

        var act = () => command.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("destination failed");
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    // D179: the NDJSON cells of the same matrix — NDJSON has no framing, so a mid-stream failure leaves
    // the already-written lines in place and cancellation is never converted to success.
    [Fact]
    public void NdJson_PartialOutput_OnMidWriteFailure()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new ThrowingWriteStream();

        var act = () => command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });

        act.Should().Throw<InvalidOperationException>().WithMessage("destination failed");
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public async Task NdJson_Cancellation_DuringWrite_ShouldAbort()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var cts = new CancellationTokenSource();
        using var stream = new CancellingWriteStream(cts);

        var act = () => command.WriteJsonAsync(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.CanWrite.Should().BeTrue();
    }

    // D179 public-surface prefix retention: the first row is flushed as a successful prefix, then the
    // destination fails. The exact prefix stays (no rollback), no closing bracket / recovery tail is
    // appended, no `Stream.Flush` is added, and the destination stays caller-owned/open. Mirrors the
    // managed-seam `JsonStreamWriterTests.WriteRow_SyncSinkFailure_ShouldKeepDestinationOpenAndPrefix`.
    [Fact]
    public void PartialPrefix_SyncSinkFailure_ShouldKeepPrefixAndDestinationOpen()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new ThrowingAfterBytesStream(writesBeforeThrow: 1);

        var act = () => command.WriteJson(stream);

        act.Should().Throw<InvalidOperationException>().WithMessage("destination failed after prefix");
        stream.Text.Should().Be("[{\"Id\":1,\"Name\":\"alpha\"}");
        stream.Text.Should().NotContain("]");
        stream.FlushCalls.Should().Be(0);
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public async Task PartialPrefix_AsyncSinkFailure_ShouldKeepPrefixAndDestinationOpen()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new ThrowingAfterBytesStream(writesBeforeThrow: 1);

        var act = () => command.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("destination failed after prefix");
        stream.Text.Should().Be("[{\"Id\":1,\"Name\":\"alpha\"}");
        stream.Text.Should().NotContain("]");
        stream.FlushCalls.Should().Be(0);
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public void NdJson_PartialPrefix_SyncSinkFailure_ShouldKeepPrefixAndDestinationOpen()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new ThrowingAfterBytesStream(writesBeforeThrow: 1);

        var act = () => command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });

        act.Should().Throw<InvalidOperationException>().WithMessage("destination failed after prefix");
        stream.Text.Should().Be("{\"Id\":1,\"Name\":\"alpha\"}\n");
        stream.FlushCalls.Should().Be(0);
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public async Task NdJson_PartialPrefix_AsyncSinkFailure_ShouldKeepPrefixAndDestinationOpen()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new ThrowingAfterBytesStream(writesBeforeThrow: 1);

        var act = () => command.WriteJsonAsync(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson }, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("destination failed after prefix");
        stream.Text.Should().Be("{\"Id\":1,\"Name\":\"alpha\"}\n");
        stream.FlushCalls.Should().Be(0);
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public void NdJson_UnsupportedShape_ShouldRejectBeforeOutputKeepingSentinel()
    {
        // The pre-output refusal is route- and mode-independent: NDJSON never touches a sentinel-preloaded
        // destination when the projection is rejected while the shape is planned.
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
            .Select(x => new { Empty = new JsonEmpty() });
        var before = PreloadSentinel(out var stream);
        using (stream)
        {
            var act = () => command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });

            act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [projection]*");
            stream.ToArray().Should().Equal(before);
        }
    }

    [Fact]
    public void TempTableQuery_ShouldThrow()
    {
        var source = _ctx.From<JsonEntity>().Select(x => new { x.Id, x.Name }).AsTempTable();
        var command = _ctx.From(source).Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [unsupported-execution-form]*");
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    // D179 V15 async twin: the temp-table refusal is execution-form based, so the async terminal must
    // reject it identically and leave the sentinel-preloaded destination unchanged.
    [Fact]
    public async Task TempTableQueryAsync_ShouldThrow()
    {
        var source = _ctx.From<JsonEntity>().Select(x => new { x.Id, x.Name }).AsTempTable();
        var command = _ctx.From(source).Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*JSON streaming validation [unsupported-execution-form]*");
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    // D180.2 / R180-03: unsupported metadata or an incompatible reader schema is rejected with a stable
    // `JSON streaming validation [<token>]` message, and the destination preloaded with sentinel bytes
    // stays byte-for-byte unchanged (no framing, no partial row). A valid query still writes.
    [Fact]
    public void UnsupportedShape_ShouldRejectBeforeOutputKeepingSentinel()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
            .Select(x => new { Empty = new JsonEmpty() });
        var before = PreloadSentinel(out var stream);
        using (stream)
        {
            var act = () => command.WriteJson(stream);

            act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [projection]*");
            stream.ToArray().Should().Equal(before);
        }
    }

    [Fact]
    public void UnsupportedColumn_ShouldRejectBeforeOutputKeepingSentinel()
    {
        var command = _ctx.From<UnsupportedJsonEntity>().Select(x => new { x.Id, x.When });
        var before = PreloadSentinel(out var stream);
        using (stream)
        {
            var act = () => command.WriteJson(stream);

            act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [unsupported-column]*");
            stream.ToArray().Should().Equal(before);
        }
    }

    [Fact]
    public void IncompatibleReaderSchema_ShouldRejectBeforeOutputKeepingSentinel()
    {
        var command = _ctx.From<JsonBindingEntity>().Select(x => new { x.Id, x.IntText });
        var before = PreloadSentinel(out var stream);
        using (stream)
        {
            var act = () => command.WriteJson(stream);

            act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [reader-binding]*");
            stream.ToArray().Should().Equal(before);
        }
    }

    [Fact]
    public async Task IncompatibleReaderSchemaAsync_ShouldRejectBeforeOutputKeepingSentinel()
    {
        var command = _ctx.From<JsonBindingEntity>().Select(x => new { x.Id, x.IntText });
        var before = PreloadSentinel(out var stream);
        using (stream)
        {
            var act = () => command.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*JSON streaming validation [reader-binding]*");
            stream.ToArray().Should().Equal(before);
        }
    }

    // W3 (r2/n2): the reader-binding guard is metadata-based, so it must run even when the result set is
    // empty. An incompatible schema is rejected before any destination write with no row ever read; a
    // compatible schema still emits `[]` (proving the unconditional call did not break empty output).
    [Fact]
    public void IncompatibleReaderSchemaEmptyResult_ShouldRejectBeforeOutputKeepingSentinel()
    {
        var command = _ctx.From<JsonBindingEntity>().Where(x => x.Id > 100).Select(x => new { x.Id, x.IntText });
        var before = PreloadSentinel(out var stream);
        using (stream)
        {
            var act = () => command.WriteJson(stream);

            act.Should().Throw<NotSupportedException>().WithMessage("*JSON streaming validation [reader-binding]*");
            stream.ToArray().Should().Equal(before);
        }
    }

    [Fact]
    public async Task IncompatibleReaderSchemaEmptyResultAsync_ShouldRejectBeforeOutputKeepingSentinel()
    {
        var command = _ctx.From<JsonBindingEntity>().Where(x => x.Id > 100).Select(x => new { x.Id, x.IntText });
        var before = PreloadSentinel(out var stream);
        using (stream)
        {
            var act = () => command.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

            await act.Should().ThrowAsync<NotSupportedException>().WithMessage("*JSON streaming validation [reader-binding]*");
            stream.ToArray().Should().Equal(before);
        }
    }

    [Fact]
    public void CompatibleReaderSchemaEmptyResult_ShouldWriteEmptyArray()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id > 100).OrderBy(x => x.Id).Select(x => x.Id);
        var sentinel = PreloadSentinel(out var stream);
        using (stream)
        {
            command.WriteJson(stream);

            var written = stream.ToArray();
            written.Take(sentinel.Length).Should().Equal(sentinel);
            Encoding.UTF8.GetString(written.AsSpan(sentinel.Length)).Should().Be("[]");
        }
    }

    [Fact]
    public async Task CompatibleReaderSchemaEmptyResultAsync_ShouldWriteEmptyArray()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id > 100).OrderBy(x => x.Id).Select(x => x.Id);
        var sentinel = PreloadSentinel(out var stream);
        using (stream)
        {
            await command.WriteJsonAsync(stream, TestContext.Current.CancellationToken);

            var written = stream.ToArray();
            written.Take(sentinel.Length).Should().Equal(sentinel);
            Encoding.UTF8.GetString(written.AsSpan(sentinel.Length)).Should().Be("[]");
        }
    }

    [Fact]
    public void ValidQuery_ShouldWriteAfterSentinelPreload()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1).Select(x => new { x.Id, x.Name });
        var sentinel = PreloadSentinel(out var stream);
        using (stream)
        {
            command.WriteJson(stream);

            var written = stream.ToArray();
            written.Should().HaveCountGreaterThan(sentinel.Length);
            written.Take(sentinel.Length).Should().Equal(sentinel);
            Encoding.UTF8.GetString(written.AsSpan(sentinel.Length)).Should().Be("[{\"Id\":1,\"Name\":\"alpha\"}]");
        }
    }

    private static byte[] PreloadSentinel(out MemoryStream stream)
    {
        stream = new MemoryStream();
        stream.Write([0x53, 0x45, 0x4E, 0x54, 0x49, 0x4E, 0x45, 0x4C]); // "SENTINEL"
        return stream.ToArray();
    }

    [Fact]
    public void WriteJson_SqlShouldMatchToListSql()
    {
        // A context local to this test, over the fixture's open connection, so registering the
        // interceptor cannot leak into the shared fixture (AddInterceptor has no removal API).
        var interceptor = new RecordingSqlInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        var command = context.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });

        command.ToList();
        var listSql = interceptor.Sql[^1];

        using var stream = new MemoryStream();
        command.WriteJson(stream);
        var jsonSql = interceptor.Sql[^1];

        jsonSql.Should().Be(listSql);
    }

    [Fact]
    public void BytesType_ShouldBase64Encode()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Data });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var text = Utf8(stream);
        text.Should().Contain("AQID");
        text.Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    [Fact]
    public void TypedColumns_ShouldMatchJsonSerializer()
    {
        // bool, decimal (numeric field type), DateTime, byte[] and a tinyint-backed byte projection:
        // the last is sent as sbyte/byte depending on the provider, guarding the typed numeric read.
        var command = _ctx.From<JsonTypedEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Flag, x.Amount, x.When, x.Tiny, x.Data });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    [Fact]
    public void NdJson_TrailingNewline_ShouldMatch()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });

        var text = Utf8(stream);
        text.Should().EndWith("\n");
        text.Should().Be(string.Concat(command.ToList().Select(row => JsonSerializer.Serialize(row) + "\n")));
    }

    // D176.3 focused coverage: nested object recursion and per-object scoped name validation through the
    // real SQLite terminal (the preparer captures the shape and the recursive writer consumes it).

    [Fact]
    public void NestedObject_ShouldMatchJsonSerializer()
    {
        // The ordinary materializer does not support a nested construction (that is #172), so the oracle
        // is STJ over constructed objects with the same shape and values, not command.ToList().
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name, x.IntValue } });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Child = new { Name = "alpha", IntValue = (int?)10 } },
            new { Id = 2, Child = new { Name = (string?)null, IntValue = (int?)null } },
            new { Id = 3, Child = new { Name = "gamma", IntValue = (int?)20 } },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void NestedObject_SameNameInDifferentScopes_ShouldBeAccepted()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Id } });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new[]
        {
            new { Id = 1, Child = new { Id = 1 } },
            new { Id = 2, Child = new { Id = 2 } },
            new { Id = 3, Child = new { Id = 3 } },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void WholeEntity_ShouldStreamFlatObject()
    {
        // The whole-entity terminal is captured as a root entity object with any-column-not-null presence;
        // it must stream exactly like the ordinary materialized entity (the D176.2 temporary guard had
        // rejected every captured shape, so this restores the second terminal).
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id);
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    [Fact]
    public void NestedObject_DuplicateNameWithinScope_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream, new JsonStreamOptions { PropertyNamingPolicy = new ConstantNamingPolicy() });

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    // D176.4 end-to-end boundary sweep: nested multi-level/named shapes, the actual Projection<T1,T2>
    // terminal, slot forms, both terminal surfaces (sync/async), lifecycle and ordinary-materializer /
    // plan-cache isolation. Supported results use the STJ oracle; distinct sentinel leaves catch a
    // wrong-ordinal binding.

    private string JsonOf<T>(QueryCommand<T> command, JsonStreamOptions? options = null)
    {
        using var stream = new MemoryStream();
        if (options is null)
            command.WriteJson(stream);
        else
            command.WriteJson(stream, options);

        return Utf8(stream);
    }

    private string JsonOf<TEntity>(EntityBuilder<TEntity> builder)
    {
        using var stream = new MemoryStream();
        builder.WriteJson(stream);
        return Utf8(stream);
    }

    [Fact]
    public void NestedObject_MultipleLevels_ShouldMatchSerializer()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Outer = new { x.Name, Inner = new { x.IntValue } } });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Outer = new { Name = "alpha", Inner = new { IntValue = (int?)10 } } },
            new { Id = 2, Outer = new { Name = (string?)null, Inner = new { IntValue = (int?)null } } },
            new { Id = 3, Outer = new { Name = "gamma", Inner = new { IntValue = (int?)20 } } },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void NestedMemberInit_ShouldMatchSerializer()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new JsonNamedOuter
            {
                Id = x.Id,
                Child = new JsonNamedInner { Name = x.Name, Value = x.IntValue },
            });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new[]
        {
            new JsonNamedOuter { Id = 1, Child = new JsonNamedInner { Name = "alpha", Value = 10 } },
            new JsonNamedOuter { Id = 2, Child = new JsonNamedInner { Name = null, Value = null } },
            new JsonNamedOuter { Id = 3, Child = new JsonNamedInner { Name = "gamma", Value = 20 } },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void NestedObject_AllNullChild_ShouldStayObject()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 2)
            .Select(x => new { x.Id, Child = new { x.Name, x.IntValue } });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be("[{\"Id\":2,\"Child\":{\"Name\":null,\"IntValue\":null}}]");
    }

    [Fact]
    public void BareProjection_LeftJoin_ShouldMatchItem1Item2()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .LeftJoin(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId);

        var expected = JsonSerializer.Serialize(command.ToList());
        var actual = JsonOf(command);

        actual.Should().Be(expected);
        actual.Should().Contain("\"Item1\"").And.Contain("\"Item2\"");
        actual.Should().Contain("\"Item2\":null", "the unmatched outer-join slot for parent 3 is JSON null");
    }

    [Fact]
    public void BareProjection_InnerJoin_ShouldMatchItem1Item2()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .Join(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId);

        var expected = JsonSerializer.Serialize(command.ToList());
        JsonOf(command).Should().Be(expected);
    }

    [Fact]
    public void BareProjection_DuplicateLeafNamesAcrossSlots_ShouldBeAccepted()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .LeftJoin(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId);

        using var document = JsonDocument.Parse(JsonOf(command));
        var first = document.RootElement[0];

        // Id/Name exist in both slots; uniqueness is enforced per object scope, not globally.
        first.GetProperty("Item1").GetProperty("Id").GetInt32().Should().Be(1);
        first.GetProperty("Item2").GetProperty("Id").GetInt32().Should().Be(10);
        first.GetProperty("Item1").GetProperty("Name").GetString().Should().Be("p1");
        first.GetProperty("Item2").GetProperty("Name").GetString().Should().Be("c1");
    }

    [Fact]
    public void EntityItemWithScalarMember_ShouldNotWrapScalarInObject()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .Join(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId)
            .Select(p => new { p.Item1, ChildName = p.Item2.Name });
        var actual = JsonOf(command);

        actual.Should().Be(JsonSerializer.Serialize(command.ToList()));

        // The scalar slot is a JSON string; only the entity slot is an object.
        using var document = JsonDocument.Parse(actual);
        var first = document.RootElement[0];
        first.GetProperty("Item1").ValueKind.Should().Be(JsonValueKind.Object);
        first.GetProperty("ChildName").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public void ScalarScalarSlots_ShouldBeFlatScalars()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .Join(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId)
            .Select(p => new { ParentName = p.Item1.Name, ChildName = p.Item2.Name });
        var actual = JsonOf(command);

        actual.Should().Be(JsonSerializer.Serialize(command.ToList()));

        using var document = JsonDocument.Parse(actual);
        var first = document.RootElement[0];
        first.GetProperty("ParentName").ValueKind.Should().Be(JsonValueKind.String);
        first.GetProperty("ChildName").ValueKind.Should().Be(JsonValueKind.String);
    }

    [Fact]
    public async Task BareProjection_Async_ShouldMatchSync()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .LeftJoin(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId);
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public async Task NestedQueryCommand_Async_ShouldMatchSync()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public void RepeatedWriteJson_OnOneContext_ShouldBeStable()
    {
        var first = JsonOf(_ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } }));
        var second = JsonOf(_ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } }));

        second.Should().Be(first);
    }

    [Fact]
    public void JsonThenOrdinary_AndReverse_ShouldNotLeakShape()
    {
        var nestedJson = JsonOf(_ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } }));

        // After a recursive-shape JSON write, the ordinary materializer is unchanged.
        var ordinary = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name }).ToList();
        ordinary.Select(x => x.Name).Should().Equal("alpha", null, "gamma");

        // And the flat JSON shape is not contaminated by the earlier nested one.
        var flatJson = JsonOf(_ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name }));
        flatJson.Should().Be(JsonSerializer.Serialize(ordinary));
        nestedJson.Should().Contain("\"Child\"");
        flatJson.Should().NotContain("\"Child\"");
    }

    [Fact]
    public void JsonWrite_ShouldNotDisablePlanCache()
    {
        // A context local to this test so AddInterceptor cannot leak into the shared fixture.
        var interceptor = new RecordingSqlInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        var flat = context.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name });
        var before = context.GetPreparedQueryCommand(flat, createEnumerator: false, storeInCache: true, CancellationToken.None);

        flat.WriteJson(new MemoryStream());

        flat.Cache.Should().BeTrue("the JSON clone must not set the sticky QueryCommand.Cache=false flag");
        var after = context.GetPreparedQueryCommand(flat, createEnumerator: false, storeInCache: true, CancellationToken.None);
        ReferenceEquals(before, after).Should().BeTrue("the caller's plan is still served from the plan cache");
    }

    [Fact]
    public void NestedPartialOutput_OnMidWriteFailure()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } });
        using var stream = new ThrowingWriteStream();

        var act = () => command.WriteJson(stream);

        act.Should().Throw<InvalidOperationException>().WithMessage("destination failed");
        stream.CanWrite.Should().BeTrue();
        _ctx.From<JsonEntity>().Select(x => x.Id).ToList().Should().HaveCount(3);
    }

    [Fact]
    public async Task NestedCancellation_DuringWrite_ShouldAbort()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } });
        using var cts = new CancellationTokenSource();
        using var stream = new CancellingWriteStream(cts);

        var act = () => command.WriteJsonAsync(stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.CanWrite.Should().BeTrue();
    }

    // D176.7 conditional-construction coverage: a translatable predicate choosing a New/MemberInit
    // construction arm against a null arm lowers to a scoped object plus a hidden nullable sentinel
    // presence column (CASE ... THEN non-null ELSE null). Presence comes from that sentinel, never from
    // the visible payload; the hidden column is never a JSON member; both terminal surfaces agree.

    [Fact]
    public void ConditionalConstruction_NullArmTrue_ShouldMatchSerializer()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name == null ? null : new { x.Name } });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Child = new { Name = "alpha" } },
            new { Id = 2, Child = (object?)null },
            new { Id = 3, Child = new { Name = "gamma" } },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void ConditionalConstruction_NullArmFalse_ShouldMatchSerializer()
    {
        // The other null-arm orientation: the null arm is the false arm and the construction is the true arm.
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name != null ? new { x.Name } : null });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Child = new { Name = "alpha" } },
            new { Id = 2, Child = (object?)null },
            new { Id = 3, Child = new { Name = "gamma" } },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void ConditionalConstruction_AllNullProperties_ShouldStayObject()
    {
        // The construction arm is chosen by Id, not by the visible Name; row 2 has an all-null object that
        // must stay an object (explicit presence), while rows with the null arm are JSON null.
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Id == 2 ? new { x.Name } : null });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Child = (object?)null },
            new { Id = 2, Child = new { Name = (string?)null } },
            new { Id = 3, Child = (object?)null },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void ConditionalConstruction_MemberInit_ShouldMatchSerializer()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name == null ? null : new JsonNamedInner { Name = x.Name, Value = x.IntValue } });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Child = new JsonNamedInner { Name = "alpha", Value = 10 } },
            new { Id = 2, Child = (JsonNamedInner?)null },
            new { Id = 3, Child = new JsonNamedInner { Name = "gamma", Value = 20 } },
        });
        Utf8(stream).Should().Be(expected);
    }

    [Fact]
    public void ConditionalConstruction_Root_ShouldMatchSerializer()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
            .Select(x => x.Name == null ? null : new { x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(new object[] { new { Name = "alpha" } }));
    }

    [Fact]
    public async Task ConditionalConstruction_Async_ShouldMatchSync()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name == null ? null : new { x.Name } });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public void ConditionalConstruction_HiddenColumn_NotMemberAndOrdinaryUnchanged()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name == null ? null : new { x.Name } });

        // The ordinary materializer/query path is untouched by the JSON-only lowering.
        var ordinary = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.Name }).ToList();
        ordinary.Select(x => x.Name).Should().Equal("alpha", null, "gamma");

        using var stream = new MemoryStream();
        command.WriteJson(stream);
        var text = Utf8(stream);

        // Exact oracle equality proves no hidden presence column leaked into the JSON shape.
        var expected = JsonSerializer.Serialize(new object[]
        {
            new { Id = 1, Child = new { Name = "alpha" } },
            new { Id = 2, Child = (object?)null },
            new { Id = 3, Child = new { Name = "gamma" } },
        });
        text.Should().Be(expected);
        text.Should().NotContain("json", "the sentinel value must never appear in JSON output");
        command.Cache.Should().BeTrue("the JSON clone must not set the sticky QueryCommand.Cache=false flag");
    }

    [Fact]
    public void ConditionalConstruction_UntranslatablePredicate_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name!.Normalize() == "a" ? new { x.Name } : null });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    [Fact]
    public void ConditionalConstruction_BothArmsConstruct_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name == null ? new { Name = (string?)x.Name } : new { Name = (string?)x.Name } });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    [Fact]
    public void ConditionalConstruction_ConstructionAgainstNonNullArm_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = x.Name == null ? (object)new { x.Name } : (object)x.Id });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    // CHECK r2-n2 loopback: T3 (per-scope duplicate name actually inside one nested object), T4 (missing
    // async terminal surfaces), W3 (nested empty construction) and T5 lifecycle/disposal edges.

    [Fact]
    public void NestedObject_DuplicateNameWithinNestedScopeOnly_ShouldThrowBeforeOutput()
    {
        // The root members (Id, Child) keep distinct names; only the two members inside Child collapse to
        // one effective name, proving the per-object check rejects a duplicate inside a nested scope.
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name, x.IntValue } });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream, new JsonStreamOptions { PropertyNamingPolicy = new SelectiveDuplicateNamingPolicy() });

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    [Fact]
    public void NestedEmptyConstruction_ShouldThrowBeforeOutput()
    {
        // W3: a nested construction with no scalar members lowers zero columns; it must fail closed with an
        // explicit message before any output is written, instead of producing invalid SQL or a misleading
        // "requires an explicit Select projection" guard.
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
            .Select(x => new { Empty = new JsonEmpty() });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    [Fact]
    public async Task EntityBuilder_Async_ShouldMatchSync()
    {
        var builder = _ctx.From<JsonEntity>().OrderBy(x => x.Id);
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        builder.WriteJson(sync);
        await builder.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    // D180.6 / E180-17 + S6 (r2/n2): the public `params` overloads are positional SQL parameter bindings
    // forwarded to the existing executor path. Each case independently asserts the positional result
    // (exact JSON or a literal-bound oracle), covering multiple values, an empty set and a null element
    // on both terminal surfaces and both sync/async.
    [Fact]
    public void WriteJson_WithPositionalParams_ShouldMatchOrdinaryExecution()
    {
        var command = _ctx.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int>(0))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions(), CancellationToken.None, 1);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(command.ToList(1)));
    }

    [Fact]
    public async Task WriteJsonAsync_WithPositionalParams_ShouldMatchOrdinaryExecution()
    {
        var command = _ctx.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int>(0))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        await command.WriteJsonAsync(stream, new JsonStreamOptions(), TestContext.Current.CancellationToken, 1);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(command.ToList(1)));
    }

    [Fact]
    public async Task EntityBuilder_WriteJson_WithPositionalParams_ShouldMatchSync()
    {
        var builder = _ctx.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int>(0))
            .OrderBy(x => x.Id);
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        builder.WriteJson(sync, new JsonStreamOptions(), CancellationToken.None, 1);
        await builder.WriteJsonAsync(async, new JsonStreamOptions(), TestContext.Current.CancellationToken, 1);

        sync.Length.Should().BeGreaterThan(0);
        async.ToArray().Should().Equal(sync.ToArray());
    }

    // Multiple values: the predicate compares two positional values; the literal gives an independent
    // expected document, and swapping the values must invert the set (proving declaration-order binding).
    [Fact]
    public void WriteJson_MultiplePositionalParams_ShouldBindPositionally()
    {
        var command = _ctx.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int>(0) && x.Id <= SqlFunctions.Parameter<int>(1))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions(), CancellationToken.None, 0, 2);

        Utf8(stream).Should().Be("[{\"Id\":1,\"Name\":\"alpha\"},{\"Id\":2,\"Name\":null}]");

        using var swapped = new MemoryStream();
        command.WriteJson(swapped, new JsonStreamOptions(), CancellationToken.None, 2, 0);
        Utf8(swapped).Should().Be("[]");
    }

    [Fact]
    public async Task WriteJsonAsync_MultiplePositionalParams_ShouldBindPositionally()
    {
        var command = _ctx.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int>(0) && x.Id <= SqlFunctions.Parameter<int>(1))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        await command.WriteJsonAsync(stream, new JsonStreamOptions(), TestContext.Current.CancellationToken, 0, 2);

        Utf8(stream).Should().Be("[{\"Id\":1,\"Name\":\"alpha\"},{\"Id\":2,\"Name\":null}]");

        using var swapped = new MemoryStream();
        await command.WriteJsonAsync(swapped, new JsonStreamOptions(), TestContext.Current.CancellationToken, 2, 0);
        Utf8(swapped).Should().Be("[]");
    }

    [Fact]
    public async Task EntityBuilder_WriteJson_MultiplePositionalParams_ShouldMatchSync()
    {
        var builder = _ctx.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int>(0) && x.Id <= SqlFunctions.Parameter<int>(1))
            .OrderBy(x => x.Id);
        using var sync = new MemoryStream();
        using var async = new MemoryStream();
        using var oracle = new MemoryStream();

        builder.WriteJson(sync, new JsonStreamOptions(), CancellationToken.None, 0, 2);
        await builder.WriteJsonAsync(async, new JsonStreamOptions(), TestContext.Current.CancellationToken, 0, 2);
        // Independent literal-bound oracle over the same default entity projection.
        _ctx.From<JsonEntity>().Where(x => x.Id > 0 && x.Id <= 2).OrderBy(x => x.Id)
            .WriteJson(oracle, new JsonStreamOptions());

        async.ToArray().Should().Equal(sync.ToArray());
        sync.ToArray().Should().Equal(oracle.ToArray());
        sync.Length.Should().BeGreaterThan(0);
    }

    [Fact]
    public void WriteJson_EmptyParams_ShouldMatchOptionsOnlyCall()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        using var withParams = new MemoryStream();
        using var without = new MemoryStream();

        command.WriteJson(withParams, new JsonStreamOptions(), CancellationToken.None);
        command.WriteJson(without, new JsonStreamOptions());

        withParams.ToArray().Should().Equal(without.ToArray());
    }

    [Fact]
    public async Task WriteJsonAsync_EmptyParams_ShouldMatchOptionsOnlyCall()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        using var withParams = new MemoryStream();
        using var without = new MemoryStream();

        // The explicit empty array forces the params overload (a 3-argument call would pick the
        // non-params overload).
        await command.WriteJsonAsync(withParams, new JsonStreamOptions(), TestContext.Current.CancellationToken, Array.Empty<object?>());
        await command.WriteJsonAsync(without, new JsonStreamOptions(), TestContext.Current.CancellationToken);

        withParams.ToArray().Should().Equal(without.ToArray());
    }

    [Fact]
    public async Task EntityBuilder_WriteJson_EmptyParams_ShouldMatchOptionsOnlyCall()
    {
        var builder = _ctx.From<JsonEntity>().OrderBy(x => x.Id);
        using var sync = new MemoryStream();
        using var async = new MemoryStream();
        using var baseline = new MemoryStream();

        builder.WriteJson(sync, new JsonStreamOptions(), CancellationToken.None, ReadOnlySpan<object?>.Empty);
        await builder.WriteJsonAsync(async, new JsonStreamOptions(), TestContext.Current.CancellationToken, Array.Empty<object?>());
        _ctx.From<JsonEntity>().OrderBy(x => x.Id).WriteJson(baseline, new JsonStreamOptions());

        async.ToArray().Should().Equal(sync.ToArray());
        sync.ToArray().Should().Equal(baseline.ToArray());
    }

    // W1 (r2/n2): an independent oracle for null positional binding. The former test compared the
    // streaming output against ToList on the same command, so both used the same binding and proved
    // nothing. Here the produced DbParameter value is asserted directly (SQL NULL is DBNull), together
    // with the SQL NULL semantics: a null on a comparison yields no rows while a concrete value does.
    [Fact]
    public void WriteJson_NullParamElement_ShouldBindDbNull()
    {
        var interceptor = new RecordingParameterInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        var command = context.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int?>(0))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions(), CancellationToken.None, (object?)null);

        interceptor.Executions.Should().ContainSingle();
        var bound = interceptor.Executions[0].Parameters.Should().ContainSingle().Subject;
        bound.Value.Should().Be(DBNull.Value, "a null positional element must bind as SQL NULL");
        Utf8(stream).Should().Be("[]", "`id > NULL` matches no row (a concrete value would match)");

        using var withValue = new MemoryStream();
        command.WriteJson(withValue, new JsonStreamOptions(), CancellationToken.None, 1);
        Utf8(withValue).Should().Be("[{\"Id\":2,\"Name\":null},{\"Id\":3,\"Name\":\"gamma\"}]");
    }

    [Fact]
    public async Task WriteJsonAsync_NullParamElement_ShouldBindDbNull()
    {
        var interceptor = new RecordingParameterInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        var command = context.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int?>(0))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });
        using var stream = new MemoryStream();

        await command.WriteJsonAsync(stream, new JsonStreamOptions(), TestContext.Current.CancellationToken, (object?)null);

        interceptor.Executions.Should().ContainSingle();
        var bound = interceptor.Executions[0].Parameters.Should().ContainSingle().Subject;
        bound.Value.Should().Be(DBNull.Value);
        Utf8(stream).Should().Be("[]");
    }

    // The fresh/created parameter branch is exercised by a positional value the plan did not pre-create
    // (an ordinary query with no SqlFunctions.Parameter placeholder); the value must still bind as SQL
    // NULL, not CLR null. The reused/indexed branch is exercised when the placeholder pre-exists.
    [Fact]
    public void WriteJson_FreshNullParamIndex_ShouldBindDbNull()
    {
        var interceptor = new RecordingParameterInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        // No norm_p* placeholder in the SQL, so GetDbCommandCore creates the parameter on the fresh branch.
        var command = context.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        using var stream = new MemoryStream();

        command.WriteJson(stream, new JsonStreamOptions(), CancellationToken.None, (object?)null);

        interceptor.Executions.Should().ContainSingle();
        var bound = interceptor.Executions[0].Parameters.Should().ContainSingle().Subject;
        bound.Value.Should().Be(DBNull.Value);
        Utf8(stream).Should().Be("[1,2,3]");
    }

    [Fact]
    public async Task EntityBuilder_WriteJson_NullParamElement_ShouldBindDbNull()
    {
        var builder = _ctx.From<JsonEntity>()
            .Where(x => x.Id > SqlFunctions.Parameter<int?>(0))
            .OrderBy(x => x.Id);
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        builder.WriteJson(sync, new JsonStreamOptions(), CancellationToken.None, (object?)null);
        await builder.WriteJsonAsync(async, new JsonStreamOptions(), TestContext.Current.CancellationToken, (object?)null);

        Utf8(sync).Should().Be("[]");
        async.ToArray().Should().Equal(sync.ToArray());

        using var withValue = new MemoryStream();
        builder.WriteJson(withValue, new JsonStreamOptions(), CancellationToken.None, 1);
        Utf8(withValue).Should().NotBe("[]", "a concrete value must match rows, so null is not being bound as 0");
    }

    // E180-17 compatibility: `(stream, null)` still binds the existing (Stream, JsonStreamOptions)
    // overload (the new params overload requires a CancellationToken, so it is not applicable), and the
    // existing overload's null-check proves the resolution.
    [Fact]
    public void WriteJson_NullOptions_ShouldResolveExistingOptionsOverload()
    {
        var command = _ctx.From<JsonEntity>().Select(x => x.Id);
        using var stream = new MemoryStream();

        var act = () => command.WriteJson(stream, null!);

        act.Should().Throw<ArgumentNullException>();
        stream.Length.Should().Be(0);
    }

    [Fact]
    public async Task WriteJsonAsync_OptionsAndToken_ShouldResolveExistingOverload()
    {
        var command = _ctx.From<JsonEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        using var stream = new MemoryStream();

        // Three arguments select the existing non-params overload in normal form over the new expanded
        // params overload; the produced document is the same either way, so this is a resolution guard.
        await command.WriteJsonAsync(stream, new JsonStreamOptions(), TestContext.Current.CancellationToken);

        Utf8(stream).Should().Be(JsonSerializer.Serialize(command.ToList()));
    }

    [Fact]
    public async Task EntityItemWithScalarSlot_Async_ShouldMatchSync()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .Join(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId)
            .Select(p => new { p.Item1, ChildName = p.Item2.Name });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public async Task ScalarScalarSlots_Async_ShouldMatchSync()
    {
        var command = _ctx.From<JsonParentEntity>().OrderBy(p => p.Id)
            .Join(_ctx.From<JsonChildEntity>(), (p, c) => p.Id == c.ParentId)
            .Select(p => new { ParentName = p.Item1.Name, ChildName = p.Item2.Name });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public async Task ConditionalConstruction_RootAsync_ShouldMatchSync()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
            .Select(x => x.Name == null ? null : new { x.Name });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public void DisposedContext_ShouldThrowBeforeOutput()
    {
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.Dispose();
        var command = context.From<JsonEntity>().Select(x => x.Id);
        using var stream = new MemoryStream();

        var act = () => command.WriteJson(stream);

        act.Should().Throw<ObjectDisposedException>();
    }

    // W4: the capture trigger classifies a member via the unwrapped conversion type while the shape build
    // classifies from the declared member type. An interface-typed (upcast) member must not silently
    // flatten or produce a mismatched shape. Reproducer outcome: ordinary materialization already rejects
    // the same projection as an unsupported member type, so JSON is not a divergent path here — it must
    // fail closed before any output, uniformly with the ordinary materializer.
    [Fact]
    public void InterfaceTypedUpcastMember_ShouldFailClosedBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
            .Select(x => new { x.Id, Upcast = (IJsonEntity)x });

        // Ordinary materialization rejects the interface-typed member type; the JSON path must not accept
        // it more permissively (the W4 divergence would be a JSON-only success or a wrong shape).
        var listAct = () => command.ToList();
        listAct.Should().Throw<NotSupportedException>();

        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    // T1 / §10 row 5: an object produced by a factory/method call (not new/MemberInit/Conditional) is not
    // a JSON construction. It must be rejected before writing instead of flattening to a scalar.
    [Fact]
    public void OpaqueFactoryProducedObject_ShouldFailBeforeOutput()
    {
        var command = _ctx.From<JsonEntity>().Where(x => x.Id == 1)
            .Select(x => new { x.Id, Child = MakeInner(x.Name) });

        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
    }

    // T2 / §10 row 20: the managed JSON path must never route through a database-side JSON generator.
    // The executed guard inspects the actual SQL emitted by WriteJson.
    [Fact]
    public void DbSideJsonRoute_ShouldNotUseForJson()
    {
        var interceptor = new RecordingSqlInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        var command = context.From<JsonEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, Child = new { x.Name } });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        interceptor.Sql.Should().NotBeEmpty("WriteJson must execute a real query");
        var sql = interceptor.Sql[^1].ToLowerInvariant();
        sql.Should().NotContain("json_agg").And.NotContain("json_object").And.NotContain("for json").And.NotContain("jsonb_build");
    }

    private static JsonNamedInner? MakeInner(string? name)
        => name is null ? null : new JsonNamedInner { Name = name };

    // D178 direct-writer coverage: a hand-written record exercises the enum path without a database,
    // including every underlying width and the null/attribute branches. The end-to-end SQLite case
    // proves the same behavior through the real streaming terminal.

    public enum JsonEnumSigned : short
    {
        Neg = -2,
        Pos = 5,
    }

    public enum JsonEnumWide : ulong
    {
        Zero = 0,
        Max = ulong.MaxValue,
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum JsonStringEnumType
    {
        Unknown = 0,
        Active = 7,
    }

    public sealed class JsonStringEnumHolder
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JsonStringEnumType Status { get; set; }
    }

    public enum JsonCustomEnum
    {
        A = 1,
    }

    public sealed class JsonCustomEnumConverter : JsonConverter<JsonCustomEnum>
    {
        public override JsonCustomEnum Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => throw new NotSupportedException();

        public override void Write(Utf8JsonWriter writer, JsonCustomEnum value, JsonSerializerOptions options)
            => throw new NotSupportedException();
    }

    public sealed class JsonCustomEnumHolder
    {
        [JsonConverter(typeof(JsonCustomEnumConverter))]
        public JsonCustomEnum Status { get; set; }
    }

    public enum JsonEnumSByte : sbyte
    {
        Min = sbyte.MinValue,
        Max = sbyte.MaxValue,
    }

    public enum JsonEnumByte : byte
    {
        Zero = 0,
        Max = byte.MaxValue,
    }

    public enum JsonEnumUShort : ushort
    {
        Max = ushort.MaxValue,
    }

    public enum JsonEnumUInt : uint
    {
        Max = uint.MaxValue,
    }

    public enum JsonEnumLong : long
    {
        Min = long.MinValue,
        Max = long.MaxValue,
    }

    [Flags]
    public enum JsonFlagsState
    {
        None = 0,
        A = 1,
        B = 2,
        C = 4,
    }

    [Flags]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum JsonStringFlags
    {
        None = 0,
        A = 1,
        B = 2,
        C = 4,
    }

    // T05: the generic stock STJ form, both type-level and member-level, exercising the
    // JsonStringEnumConverter<> / Activator.CreateInstance branch of TryCreateStringEnumConverter.
    [JsonConverter(typeof(JsonStringEnumConverter<JsonGenericStringEnumType>))]
    public enum JsonGenericStringEnumType
    {
        Unknown = 0,
        Active = 7,
    }

    public sealed class JsonGenericStringEnumHolder
    {
        [JsonConverter(typeof(JsonStringEnumConverter<JsonGenericStringEnumType>))]
        public JsonGenericStringEnumType Status { get; set; }
    }

    public sealed class JsonGenericMismatchHolder
    {
        // The generic argument does not match the member's enum type: must be rejected, never used.
        [JsonConverter(typeof(JsonStringEnumConverter<JsonGenericStringEnumType>))]
        public JsonEnumState Status { get; set; }
    }

    // Precedence: the enum type carries the supported string attribute, but the member overrides it
    // with an unsupported custom converter, so the member attribute must win and fail fast.
    public sealed class JsonMemberOverridesTypeWithUnsupportedAttributeHolder
    {
        [JsonConverter(typeof(JsonCustomEnumConverter))]
        public JsonStringEnumType Status { get; set; }
    }

    // Precedence in the other direction: an unsupported enum-type attribute must not win over the
    // member's supported string converter.
    [JsonConverter(typeof(JsonCustomEnumConverter))]
    public enum JsonUnsupportedTypeAttributeEnum
    {
        Active = 7,
    }

    public sealed class JsonMemberStringOverUnsupportedTypeAttributeHolder
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JsonUnsupportedTypeAttributeEnum Status { get; set; }
    }

    // A member-only string attribute (no enum-type attribute) so a projection that carries no
    // SelectExpression.PropertyInfo must resolve the source member through the expression lambda.
    public enum JsonMemberOnlyStringEnum
    {
        Unknown = 0,
        Active = 7,
        Disabled = -3,
    }

    public sealed class JsonMemberOnlyStringEnumHolder
    {
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JsonMemberOnlyStringEnum Status { get; set; }
    }

    [SqlTable("json_enum")]
    public interface IJsonMemberOnlyStringEnumEntity
    {
        [Key]
        [Column("id")]
        int Id { get; set; }

        [JsonConverter(typeof(JsonStringEnumConverter))]
        [Column("state")]
        JsonMemberOnlyStringEnum State { get; set; }
    }

    public class JsonMemberOnlyStringEnumEntity : IJsonMemberOnlyStringEnumEntity
    {
        public int Id { get; set; }

        // The projection lambda binds the class property, so the attribute must live here to be
        // reachable through the expression-based provenance walk.
        [JsonConverter(typeof(JsonStringEnumConverter))]
        public JsonMemberOnlyStringEnum State { get; set; }
    }

    // A JSON converter attribute on a non-enum scalar must be rejected before output, not silently
    // ignored.
    public sealed class JsonNonEnumStringConverter : JsonConverter<string>
    {
        public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            => reader.GetString()!;

        public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
            => writer.WriteStringValue(value);
    }

    public sealed class JsonNonEnumConverterHolder
    {
        [JsonConverter(typeof(JsonNonEnumStringConverter))]
        public string? Name { get; set; }
    }

    [JsonConverter(typeof(JsonNonEnumStringConverter))]
    public sealed class JsonNonEnumTypeAttributeHolder
    {
        public string? Name { get; set; }
    }

    private sealed class EnumScalarRecord : IDataRecord
    {
        private readonly object _value;
        private readonly Type _fieldType;
        private readonly bool _isNull;

        public EnumScalarRecord(object value, Type fieldType, bool isNull)
        {
            _value = value;
            _fieldType = fieldType;
            _isNull = isNull;
        }

        public int FieldCount => 1;
        public object this[int i] => GetValue(i);
        public object this[string name] => throw new NotSupportedException();
        public bool GetBoolean(int i) => throw new NotSupportedException();
        public byte GetByte(int i) => (byte)_value;
        public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public char GetChar(int i) => throw new NotSupportedException();
        public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
        public IDataReader GetData(int i) => throw new NotSupportedException();
        public string GetDataTypeName(int i) => _fieldType.Name;
        public DateTime GetDateTime(int i) => throw new NotSupportedException();
        public decimal GetDecimal(int i) => throw new NotSupportedException();
        public double GetDouble(int i) => throw new NotSupportedException();
        public Type GetFieldType(int i) => _fieldType;
        public float GetFloat(int i) => throw new NotSupportedException();
        public Guid GetGuid(int i) => throw new NotSupportedException();
        public short GetInt16(int i) => (short)_value;
        public int GetInt32(int i) => (int)_value;
        public long GetInt64(int i) => (long)_value;
        public string GetName(int i) => "Status";
        public int GetOrdinal(string name) => 0;
        public string GetString(int i) => (string)_value;
        public object GetValue(int i) => _value;
        public int GetValues(object[] values)
        {
            values[0] = _value;
            return 1;
        }

        public bool IsDBNull(int i) => _isNull;
    }

    private static string WriteEnumDirect(Type declaredType, Type fieldType, object value, System.Reflection.PropertyInfo? member = null, bool isNull = false)
    {
        var plan = JsonShapePlan.Build(
            [new SelectExpression(declaredType) { Index = 0, PropertyName = "Status", PropertyInfo = member }],
            oneColumn: true,
            new JsonStreamOptions());
        var writeRow = JsonRowWriterFactory.Build(plan);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writeRow(new EnumScalarRecord(value, fieldType, isNull), writer);
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }

    [Fact]
    public void Enum_NumericScalar_ShouldWriteUnderlyingNumber()
    {
        WriteEnumDirect(typeof(JsonEnumState), typeof(int), (int)JsonEnumState.Active).Should().Be("7");
    }

    [Fact]
    public void Enum_SignedNarrowUnderlying_ShouldWidenAndWriteNumber()
    {
        WriteEnumDirect(typeof(JsonEnumSigned), typeof(short), (short)JsonEnumSigned.Neg).Should().Be("-2");
    }

    [Fact]
    public void Enum_UnsignedWideUnderlying_ShouldNotNarrow()
    {
        WriteEnumDirect(typeof(JsonEnumWide), typeof(ulong), ulong.MaxValue).Should().Be("18446744073709551615");
    }

    [Fact]
    public void Enum_NullableNull_ShouldWriteNull()
    {
        WriteEnumDirect(typeof(JsonEnumState?), typeof(int), 0, isNull: true).Should().Be("null");
    }

    [Fact]
    public void Enum_TypeStringAttribute_ShouldWriteName()
    {
        WriteEnumDirect(typeof(JsonStringEnumType), typeof(int), (int)JsonStringEnumType.Active).Should().Be("\"Active\"");
    }

    [Fact]
    public void Enum_PropertyStringAttribute_ShouldWriteName()
    {
        var member = typeof(JsonStringEnumHolder).GetProperty(nameof(JsonStringEnumHolder.Status));
        WriteEnumDirect(typeof(JsonStringEnumType), typeof(int), (int)JsonStringEnumType.Active, member).Should().Be("\"Active\"");
    }

    [Fact]
    public void Enum_UnsupportedAttribute_ShouldThrowBeforeOutput()
    {
        var member = typeof(JsonCustomEnumHolder).GetProperty(nameof(JsonCustomEnumHolder.Status));
        var act = () => JsonShapePlan.Build(
            [new SelectExpression(typeof(JsonCustomEnum)) { Index = 0, PropertyName = "Status", PropertyInfo = member }],
            oneColumn: true,
            new JsonStreamOptions());

        act.Should().Throw<NotSupportedException>().WithMessage("*converter*");
    }

    [Fact]
    public void Enum_SqliteEndpoint_ShouldWriteNumbers()
    {
        var command = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.State, x.NullableState });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be("[{\"Id\":1,\"State\":7,\"NullableState\":null},{\"Id\":2,\"State\":-3,\"NullableState\":7}]");
    }

    // D178.4 variant matrix: underlying widths (T01), undefined/flags (T02), nullable/default (T03),
    // converter edges (T05/T06), storage-converter/attribute rejection (T07/T08), provider field
    // type (T09), both terminals (T10), empty shape (T13), options/modes (T14), cache (T17).

    [Fact]
    public void Enum_AllUnderlyingWidths_ShouldWriteExactNumbers()
    {
        WriteEnumDirect(typeof(JsonEnumSByte), typeof(sbyte), sbyte.MinValue).Should().Be("-128");
        WriteEnumDirect(typeof(JsonEnumByte), typeof(byte), byte.MaxValue).Should().Be("255");
        WriteEnumDirect(typeof(JsonEnumSigned), typeof(short), short.MinValue).Should().Be("-32768");
        WriteEnumDirect(typeof(JsonEnumUShort), typeof(ushort), ushort.MaxValue).Should().Be("65535");
        WriteEnumDirect(typeof(JsonEnumState), typeof(int), int.MinValue).Should().Be("-2147483648");
        WriteEnumDirect(typeof(JsonEnumUInt), typeof(uint), uint.MaxValue).Should().Be("4294967295");
        WriteEnumDirect(typeof(JsonEnumLong), typeof(long), long.MaxValue).Should().Be("9223372036854775807");
        WriteEnumDirect(typeof(JsonEnumWide), typeof(ulong), ulong.MaxValue).Should().Be("18446744073709551615");
    }

    [Fact]
    public void Enum_UndefinedAndFlags_ShouldStayNumbers()
    {
        WriteEnumDirect(typeof(JsonEnumState), typeof(int), 42).Should().Be("42");
        WriteEnumDirect(typeof(JsonFlagsState), typeof(int), (int)(JsonFlagsState.A | JsonFlagsState.C)).Should().Be("5");
    }

    [Fact]
    public void Enum_DefaultOnNull_ShouldWriteUnderlyingDefault()
    {
        // A non-nullable *OrDefault scalar: SQL NULL means "no row" and must become default(enum) == 0,
        // not a JSON null and not a wrong enum name.
        var column = new SelectExpression(typeof(JsonEnumState)) { Index = 0, PropertyName = "State", DefaultOnNull = true };
        var plan = JsonShapePlan.Build([column], oneColumn: true, new JsonStreamOptions());
        var writeRow = JsonRowWriterFactory.Build(plan);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writeRow(new EnumScalarRecord(0, typeof(int), isNull: true), writer);
        }

        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("0");
    }

    [Fact]
    public void Enum_StringFlagsAndUnnamed_ShouldMatchStjConverter()
    {
        var flags = JsonStringFlags.A | JsonStringFlags.C;
        WriteEnumDirect(typeof(JsonStringFlags), typeof(int), (int)flags).Should().Be(JsonSerializer.Serialize(flags));

        var unnamed = (JsonStringEnumType)99;
        WriteEnumDirect(typeof(JsonStringEnumType), typeof(int), 99).Should().Be(JsonSerializer.Serialize(unnamed));
    }

    [Fact]
    public void Enum_WithStorageConverter_ShouldThrowBeforeOutput()
    {
        var column = new SelectExpression(typeof(JsonEnumState))
        {
            Index = 0,
            PropertyName = "State",
            Converter = new EnumToStringConverter<JsonEnumState>(),
        };

        var act = () => JsonShapePlan.Build([column], oneColumn: true, new JsonStreamOptions());

        act.Should().Throw<NotSupportedException>().WithMessage("*value-converted*");
    }

    [Fact]
    public void Enum_UnsupportedProviderFieldType_ShouldThrowOnWrite()
    {
        var plan = JsonShapePlan.Build(
            [new SelectExpression(typeof(JsonEnumState)) { Index = 0, PropertyName = "State" }],
            oneColumn: true,
            new JsonStreamOptions());
        var writeRow = JsonRowWriterFactory.Build(plan);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        var act = () => writeRow(new EnumScalarRecord("nope", typeof(string), isNull: false), writer);

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task Enum_SyncAsync_ShouldProduceIdenticalBytes()
    {
        var command = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.State, x.NullableState });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public void Enum_OptionsAndModes_ShouldBeRespected()
    {
        var command = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.State });

        using var root = new MemoryStream();
        command.WriteJson(root, new JsonStreamOptions { Root = "states" });
        Utf8(root).Should().Be("{\"states\":[{\"State\":7},{\"State\":-3}]}");

        using var nd = new MemoryStream();
        command.WriteJson(nd, new JsonStreamOptions { Mode = JsonStreamMode.NdJson });
        Utf8(nd).Should().Be("{\"State\":7}\n{\"State\":-3}\n");
    }

    [Fact]
    public void Enum_EmptyResult_ShouldWriteEmptyArray()
    {
        var command = _ctx.From<JsonEnumEntity>().Where(x => x.Id > 100).Select(x => new { x.State });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be("[]");
    }

    [Fact]
    public void Enum_WriteJson_ShouldNotDisablePlanCache()
    {
        var command = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.State });

        using var first = new MemoryStream();
        command.WriteJson(first);

        command.Cache.Should().BeTrue("the enum JSON clone must not set the sticky QueryCommand.Cache=false flag");

        // A second write on the same context/shape stays byte-identical (no leaked writer state).
        using var second = new MemoryStream();
        command.WriteJson(second);
        second.ToArray().Should().Equal(first.ToArray());
    }

    // CHECK loop-back (n=2): T05 generic converter, provenance/precedence, T09 out-of-range,
    // T10 raw/EntityBuilder surfaces, T11 ownership/cancellation, T17 SQL/parameter parity,
    // nullable/DefaultOnNull string converter, and the non-enum converter rejection.

    [Fact]
    public void Enum_GenericTypeStringAttribute_ShouldWriteName()
    {
        // JsonStringEnumConverter<TEnum> declared on the enum type: TryCreateStringEnumConverter must
        // build the concrete converter via Activator.CreateInstance and write the STJ name.
        WriteEnumDirect(typeof(JsonGenericStringEnumType), typeof(int), (int)JsonGenericStringEnumType.Active)
            .Should().Be("\"Active\"");
    }

    [Fact]
    public void Enum_GenericPropertyStringAttribute_ShouldWriteName()
    {
        var member = typeof(JsonGenericStringEnumHolder).GetProperty(nameof(JsonGenericStringEnumHolder.Status));
        WriteEnumDirect(typeof(JsonGenericStringEnumType), typeof(int), (int)JsonGenericStringEnumType.Active, member)
            .Should().Be("\"Active\"");
    }

    [Fact]
    public void Enum_GenericAttributeWrongEnumArgument_ShouldThrowBeforeOutput()
    {
        // JsonStringEnumConverter<OtherEnum> on a JsonEnumState member: the generic argument does not
        // match, so the attribute is unsupported and must be rejected (never used or ignored).
        var member = typeof(JsonGenericMismatchHolder).GetProperty(nameof(JsonGenericMismatchHolder.Status));
        var act = () => JsonShapePlan.Build(
            [new SelectExpression(typeof(JsonEnumState)) { Index = 0, PropertyName = "Status", PropertyInfo = member }],
            oneColumn: true,
            new JsonStreamOptions());

        act.Should().Throw<NotSupportedException>().WithMessage("*converter*");
    }

    [Fact]
    public void Enum_MemberAttributeResolvedFromLambdaExpression_ShouldWriteName()
    {
        // No PropertyInfo is supplied: the provenance must be resolved from the projection expression's
        // lambda body (LambdaExpression -> MemberExpression). The enum type has no type-level attribute,
        // so a numeric write would prove the member attribute was missed.
        Expression<Func<JsonMemberOnlyStringEnumHolder, JsonMemberOnlyStringEnum>> lambda = x => x.Status;
        var column = new SelectExpression(typeof(JsonMemberOnlyStringEnum))
        {
            Index = 0,
            PropertyName = "Status",
            PropertyInfo = null,
            Expression = lambda,
        };
        var plan = JsonShapePlan.Build([column], oneColumn: true, new JsonStreamOptions());
        var writeRow = JsonRowWriterFactory.Build(plan);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writeRow(new EnumScalarRecord((int)JsonMemberOnlyStringEnum.Active, typeof(int), isNull: false), writer);
        }

        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("\"Active\"");
    }

    [Fact]
    public void Enum_MemberAttributeOverAnonymousProjection_ShouldWriteName()
    {
        // The ordinary preparer does not set PropertyInfo for an anonymous member projection, so this
        // end-to-end case walks the real lambda-based provenance over SQLite.
        var command = _ctx.From<JsonMemberOnlyStringEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.State });
        using var stream = new MemoryStream();

        command.WriteJson(stream);

        Utf8(stream).Should().Be("[{\"State\":\"Active\"},{\"State\":\"Disabled\"}]");
    }

    [Fact]
    public void Enum_MemberAttributeWinsOverEnumTypeAttribute()
    {
        // The enum type carries the supported string attribute; the member overrides it with an
        // unsupported custom converter. If the type attribute were used the write would succeed with
        // "Active"; the expected NotSupportedException proves member precedence.
        var member = typeof(JsonMemberOverridesTypeWithUnsupportedAttributeHolder)
            .GetProperty(nameof(JsonMemberOverridesTypeWithUnsupportedAttributeHolder.Status));
        var act = () => JsonShapePlan.Build(
            [new SelectExpression(typeof(JsonStringEnumType)) { Index = 0, PropertyName = "Status", PropertyInfo = member }],
            oneColumn: true,
            new JsonStreamOptions());

        act.Should().Throw<NotSupportedException>().WithMessage("*converter*");
    }

    [Fact]
    public void Enum_MemberAttributeWinsOverUnsupportedTypeAttribute()
    {
        // The enum type's own attribute is unsupported; the member's stock string converter must win.
        var member = typeof(JsonMemberStringOverUnsupportedTypeAttributeHolder)
            .GetProperty(nameof(JsonMemberStringOverUnsupportedTypeAttributeHolder.Status));
        WriteEnumDirect(typeof(JsonUnsupportedTypeAttributeEnum), typeof(int), 7, member).Should().Be("\"Active\"");
    }

    [Fact]
    public void Enum_NumericOutOfRange_ShouldThrowNotTruncate()
    {
        // The enum's underlying type is byte but the provider field is a wider int: an out-of-range
        // value must throw from the checked Convert, never silently truncate 300 -> 44.
        var act = () => WriteEnumDirect(typeof(JsonEnumByte), typeof(int), 300);
        act.Should().Throw<OverflowException>();

        // The in-range boundary still writes the exact value (no narrowing of a representable value).
        WriteEnumDirect(typeof(JsonEnumByte), typeof(int), 255).Should().Be("255");
    }

    [Fact]
    public async Task Enum_EntityBuilderSurface_ShouldMatchSerializer_SyncAndAsync()
    {
        // The whole-entity fluent EntityBuilder surface (no Select), streaming the mapped enum members.
        var builder = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id);
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        builder.WriteJson(sync);
        await builder.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        // The ordinary materializer cannot map an enum member (pre-existing buffered-mapper gap outside
        // this footprint), so the whole-entity oracle is the literal STJ-shaped document.
        Utf8(sync).Should().Be("[{\"Id\":1,\"State\":7,\"NullableState\":null},{\"Id\":2,\"State\":-3,\"NullableState\":7}]");
        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public async Task Enum_FromSqlSurface_ShouldMatchSyncAsyncAndLiteral()
    {
        // The raw FromSql (BindEntity) command surface over the same enum column.
        var command = _ctx.FromSql("select id, state, nullable_state from json_enum order by id")
            .BindEntity<JsonEnumEntity>(["id", "state", "nullable_state"])
            .Select(x => new { x.Id, x.State });
        using var sync = new MemoryStream();
        using var async = new MemoryStream();

        command.WriteJson(sync);
        await command.WriteJsonAsync(async, TestContext.Current.CancellationToken);

        Utf8(sync).Should().Be("[{\"Id\":1,\"State\":7},{\"Id\":2,\"State\":-3}]");
        async.ToArray().Should().Equal(sync.ToArray());
    }

    [Fact]
    public void Enum_PartialOutput_OnMidWriteFailure_ShouldKeepOwnership()
    {
        var command = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.State });
        using var stream = new ThrowingWriteStream();

        var act = () => command.WriteJson(stream);

        act.Should().Throw<InvalidOperationException>().WithMessage("destination failed");
        // The destination stays caller-owned and the reader/connection were released: the shared open
        // connection is still usable and the context can immediately execute another query, twice, with
        // no leaked reader accumulating on the connection.
        stream.CanWrite.Should().BeTrue();
        _fixture.Connection.State.Should().Be(ConnectionState.Open);
        _ctx.From<JsonEnumEntity>().Select(x => x.Id).ToList().Should().HaveCount(2);
        _ctx.From<JsonEnumEntity>().Select(x => x.Id).ToList().Should().HaveCount(2);
    }

    [Fact]
    public async Task Enum_Cancellation_DuringWrite_ShouldAbortAndKeepOwnership()
    {
        var command = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.State });
        using var cts = new CancellationTokenSource();
        using var stream = new CancellingWriteStream(cts);

        var act = () => command.WriteJsonAsync(stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.CanWrite.Should().BeTrue();
        _fixture.Connection.State.Should().Be(ConnectionState.Open);
        _ctx.From<JsonEnumEntity>().Select(x => x.Id).ToList().Should().HaveCount(2);
        _ctx.From<JsonEnumEntity>().Select(x => x.Id).ToList().Should().HaveCount(2);
    }

    // T11 pre-cancellation on the enum path: a token already cancelled before enumeration must abort
    // before any output and leave the caller-owned destination and the shared reader/connection intact.
    // The sync WriteJson surface has no CancellationToken parameter, so the token-observing async
    // terminal is the only surface that can express pre-cancellation here (no separate sync twin).
    [Fact]
    public async Task Enum_PreCancelled_ShouldThrowAndKeepOwnership()
    {
        var command = _ctx.From<JsonEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.State });
        using var stream = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = () => command.WriteJsonAsync(stream, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        stream.Length.Should().Be(0, "a pre-cancelled token must abort before any byte reaches the destination");
        stream.CanWrite.Should().BeTrue();
        var write = () => stream.WriteByte(1);
        write.Should().NotThrow();

        // Reader/connection released: the shared open connection is still usable and the same context
        // immediately re-executes successfully (twice) with no leaked reader left behind.
        _fixture.Connection.State.Should().Be(ConnectionState.Open);
        _ctx.From<JsonEnumEntity>().Select(x => x.Id).ToList().Should().HaveCount(2);
        _ctx.From<JsonEnumEntity>().Select(x => x.Id).ToList().Should().HaveCount(2);
    }

    // T13 empty-result + invalid attributed shape: an unsupported [JsonConverter] on an enum member must
    // still be rejected before any output even when the mapped table has zero rows, proving the shape is
    // validated at prepare time rather than inferred from the (empty) result set.
    [Fact]
    public void Enum_InvalidAttributedShape_EmptyResult_ShouldThrowBeforeOutput()
    {
        var command = _ctx.From<JsonEmptyEnumEntity>().Select(x => new { x.State });
        using var stream = new MemoryStream();

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>().WithMessage("*converter*");
        stream.Length.Should().Be(0, "the unsupported shape must be rejected before any output");
    }

    [Fact]
    public void Enum_WriteJson_ParametersAndSqlShouldMatchOrdinaryExecution()
    {
        // A context local to this test so registering the interceptor cannot leak into the shared fixture.
        var interceptor = new RecordingParameterInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        // An enum-valued parameter must reach the provider identically for ordinary and JSON execution.
        var state = JsonEnumState.Active;
        var command = context.From<JsonEnumEntity>().Where(x => x.State == state).Select(x => new { x.Id });

        command.ToList();
        var listExecution = interceptor.Executions[^1];

        using var stream = new MemoryStream();
        command.WriteJson(stream);
        var jsonExecution = interceptor.Executions[^1];

        jsonExecution.Sql.Should().Be(listExecution.Sql);
        jsonExecution.Parameters.Should().Equal(listExecution.Parameters);
        command.Cache.Should().BeTrue("streaming must not set the sticky QueryCommand.Cache=false flag");
    }

    [Fact]
    public void Enum_WriteJson_EnumProjection_ShouldNotMutateSharedCommandState()
    {
        // The enum projection itself cannot be materialized by the buffered mapper (pre-existing gap),
        // so parity is asserted by repeating the JSON write: identical SQL/parameters/bytes prove the
        // shared command/plan state is not mutated by the enum streaming clone.
        var interceptor = new RecordingParameterInterceptor();
        using var context = new SqliteDataContext(_fixture.Connection, new DataContextBuilder());
        context.AddInterceptor(interceptor);
        context.PurgeQueryCache();

        var command = context.From<JsonEnumEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.State });

        using var first = new MemoryStream();
        command.WriteJson(first);
        var firstExecution = interceptor.Executions[^1];

        using var second = new MemoryStream();
        command.WriteJson(second);
        var secondExecution = interceptor.Executions[^1];

        secondExecution.Sql.Should().Be(firstExecution.Sql);
        secondExecution.Parameters.Should().Equal(firstExecution.Parameters);
        second.ToArray().Should().Equal(first.ToArray());
        command.Cache.Should().BeTrue("streaming an enum must not set the sticky QueryCommand.Cache=false flag");
    }

    [Fact]
    public void Enum_NullableStringAttribute_Null_ShouldWriteNull()
    {
        WriteEnumDirect(typeof(JsonStringEnumType?), typeof(int), 0, isNull: true).Should().Be("null");
        WriteEnumDirect(typeof(JsonStringEnumType?), typeof(int), (int)JsonStringEnumType.Active).Should().Be("\"Active\"");
    }

    [Fact]
    public void Enum_StringDefaultOnNull_ShouldWriteDefaultName()
    {
        // A non-nullable *OrDefault scalar with a string-enum attribute: SQL NULL means "no row" and
        // must become default(enum) through the converter (Unknown), not JSON null.
        var column = new SelectExpression(typeof(JsonStringEnumType))
        {
            Index = 0,
            PropertyName = "State",
            DefaultOnNull = true,
        };
        var plan = JsonShapePlan.Build([column], oneColumn: true, new JsonStreamOptions());
        var writeRow = JsonRowWriterFactory.Build(plan);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writeRow(new EnumScalarRecord(0, typeof(int), isNull: true), writer);
        }

        Encoding.UTF8.GetString(stream.ToArray()).Should().Be("\"Unknown\"");
    }

    [Fact]
    public void NonEnumMemberJsonConverterAttribute_ShouldThrowBeforeOutput()
    {
        var member = typeof(JsonNonEnumConverterHolder).GetProperty(nameof(JsonNonEnumConverterHolder.Name));
        var act = () => JsonShapePlan.Build(
            [new SelectExpression(typeof(string)) { Index = 0, PropertyName = "Name", PropertyInfo = member }],
            oneColumn: true,
            new JsonStreamOptions());

        act.Should().Throw<NotSupportedException>().WithMessage("*converter*");
    }

    [Fact]
    public void NonEnumTypeJsonConverterAttribute_ShouldThrowBeforeOutput()
    {
        var act = () => JsonShapePlan.Build(
            [new SelectExpression(typeof(JsonNonEnumTypeAttributeHolder)) { Index = 0, PropertyName = "Value" }],
            oneColumn: true,
            new JsonStreamOptions());

        act.Should().Throw<NotSupportedException>().WithMessage("*converter*");
    }
}
