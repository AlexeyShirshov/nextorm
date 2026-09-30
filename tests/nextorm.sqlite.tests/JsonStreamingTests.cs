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
                "create table unsupported_entity (id integer primary key, when_col text);";
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
}
