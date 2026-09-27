using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// The streaming LOB terminals. Providers that support
/// <c>CommandBehavior.SequentialAccess</c> return a reader-owning wrapper; the in-memory provider has no
/// reader, so it serves the already-materialized value as a plain BCL <see cref="MemoryStream"/> /
/// <see cref="StringReader"/> (empty and NULL results yield <see cref="Stream.Null"/> /
/// <see cref="TextReader.Null"/>). The multi-column <c>ToDataReader</c> stays relational-only.
/// </summary>
public class LobStreamingTests
{
    private readonly IDataContext _ctx;

    public LobStreamingTests(IDataContext ctx)
    {
        _ctx = ctx;
    }

    private static InMemoryDataContext ContextWith(params LobTestEntity[] rows)
    {
        var ctx = new InMemoryDataContext();
        ctx.From<LobTestEntity>().WithData(rows);
        return ctx;
    }

    private static byte[] ReadAll(Stream stream)
    {
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);
        return buffer.ToArray();
    }

    // --- ToStream (sync) ---

    [Fact]
    public void ToStream_InMemory_WithValue_ShouldReadBytes()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = [1, 2, 3] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        using var stream = command.ToStream();

        ReadAll(stream).Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ToStream_InMemory_ShouldReturnReadOnlyStream()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = [1, 2, 3] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        using var stream = command.ToStream();

        // Same read-only contract as the relational LobStream: no writes, no buffer exposure.
        stream.CanWrite.Should().BeFalse();
        var write = () => stream.Write(new byte[1], 0, 1);
        write.Should().Throw<NotSupportedException>();
        var getBuffer = () => ((MemoryStream)stream).GetBuffer();
        getBuffer.Should().Throw<UnauthorizedAccessException>();
    }

    [Fact]
    public void ToStream_InMemory_EmptyResult_ShouldReturnStreamNull()
    {
        using var ctx = ContextWith();
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        command.ToStream().Should().BeSameAs(Stream.Null);
    }

    [Fact]
    public void ToStream_InMemory_NullValue_ShouldReturnStreamNull()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = null });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        command.ToStream().Should().BeSameAs(Stream.Null);
    }

    [Fact]
    public void ToStream_InMemory_EmptyValue_ShouldReturnEmptyStreamNotSentinel()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = [] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        using var stream = command.ToStream();

        stream.Should().NotBeSameAs(Stream.Null);
        ReadAll(stream).Should().BeEmpty();
    }

    [Fact]
    public void ToStream_InMemory_MultipleRows_ShouldReadFirstRow()
    {
        using var ctx = ContextWith(
            new LobTestEntity { Id = 1, Data = [1] },
            new LobTestEntity { Id = 2, Data = [2] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        using var stream = command.ToStream();

        ReadAll(stream).Should().Equal(1);
    }

    // --- ToStreamAsync ---

    [Fact]
    public async Task ToStreamAsync_InMemory_WithValue_ShouldReadBytes()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = [1, 2, 3] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        using var stream = await command.ToStreamAsync();

        ReadAll(stream).Should().Equal(1, 2, 3);
    }

    [Fact]
    public async Task ToStreamAsync_InMemory_EmptyResult_ShouldReturnStreamNull()
    {
        using var ctx = ContextWith();
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        (await command.ToStreamAsync()).Should().BeSameAs(Stream.Null);
    }

    [Fact]
    public async Task ToStreamAsync_InMemory_NullValue_ShouldReturnStreamNull()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = null });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        (await command.ToStreamAsync()).Should().BeSameAs(Stream.Null);
    }

    [Fact]
    public async Task ToStreamAsync_InMemory_EmptyValue_ShouldReturnEmptyStreamNotSentinel()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = [] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        using var stream = await command.ToStreamAsync();

        stream.Should().NotBeSameAs(Stream.Null);
        ReadAll(stream).Should().BeEmpty();
    }

    [Fact]
    public async Task ToStreamAsync_InMemory_MultipleRows_ShouldReadFirstRow()
    {
        using var ctx = ContextWith(
            new LobTestEntity { Id = 1, Data = [1] },
            new LobTestEntity { Id = 2, Data = [2] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);

        using var stream = await command.ToStreamAsync();

        ReadAll(stream).Should().Equal(1);
    }

    [Fact]
    public async Task ToStreamAsync_InMemory_CanceledToken_ShouldThrowOperationCanceled()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Data = [1] });
        var command = ctx.From<LobTestEntity>().Select(it => it.Data!);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await command.ToStreamAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- ToTextReader (sync) ---

    [Fact]
    public void ToTextReader_InMemory_WithValue_ShouldReadText()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Body = "hello" });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        using var reader = command.ToTextReader();

        reader.ReadToEnd().Should().Be("hello");
    }

    [Fact]
    public void ToTextReader_InMemory_EmptyResult_ShouldReturnTextReaderNull()
    {
        using var ctx = ContextWith();
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        command.ToTextReader().Should().BeSameAs(TextReader.Null);
    }

    [Fact]
    public void ToTextReader_InMemory_NullValue_ShouldReturnTextReaderNull()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Body = null });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        command.ToTextReader().Should().BeSameAs(TextReader.Null);
    }

    [Fact]
    public void ToTextReader_InMemory_EmptyValue_ShouldReturnEmptyReaderNotSentinel()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Body = "" });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        using var reader = command.ToTextReader();

        reader.Should().NotBeSameAs(TextReader.Null);
        reader.ReadToEnd().Should().BeEmpty();
    }

    [Fact]
    public void ToTextReader_InMemory_MultipleRows_ShouldReadFirstRow()
    {
        using var ctx = ContextWith(
            new LobTestEntity { Id = 1, Body = "first" },
            new LobTestEntity { Id = 2, Body = "second" });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        using var reader = command.ToTextReader();

        reader.ReadToEnd().Should().Be("first");
    }

    // --- ToTextReaderAsync ---

    [Fact]
    public async Task ToTextReaderAsync_InMemory_WithValue_ShouldReadText()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Body = "hello" });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        using var reader = await command.ToTextReaderAsync();

        reader.ReadToEnd().Should().Be("hello");
    }

    [Fact]
    public async Task ToTextReaderAsync_InMemory_EmptyResult_ShouldReturnTextReaderNull()
    {
        using var ctx = ContextWith();
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        (await command.ToTextReaderAsync()).Should().BeSameAs(TextReader.Null);
    }

    [Fact]
    public async Task ToTextReaderAsync_InMemory_NullValue_ShouldReturnTextReaderNull()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Body = null });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        (await command.ToTextReaderAsync()).Should().BeSameAs(TextReader.Null);
    }

    [Fact]
    public async Task ToTextReaderAsync_InMemory_EmptyValue_ShouldReturnEmptyReaderNotSentinel()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Body = "" });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        using var reader = await command.ToTextReaderAsync();

        reader.Should().NotBeSameAs(TextReader.Null);
        reader.ReadToEnd().Should().BeEmpty();
    }

    [Fact]
    public async Task ToTextReaderAsync_InMemory_MultipleRows_ShouldReadFirstRow()
    {
        using var ctx = ContextWith(
            new LobTestEntity { Id = 1, Body = "first" },
            new LobTestEntity { Id = 2, Body = "second" });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);

        using var reader = await command.ToTextReaderAsync();

        reader.ReadToEnd().Should().Be("first");
    }

    [Fact]
    public async Task ToTextReaderAsync_InMemory_CanceledToken_ShouldThrowOperationCanceled()
    {
        using var ctx = ContextWith(new LobTestEntity { Id = 1, Body = "hello" });
        var command = ctx.From<LobTestEntity>().Select(it => it.Body!);
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await command.ToTextReaderAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // --- ToDataReader stays relational-only. ---

    [Fact]
    public void ToDataReader_InMemory_ShouldThrowNotSupported()
    {
        var command = _ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => new { it.Id, it.Data });

        var act = () => command.ToDataReader();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task ToDataReaderAsync_InMemory_ShouldThrowNotSupported()
    {
        var command = _ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => new { it.Id, it.Data });

        var act = async () => await command.ToDataReaderAsync();

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // --- Disposed context still fails closed before evaluation. ---

    [Fact]
    public void ToStream_AfterContextDispose_ShouldThrowObjectDisposed()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => it.Data!);
        ctx.Dispose();

        var act = () => command.ToStream();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void ToTextReader_AfterContextDispose_ShouldThrowObjectDisposed()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => it.Body!);
        ctx.Dispose();

        var act = () => command.ToTextReader();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public async Task ToStreamAsync_AfterContextDispose_ShouldThrowObjectDisposed()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => it.Data!);
        ctx.Dispose();

        var act = async () => await command.ToStreamAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ToTextReaderAsync_AfterContextDispose_ShouldThrowObjectDisposed()
    {
        using var ctx = new InMemoryDataContext();
        var command = ctx.From<LobTestEntity>().Where(it => it.Id == 1).Select(it => it.Body!);
        ctx.Dispose();

        var act = async () => await command.ToTextReaderAsync();

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }
}
