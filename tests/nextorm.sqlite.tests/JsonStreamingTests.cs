using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using System.Text;
using System.Text.Json;
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
                "insert into json_child (id, parent_id, name) values (10, 1, 'c1'), (11, 1, 'c2'), (12, 2, 'c3');";
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

        collisionAct.Should().Throw<NotSupportedException>();
        collision.Position.Should().Be(collisionPosition);
        collision.Length.Should().Be(collisionPosition);

        using var nullName = new MemoryStream();
        nullName.WriteByte(1);
        var nullPosition = nullName.Position;
        var nullAct = () => command.WriteJson(nullName, new JsonStreamOptions { PropertyNamingPolicy = new NullNamingPolicy() });

        nullAct.Should().Throw<NotSupportedException>();
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

        act.Should().Throw<NotSupportedException>();
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

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void NdJson_WithWriteIndented_ShouldThrow()
    {
        var command = _ctx.From<JsonEntity>().Select(x => x.Id);
        using var stream = new MemoryStream();

        var act = () => command.WriteJson(stream, new JsonStreamOptions { Mode = JsonStreamMode.NdJson, WriteIndented = true });

        act.Should().Throw<NotSupportedException>();
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

    [Fact]
    public void TempTableQuery_ShouldThrow()
    {
        var source = _ctx.From<JsonEntity>().Select(x => new { x.Id, x.Name }).AsTempTable();
        var command = _ctx.From(source).Select(t => new { Id = t.GetInt32("id"), Name = t.GetString("name") });
        using var stream = new MemoryStream();
        stream.WriteByte(1);
        var position = stream.Position;

        var act = () => command.WriteJson(stream);

        act.Should().Throw<NotSupportedException>();
        stream.Position.Should().Be(position);
        stream.Length.Should().Be(position);
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
}
