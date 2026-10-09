using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// D177 SQL Server native JSON streaming, end to end. The native path is proven observably: SQL Server
/// leaves Unicode and HTML-sensitive characters unescaped, while the managed System.Text.Json writer
/// escapes them, so raw <c>é</c>/<c>中</c>/<c>😀</c> and <c>&lt;</c> in the output can only come from the
/// database document. The tests also cover the empty-result <c>[]</c>, async/sync parity, destination
/// ownership and pre-cancellation, and the managed fallback for a non-admitted projection.
/// </summary>
[SqlTable("native_json_unicode")]
public interface INativeJsonUnicodeEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string? Name { get; set; }

    [Column("flag")]
    bool Flag { get; set; }

    [Column("num")]
    long Num { get; set; }
}

[SqlTable("native_json_surrogate")]
public interface INativeJsonSurrogateEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

public sealed class SqlServerNativeJsonStreamTests : ProviderTestSuite
{
    private static readonly object SeedGate = new();
    private static bool _seeded;
    private static bool _surrogateSeeded;

    protected override ITestProvider Provider => SqlServerTestProvider.Instance;

    private void EnsureNativeTable()
    {
        if (_seeded)
            return;

        lock (SeedGate)
        {
            if (_seeded)
                return;

            var context = (DataContext)_sut.DataProvider;
            context.EnsureConnectionOpen();
            using var command = context.CreateCommand(
                "if object_id('native_json_unicode') is null " +
                "create table native_json_unicode (id int not null primary key, name nvarchar(max) null, flag bit not null, num bigint not null); " +
                "delete from native_json_unicode; " +
                "insert into native_json_unicode (id, name, flag, num) values " +
                "(1, N'plain', 1, 10), (2, N'é中😀<b>/slash', 0, 20), (3, null, 1, 30), " +
                "(4, replicate(cast(N'x' as nvarchar(max)), 4095) + N'😀end', 0, 40);");
            command.ExecuteNonQuery();
            _seeded = true;
        }
    }

    // SQL Server returns a FOR JSON document longer than 2033 characters as multiple reader rows. The
    // 2022-'x' prefix puts the emoji's high surrogate at document index 2032, i.e. the last character of
    // the first 2033-character reader row, so the surrogate pair straddles a real reader-row boundary.
    private const int SurrogatePrefix = 2022;

    private void EnsureSurrogateTable()
    {
        if (_surrogateSeeded)
            return;

        lock (SeedGate)
        {
            if (_surrogateSeeded)
                return;

            var context = (DataContext)_sut.DataProvider;
            context.EnsureConnectionOpen();
            using var command = context.CreateCommand(
                "if object_id('native_json_surrogate') is null " +
                "create table native_json_surrogate (id int not null primary key, name nvarchar(max) null); " +
                "delete from native_json_surrogate; " +
                $"insert into native_json_surrogate (id, name) values (1, replicate(cast(N'x' as nvarchar(max)), {SurrogatePrefix}) + N'😀end');");
            command.ExecuteNonQuery();
            _surrogateSeeded = true;
        }
    }

    private byte[] StreamNative()
    {
        EnsureNativeTable();
        using var buffer = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .WriteJson(buffer);
        return buffer.ToArray();
    }

    [Fact]
    public void Native_UnicodeAndHtml_ShouldBeRawAndLogicallyEquivalent()
    {
        var text = Encoding.UTF8.GetString(StreamNative());

        // Raw characters (not System.Text.Json's \uXXXX escapes) prove the database document transport.
        text.Should().Contain("é").And.Contain("中").And.Contain("😀");
        text.Should().NotContain("\\u00e9").And.NotContain("\\u003C");

        using var document = JsonDocument.Parse(text);
        var rows = document.RootElement;
        rows.GetArrayLength().Should().Be(4);
        // Exact aliases: the physical id/name columns surface as the projected Id/Name properties.
        rows[0].GetProperty("Id").GetInt32().Should().Be(1);
        rows[1].GetProperty("Name").GetString().Should().Be("é中😀<b>/slash");
        rows[2].GetProperty("Name").ValueKind.Should().Be(JsonValueKind.Null);
        // The emoji's surrogate pair straddles the 4096-character pump buffer boundary (high surrogate
        // is the 4096th character), proving the encoder is surrogate-safe across chunks.
        rows[3].GetProperty("Name").GetString().Should().Be(new string('x', 4095) + "😀end");
    }

    [Fact]
    public void Native_EmptyResult_ShouldBeEmptyArray()
    {
        EnsureNativeTable();
        using var buffer = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id < 0)
            .Select(x => new { x.Id, x.Name })
            .WriteJson(buffer);

        Encoding.UTF8.GetString(buffer.ToArray()).Should().Be("[]");
    }

    [Fact]
    public async Task NativeAsync_ShouldMatchSyncAndObserveCancellation()
    {
        EnsureNativeTable();

        var sync = StreamNative();

        using var asyncBuffer = new MemoryStream();
        await _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .WriteJsonAsync(asyncBuffer, cancellationToken: TestContext.Current.CancellationToken);

        asyncBuffer.ToArray().Should().Equal(sync);

        using var cancelled = new MemoryStream();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .WriteJsonAsync(cancelled, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        cancelled.Length.Should().Be(0);
    }

    [Fact]
    public void Native_DestinationIsCallerOwned_ShouldNotFlushOrDispose()
    {
        EnsureNativeTable();

        using var sink = new TrackingStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .WriteJson(sink);

        sink.Length.Should().BeGreaterThan(0);
        sink.FlushCount.Should().Be(0);
        sink.Disposed.Should().BeFalse();
    }

    [Fact]
    public void NonAdmittedProjection_ShouldFallBackToManagedBytes()
    {
        // A decimal member is outside the admitted native set, so the whole request must use the managed
        // row writer and stay byte-identical to System.Text.Json.
        var expected = JsonSerializer.Serialize(
            _sut.ComplexEntity.OrderBy(x => x.Id)
                .Select(x => new { x.Id, x.Numeric })
                .ToList());

        using var buffer = new MemoryStream();
        _sut.ComplexEntity.OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Numeric })
            .WriteJson(buffer);

        buffer.ToArray().Should().Equal(Encoding.UTF8.GetBytes(expected));
    }

    [Fact]
    public void Native_SurrogateAcrossReaderRow_ShouldRoundTrip()
    {
        EnsureSurrogateTable();
        var expected = new string('x', SurrogatePrefix) + "😀end";

        // Prove the transport really splits the pair: read the raw FOR JSON rows and assert the reader-row
        // boundary falls between the high and low surrogate of the emoji.
        var context = (DataContext)_sut.DataProvider;
        context.EnsureConnectionOpen();
        using (var command = context.CreateCommand(
            "select [name] as [Name] from native_json_surrogate where id = 1 for json path"))
        using (var reader = command.ExecuteReader())
        {
            var rows = new List<string>();
            while (reader.Read())
                rows.Add(reader.GetString(0));

            rows.Count.Should().BeGreaterThan(1, "a >2033-character FOR JSON document is returned in multiple reader rows");
            var document = string.Concat(rows);
            var emoji = document.IndexOf("😀", StringComparison.Ordinal);
            emoji.Should().BeGreaterThanOrEqualTo(0);
            rows[0].Length.Should().Be(emoji + 1, "the reader-row boundary must fall inside the surrogate pair");
        }

        using var buffer = new MemoryStream();
        _sut.DataProvider.From<INativeJsonSurrogateEntity>()
            .Where(x => x.Id == 1)
            .Select(x => new { x.Name })
            .WriteJson(buffer);

        var text = Encoding.UTF8.GetString(buffer.ToArray());
        using var parsed = JsonDocument.Parse(text);
        parsed.RootElement[0].GetProperty("Name").GetString().Should().Be(expected);
    }

    [Fact]
    public async Task NativeAsync_DestinationIsCallerOwned_ShouldNotFlushOrDispose()
    {
        EnsureNativeTable();

        using var sink = new TrackingStream();
        await _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .WriteJsonAsync(sink, cancellationToken: TestContext.Current.CancellationToken);

        sink.Length.Should().BeGreaterThan(0);
        sink.FlushCount.Should().Be(0);
        sink.Disposed.Should().BeFalse();
        sink.ToArray().Should().Equal(StreamNative());
    }

    [Fact]
    public async Task NativeAsync_MidStreamCancellation_ShouldKeepPartialOutputAndOwnership()
    {
        EnsureNativeTable();

        using var cts = new CancellationTokenSource();
        using var sink = new CancelAfterFirstWriteStream(cts);
        var act = async () => await _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .WriteJsonAsync(sink, cancellationToken: cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        sink.Length.Should().BeGreaterThan(0, "cancellation must observe the already-written partial document");
        sink.Disposed.Should().BeFalse();

        // The reader must have been released: the same context still serves a later query.
        _sut.DataProvider.From<INativeJsonUnicodeEntity>().Count().Should().Be(4);
    }

    [Fact]
    public void NativeSync_MidStreamWriteFailure_ShouldKeepPartialOutputAndOwnership()
    {
        EnsureNativeTable();

        using var sink = new ThrowAfterFirstWriteStream();
        var act = () => _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name })
            .WriteJson(sink);

        act.Should().Throw<OperationCanceledException>();
        sink.Length.Should().BeGreaterThan(0, "the write failure must keep the already-written partial document");
        sink.Disposed.Should().BeFalse();

        _sut.DataProvider.From<INativeJsonUnicodeEntity>().Count().Should().Be(4);
    }

    [Fact]
    public void Native_EntityBuilderWholeEntity_ShouldStreamRawAndHonorIgnoreNull()
    {
        EnsureNativeTable();

        // The whole-entity EntityBuilder surface (no Select) reaches native: raw Unicode proves the
        // database document transport.
        using var full = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>().OrderBy(x => x.Id).WriteJson(full);
        var fullText = Encoding.UTF8.GetString(full.ToArray());
        fullText.Should().Contain("😀").And.NotContain("\\u003C");
        using (var document = JsonDocument.Parse(fullText))
        {
            document.RootElement.GetArrayLength().Should().Be(4);
            document.RootElement[2].GetProperty("Name").ValueKind.Should().Be(JsonValueKind.Null);
        }

        // Native IgnoreNull=true maps to FOR JSON without INCLUDE_NULL_VALUES, so the SQL NULL Name member
        // is omitted exactly as the managed writer omits it.
        using var ignore = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>().OrderBy(x => x.Id)
            .WriteJson(ignore, new JsonStreamOptions { IgnoreNull = true });
        var ignoreText = Encoding.UTF8.GetString(ignore.ToArray());
        using (var document = JsonDocument.Parse(ignoreText))
        {
            document.RootElement.GetArrayLength().Should().Be(4);
            document.RootElement[2].TryGetProperty("Name", out _).Should().BeFalse();
            document.RootElement[0].GetProperty("Name").GetString().Should().Be("plain");
        }
    }

    private sealed class TrackingStream : MemoryStream
    {
        public int FlushCount { get; private set; }

        public bool Disposed { get; private set; }

        public override void Flush()
        {
            FlushCount++;
            base.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            FlushCount++;
            return base.FlushAsync(cancellationToken);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    // Cancels the token right after the first async write, so the pump observes cancellation mid-document.
    private sealed class CancelAfterFirstWriteStream(CancellationTokenSource cts) : MemoryStream
    {
        public bool Disposed { get; private set; }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await base.WriteAsync(buffer, cancellationToken).ConfigureAwait(false);
            await cts.CancelAsync().ConfigureAwait(false);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }

    // Throws on the second synchronous write (a mid-document destination failure).
    private sealed class ThrowAfterFirstWriteStream : MemoryStream
    {
        private int _writes;

        public bool Disposed { get; private set; }

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (_writes++ > 0)
                throw new OperationCanceledException("destination write cancelled mid-document");

            base.Write(buffer, offset, count);
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
