using System.Data.Common;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Shared coverage for the streaming LOB terminals (<c>ToStream</c>/<c>ToTextReader</c> and their
/// async twins). PostgreSQL, SQL Server and SQLite implement them: the reader is opened with
/// <c>CommandBehavior.SequentialAccess</c> and the returned <see cref="Stream"/>/<see cref="TextReader"/>
/// owns the reader and the per-call command until it is disposed. MySQL/MariaDB and ClickHouse reject
/// the terminal with <see cref="NotSupportedException"/> because their drivers do not provide
/// memory-bounded LOB streaming (<c>SupportsSequentialAccess</c> is deliberately left false); the
/// in-memory provider supports the scalar terminals over the materialized value but rejects the
/// multi-column <c>ToDataReader</c>.
/// </summary>
public abstract partial class CommonTestSuite
{
    private const int LobSize = 8 * 1024 * 1024;

    private static readonly byte[] ExpectedBlob = CreateExpectedBlob();
    private static readonly string ExpectedText = new('x', LobSize);

    private static byte[] CreateExpectedBlob()
    {
        var data = new byte[LobSize];
        Array.Fill(data, (byte)0xAB);
        return data;
    }

    private void RequireLobStreaming()
        => Assert.SkipUnless(
            Provider.SupportsLobStreaming,
            "This provider does not implement streaming LOB reads.");

    private void RequireLobUnsupported()
        => Assert.SkipUnless(
            !Provider.SupportsLobStreaming,
            "This provider implements streaming LOB reads.");

    [Fact]
    public void LobBlob_ToStream_ShouldRoundTripAllBytes()
    {
        RequireLobStreaming();

        using var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        buffer.ToArray().Should().Equal(ExpectedBlob);
    }

    [Fact]
    public async Task LobBlob_ToStreamAsync_ShouldRoundTripAllBytes()
    {
        RequireLobStreaming();

        await using var stream = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .ToStreamAsync(TestContext.Current.CancellationToken);

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, TestContext.Current.CancellationToken);

        buffer.ToArray().Should().Equal(ExpectedBlob);
    }

    [Fact]
    public void LobBlob_ToStream_WithParameter_ShouldRoundTripAllBytes()
    {
        RequireLobStreaming();

        using var stream = _sut.LobEntity
            .Where(it => it.Id == SqlFunctions.Parameter<int>(0))
            .Select(it => it.Data!)
            .ToStream(1);

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        buffer.ToArray().Should().Equal(ExpectedBlob);
    }

    [Fact]
    public void LobClob_ToTextReader_ShouldRoundTripAllChars()
    {
        RequireLobStreaming();

        using var reader = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Body!).ToTextReader();

        reader.ReadToEnd().Should().Be(ExpectedText);
    }

    [Fact]
    public async Task LobClob_ToTextReaderAsync_ShouldRoundTripAllChars()
    {
        RequireLobStreaming();

        using var reader = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .ToTextReaderAsync(TestContext.Current.CancellationToken);

        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);

        text.Should().Be(ExpectedText);
    }

    // --- Named-column (TableAlias) streaming accessors: the same terminal ownership, but the LOB
    // column is selected by name and typed as Stream/TextReader. ---

    [Fact]
    public void LobBlob_NamedColumnGetStream_ShouldRoundTripAllBytes()
    {
        RequireLobStreaming();

        using var stream = _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t.GetStream("data"))
            .ToStream();

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        buffer.ToArray().Should().Equal(ExpectedBlob);
    }

    [Fact]
    public void LobClob_NamedColumnGetTextReader_ShouldRoundTripAllChars()
    {
        RequireLobStreaming();

        using var reader = _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t.GetTextReader("body"))
            .ToTextReader();

        reader.ReadToEnd().Should().Be(ExpectedText);
    }

    [Fact]
    public void LobBlob_NamedColumnAsStream_ShouldRoundTripAllBytes()
    {
        RequireLobStreaming();

        using var stream = _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t["data"].AsStream)
            .ToStream();

        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        buffer.ToArray().Should().Equal(ExpectedBlob);
    }

    // --- Row streaming: a streaming accessor inside an enumerated projection. The stream/reader is
    // materialized per row and is valid only until the enumerator advances (MoveNext); the enumerator
    // owns the underlying reader and command, so the value must be consumed inside the loop body. The
    // streaming member must be the last projection member (sequential-access ordering). ---

    [Fact]
    public async Task LobBlob_RowStream_ShouldRoundTripAllBytes()
    {
        RequireLobStreaming();

        var rows = 0;
        await foreach (var row in _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => new { Id = t.GetInt32("id"), Data = t.GetStream("data") })
            .ToAsyncEnumerable(TestContext.Current.CancellationToken))
        {
            rows++;
            row.Id.Should().Be(1);

            // The stream is valid only until the next MoveNext: read and release it before iterating.
            using var buffer = new MemoryStream();
            await row.Data.CopyToAsync(buffer, TestContext.Current.CancellationToken);
            await row.Data.DisposeAsync();

            buffer.Length.Should().Be(LobSize);
            buffer.ToArray().Should().Equal(ExpectedBlob);
        }

        rows.Should().Be(1);
    }

    [Fact]
    public async Task LobClob_RowTextReader_ShouldRoundTripAllChars()
    {
        RequireLobStreaming();

        var rows = 0;
        await foreach (var row in _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => new { Id = t.GetInt32("id"), Body = t.GetTextReader("body") })
            .ToAsyncEnumerable(TestContext.Current.CancellationToken))
        {
            rows++;
            row.Id.Should().Be(1);

            var text = await row.Body.ReadToEndAsync(TestContext.Current.CancellationToken);
            row.Body.Dispose();

            text.Length.Should().Be(LobSize);
            text.Should().Be(ExpectedText);
        }

        rows.Should().Be(1);
    }

    // --- Row-streaming lifetime (part 2, #101 P1). The per-row stream is valid only until the
    // enumerator advances: an early break must release the reader/command (and, on SQLite, unpin the
    // rowid statement). A dedicated small multi-row set is seeded so the 8 MiB base row is not read. ---

    private const int MultiRowLobIdA = 101;
    private const int MultiRowLobIdB = 102;
    private const int NullLobId = 103;
    private const string SmallText = "small-row-text";

    private static readonly byte[] SmallBlobA = CreateSmallBlob(0x2A);
    private static readonly byte[] SmallBlobB = CreateSmallBlob(0x5C);

    private static byte[] CreateSmallBlob(byte value)
    {
        var data = new byte[64];
        Array.Fill(data, value);
        return data;
    }

    private void SeedStreamingRows()
    {
        RemoveStreamingRows();

        var ctx = _sut.DataProvider;
        using (ctx.ExecuteRaw(
            "insert into lob_entity (id, data, body) values (@id, @data, @body)",
            [
                new ProcedureParameter("id", MultiRowLobIdA),
                new ProcedureParameter("data", SmallBlobA),
                new ProcedureParameter("body", SmallText),
            ]))
        {
        }

        using (ctx.ExecuteRaw(
            "insert into lob_entity (id, data, body) values (@id, @data, @body)",
            [
                new ProcedureParameter("id", MultiRowLobIdB),
                new ProcedureParameter("data", SmallBlobB),
                new ProcedureParameter("body", SmallText),
            ]))
        {
        }

        // A NULL streaming column: the mapper calls IsDBNull(ordinal) and must short-circuit GetStream.
        using (ctx.ExecuteRaw($"insert into lob_entity (id, data, body) values ({NullLobId}, null, null)"))
        {
        }
    }

    private void RemoveStreamingRows()
    {
        var ctx = _sut.DataProvider;
        using (ctx.ExecuteRaw($"delete from lob_entity where id in ({MultiRowLobIdA}, {MultiRowLobIdB}, {NullLobId})"))
        {
        }
    }

    /// <summary>
    /// Returns whether a per-row stream captured before <c>MoveNext</c> still serves the watched row's
    /// data after the enumerator advanced. The lifetime contract only promises the <em>safety</em>
    /// property — a stale handle must not yield the watched row's bytes — so this returns
    /// <see langword="false"/> for every accepted outcome: end-of-stream (0 bytes), a different row's
    /// bytes, or any exception. What happens after the step is undefined and provider-specific:
    /// PostgreSQL may hand back the next row's bytes, SQL Server throws <c>ObjectDisposedException</c>
    /// when the active sequential stream is closed, SQLite returns EOF at the blob's end. No specific
    /// exception is required.
    /// </summary>
    private static bool StaleStreamYieldsWatchedRow(Stream stale, byte[] watchedRow)
    {
        try
        {
            var probe = new byte[watchedRow.Length];
            var read = stale.Read(probe, 0, probe.Length);
            return read > 0 && probe.AsSpan(0, read).SequenceEqual(watchedRow.AsSpan(0, read));
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static byte[] ExpectedSmallBlob(int id)
        => id == MultiRowLobIdA
            ? SmallBlobA
            : id == MultiRowLobIdB
                ? SmallBlobB
                : throw new ArgumentOutOfRangeException(nameof(id));

    [Fact]
    public async Task LobBlob_RowStream_ReadAfterMoveNext_ShouldBeInvalid()
    {
        RequireLobStreaming();
        SeedStreamingRows();

        Stream? stale = null;

        try
        {
            var staleId = 0;
            var reads = 0;

            await foreach (var row in _sut.From("lob_entity")
                .Where(t => t.GetInt32("id") >= MultiRowLobIdA && t.GetInt32("id") <= MultiRowLobIdB)
                .Select(t => new { Id = t.GetInt32("id"), Data = t.GetStream("data") })
                .ToAsyncEnumerable(TestContext.Current.CancellationToken))
            {
                reads++;

                if (reads == 1)
                {
                    // Fully read the watched row's stream inside this iteration, then deliberately keep
                    // the (now exhausted) handle alive across MoveNext so the next iteration can probe
                    // it. Reading to the end is required: a sequential provider (PostgreSQL) only
                    // finishes the column read at EOF, which lets the enumerator advance and materialise
                    // the next row; a partially-read handle would wedge the next GetStream call.
                    staleId = row.Id;
                    stale = row.Data;
                    using var first = new MemoryStream();
                    await stale.CopyToAsync(first, TestContext.Current.CancellationToken);
                    first.ToArray().Should().Equal(ExpectedSmallBlob(staleId));
                }
                else
                {
                    // Read this row's stream fully within its own iteration before touching the stale
                    // handle: on SQL Server the per-row stream wraps the shared reader, so releasing
                    // the stale handle first would tear down the reader this row still needs.
                    using var buffer = new MemoryStream();
                    await row.Data.CopyToAsync(buffer, TestContext.Current.CancellationToken);
                    await row.Data.DisposeAsync();
                    buffer.ToArray().Should().Equal(ExpectedSmallBlob(row.Id));

                    // MoveNext has advanced: the stale handle must not serve the watched row. Its
                    // behavior after the step is undefined/provider-specific, so only the safety
                    // property is asserted.
                    StaleStreamYieldsWatchedRow(stale!, ExpectedSmallBlob(staleId))
                        .Should().BeFalse(
                            "a per-row stream must not yield the watched row's data after the enumerator advances");

                    await stale!.DisposeAsync();
                    stale = null;
                }
            }

            reads.Should().Be(2);
        }
        finally
        {
            if (stale is not null)
                await stale.DisposeAsync();
            RemoveStreamingRows();
        }
    }

    [Fact]
    public async Task LobBlob_RowStream_EarlyBreak_ShouldReleaseAndNotPinStatement()
    {
        RequireLobStreaming();
        SeedStreamingRows();

        try
        {
            var enumerated = 0;
            await foreach (var row in _sut.From("lob_entity")
                .Where(t => t.GetInt32("id") >= MultiRowLobIdA && t.GetInt32("id") <= MultiRowLobIdB)
                .Select(t => new { Id = t.GetInt32("id"), Data = t.GetStream("data") })
                .ToAsyncEnumerable(TestContext.Current.CancellationToken))
            {
                enumerated++;

                // Release deterministically on every exit path (including break/throw): on SQLite a
                // leaked rowid locator keeps the statement pinned and later cleanup hits a locked DB.
                await using var data = row.Data;
                var probe = new byte[8];
                (await data.ReadAsync(probe.AsMemory(), TestContext.Current.CancellationToken))
                    .Should().Be(probe.Length);

                break;
            }

            enumerated.Should().Be(1);

            // An early exit must not pin the statement; on SQLite a leaked rowid locator would.
            AssertContextAndBufferedShapeStillWork();
        }
        finally
        {
            RemoveStreamingRows();
        }
    }

    // --- #19: the generated streaming mapper calls IsDBNull(ordinal) then GetStream/GetTextReader on
    // the same ordinal under SequentialAccess. Prove both a NULL (IsDBNull true, accessor skipped) and
    // a non-NULL (IsDBNull false, accessor read) row on every streaming provider. ---

    [Fact]
    public async Task LobBlob_RowStream_NullAndNonNull_ShouldReadIsDBNullThenGetStream()
    {
        RequireLobStreaming();
        SeedStreamingRows();

        try
        {
            var lengths = new Dictionary<int, long?>();

            await foreach (var row in _sut.From("lob_entity")
                .Where(t => t.GetInt32("id") >= MultiRowLobIdA && t.GetInt32("id") <= NullLobId)
                .Select(t => new { Id = t.GetInt32("id"), Data = t.GetStream("data") })
                .ToAsyncEnumerable(TestContext.Current.CancellationToken))
            {
                // Disposed at the end of the iteration (even on an assertion failure) so the per-row
                // handle never outlives the step.
                await using var data = row.Data;
                if (data is null)
                {
                    lengths[row.Id] = null;
                }
                else
                {
                    using var buffer = new MemoryStream();
                    await data.CopyToAsync(buffer, TestContext.Current.CancellationToken);
                    lengths[row.Id] = buffer.Length;
                }
            }

            lengths[MultiRowLobIdA].Should().Be(SmallBlobA.Length);
            lengths[MultiRowLobIdB].Should().Be(SmallBlobB.Length);
            lengths[NullLobId].Should().BeNull("a NULL streaming column maps to null without calling GetStream");
        }
        finally
        {
            RemoveStreamingRows();
        }
    }

    [Fact]
    public async Task LobClob_RowStream_NullAndNonNull_ShouldReadIsDBNullThenGetTextReader()
    {
        RequireLobStreaming();
        SeedStreamingRows();

        try
        {
            var lengths = new Dictionary<int, long?>();

            await foreach (var row in _sut.From("lob_entity")
                .Where(t => t.GetInt32("id") >= MultiRowLobIdA && t.GetInt32("id") <= NullLobId)
                .Select(t => new { Id = t.GetInt32("id"), Body = t.GetTextReader("body") })
                .ToAsyncEnumerable(TestContext.Current.CancellationToken))
            {
                // Disposed at the end of the iteration (even on an assertion failure) so the per-row
                // handle never outlives the step.
                using var body = row.Body;
                if (body is null)
                {
                    lengths[row.Id] = null;
                }
                else
                {
                    var text = await body.ReadToEndAsync(TestContext.Current.CancellationToken);
                    lengths[row.Id] = text.Length;
                }
            }

            lengths[MultiRowLobIdA].Should().Be(SmallText.Length);
            lengths[MultiRowLobIdB].Should().Be(SmallText.Length);
            lengths[NullLobId].Should().BeNull("a NULL streaming column maps to null without calling GetTextReader");
        }
        finally
        {
            RemoveStreamingRows();
        }
    }

    [Fact]
    public async Task Lob_RowStream_UnsupportedProvider_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = async () =>
        {
            await foreach (var _ in _sut.From("lob_entity")
                .Where(t => t.GetInt32("id") == 1)
                .Select(t => new { Id = t.GetInt32("id"), Data = t.GetStream("data") })
                .ToAsyncEnumerable(TestContext.Current.CancellationToken))
            {
            }
        };

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public void LobStream_Dispose_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobStreaming();

        var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream();
        stream.CopyTo(Stream.Null);
        stream.Dispose();

        // The context/connection is still usable once the stream released the reader and command.
        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public async Task LobStream_DisposeAsync_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobStreaming();

        var stream = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .ToStreamAsync(TestContext.Current.CancellationToken);

        await stream.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);
        await stream.DisposeAsync();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public void LobStream_ReadAfterDispose_ShouldThrowObjectDisposed()
    {
        RequireLobStreaming();

        var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream();
        stream.Dispose();

        var act = () => stream.ReadByte();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void LobTextReader_ReadAfterDispose_ShouldThrowObjectDisposed()
    {
        RequireLobStreaming();

        var reader = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Body!).ToTextReader();
        reader.Dispose();

        var act = () => reader.Read();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void LobStream_ContextDisposedBeforeStream_ShouldThrowObjectDisposed()
    {
        RequireLobStreaming();

        var ctx = Provider.CreateContext();
        var command = ctx.From<LobEntity>().Where(it => it.Id == 1).Select(it => it.Data!);
        ctx.Dispose();

        var act = () => command.ToStream();

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void LobBlob_NonSingleColumnProjection_ShouldThrowInvalidOperationException()
    {
        RequireLobStreaming();

        // A byte[]-typed command whose SQL yields two columns is not a single-value terminal.
        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data, id from lob_entity where id = 1");

        var act = () => command.ToStream();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void LobStream_CancelledBeforeOpen_ShouldThrowAndNotLeak()
    {
        RequireLobStreaming();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!);

        var act = () => command.ToStream(cts.Token);

        act.Should().Throw<OperationCanceledException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public async Task LobStreamAsync_CancelledBeforeOpen_ShouldThrowAndNotLeak()
    {
        RequireLobStreaming();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!);

        var act = async () => await command.ToStreamAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public void LobBlob_StreamingAfterBuffered_ShouldNotReuseBufferedPlan()
    {
        RequireLobStreaming();

        // Same SQL shape buffered first: the streaming discriminator must be part of the plan key,
        // otherwise the stream terminal would reuse the buffered (Behavior = 0) plan.
        _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).First().Should().Equal(ExpectedBlob);

        using var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream();
        using var buffer = new MemoryStream();
        stream.CopyTo(buffer);

        buffer.ToArray().Should().Equal(ExpectedBlob);
    }

    [Fact]
    public void LobBlob_BufferedAfterStreaming_ShouldStillMaterialize()
    {
        RequireLobStreaming();

        using (var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream())
        {
            stream.CopyTo(Stream.Null);
        }

        // The LOB plan must not poison the plan cache for the buffered shape.
        _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).First().Should().Equal(ExpectedBlob);
    }

    [Fact]
    public void Lob_UnsupportedProvider_ToStream_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = () => _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Lob_UnsupportedProvider_ToTextReader_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = () => _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Body!).ToTextReader();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task Lob_UnsupportedProvider_ToStreamAsync_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = async () => await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .ToStreamAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Lob_UnsupportedProvider_ToTextReaderAsync_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = async () => await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .ToTextReaderAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // The named-column (TableAlias) streaming accessors reach the same terminal and must fail closed
    // on the same providers: the dialect lacks sequential access (MySQL/MariaDB, ClickHouse) or the
    // context is the in-memory provider.

    [Fact]
    public void Lob_UnsupportedProvider_NamedColumnGetStream_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = () => _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t.GetStream("data"))
            .ToStream();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Lob_UnsupportedProvider_NamedColumnGetTextReader_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = () => _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t.GetTextReader("body"))
            .ToTextReader();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Lob_UnsupportedProvider_NamedColumnAsStream_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = () => _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t["data"].AsStream)
            .ToStream();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void Lob_UnsupportedProvider_NamedColumnAsTextReader_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = () => _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t["body"].AsTextReader)
            .ToTextReader();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task Lob_UnsupportedProvider_NamedColumnGetStreamAsync_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = async () => await _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t.GetStream("data"))
            .ToStreamAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    [Fact]
    public async Task Lob_UnsupportedProvider_NamedColumnGetTextReaderAsync_ShouldThrowNotSupported()
    {
        RequireLobUnsupported();

        var act = async () => await _sut.From("lob_entity")
            .Where(t => t.GetInt32("id") == 1)
            .Select(t => t.GetTextReader("body"))
            .ToTextReaderAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // --- Multi-column DbDataReader terminal (issue #27, phase 2). PostgreSQL and SQL Server expose
    // the reader; SQLite fails closed on its rowid locator and MySQL/MariaDB, ClickHouse and the
    // in-memory provider have no sequential-access support. ---

    private void RequireLobDataReader()
        => Assert.SkipUnless(
            Provider.SupportsLobDataReader,
            "This provider does not implement the multi-column LOB reader terminal.");

    private void RequireLobDataReaderUnsupported()
        => Assert.SkipUnless(
            !Provider.SupportsLobDataReader,
            "This provider implements the multi-column LOB reader terminal.");

    [Fact]
    public void LobDataReader_ToDataReader_ShouldReadTwoColumnsAcrossRows()
    {
        RequireLobDataReader();

        using var reader = _sut.ComplexEntity
            .OrderBy(it => it.Id)
            .Select(it => new { it.Id, it.String })
            .ToDataReader();

        var rows = new List<(long Id, string? Text)>();
        while (reader.Read())
            rows.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));

        rows.Should().Equal((1L, "dadfasd"), (2L, "xxx"), (3L, null));
    }

    [Fact]
    public async Task LobDataReader_ToDataReaderAsync_ShouldReadTwoColumnsAcrossRows()
    {
        RequireLobDataReader();

        await using var reader = await _sut.ComplexEntity
            .OrderBy(it => it.Id)
            .Select(it => new { it.Id, it.String })
            .ToDataReaderAsync(TestContext.Current.CancellationToken);

        var rows = new List<(long Id, string? Text)>();
        while (await reader.ReadAsync(TestContext.Current.CancellationToken))
            rows.Add((reader.GetInt64(0), reader.IsDBNull(1) ? null : reader.GetString(1)));

        rows.Should().Equal((1L, "dadfasd"), (2L, "xxx"), (3L, null));
    }

    [Fact]
    public void LobDataReader_Dispose_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobDataReader();

        var reader = _sut.ComplexEntity
            .OrderBy(it => it.Id)
            .Select(it => new { it.Id, it.String })
            .ToDataReader();

        reader.Read().Should().BeTrue();
        reader.Dispose();
        reader.Dispose();

        var act = () => reader.Read();
        act.Should().Throw<ObjectDisposedException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public async Task LobDataReader_DisposeAsync_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobDataReader();

        var reader = await _sut.ComplexEntity
            .OrderBy(it => it.Id)
            .Select(it => new { it.Id, it.String })
            .ToDataReaderAsync(TestContext.Current.CancellationToken);

        (await reader.ReadAsync(TestContext.Current.CancellationToken)).Should().BeTrue();
        await reader.DisposeAsync();
        await reader.DisposeAsync();

        var act = async () => await reader.ReadAsync(TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<ObjectDisposedException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public void LobDataReader_CancelledBeforeOpen_ShouldThrowAndNotLeak()
    {
        RequireLobDataReader();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String });

        var act = () => command.ToDataReader(cts.Token);

        act.Should().Throw<OperationCanceledException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public async Task LobDataReader_CancelledBeforeOpenAsync_ShouldThrowAndNotLeak()
    {
        RequireLobDataReader();

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String });

        var act = async () => await command.ToDataReaderAsync(cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
    }

    [Fact]
    public void LobDataReader_UnsupportedProvider_ShouldThrowNotSupported()
    {
        RequireLobDataReaderUnsupported();

        var act = () => _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String }).ToDataReader();

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public async Task LobDataReader_UnsupportedProviderAsync_ShouldThrowNotSupported()
    {
        RequireLobDataReaderUnsupported();

        var command = _sut.ComplexEntity.OrderBy(it => it.Id).Select(it => new { it.Id, it.String });

        var act = async () => await command.ToDataReaderAsync(TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotSupportedException>();
    }

    // --- Resource ownership: a partial read (caller stops mid-read), a cancelled read, and a
    // failed projection must all release the DbDataReader + per-call DbCommand and leave the
    // context reusable, including for a repeated buffered query of the same shape. ---

    private void AssertContextAndBufferedShapeStillWork()
    {
        _sut.SimpleEntity.Where(it => it.Id == 1).Select(it => it.Id).First().Should().Be(1);
        _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).First().Should().Equal(ExpectedBlob);
        _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Body!).First().Should().Be(ExpectedText);
    }

    [Fact]
    public void LobStream_PartialReadThenDispose_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobStreaming();

        var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream();
        var buffer = new byte[1024];
        stream.Read(buffer, 0, buffer.Length).Should().Be(buffer.Length);
        stream.Dispose();

        AssertContextAndBufferedShapeStillWork();
    }

    [Fact]
    public async Task LobStreamAsync_CancelledMidRead_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobStreaming();

        using var cts = new CancellationTokenSource();
        var stream = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .ToStreamAsync(cts.Token);

        var buffer = new byte[1024];
        (await stream.ReadAsync(buffer.AsMemory(), cts.Token)).Should().Be(buffer.Length);

        cts.Cancel();
        await stream.DisposeAsync();

        AssertContextAndBufferedShapeStillWork();
    }

    [Fact]
    public async Task LobTextReaderAsync_PartialReadThenDispose_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobStreaming();

        var reader = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .ToTextReaderAsync(TestContext.Current.CancellationToken);

        var buffer = new char[1024];
        (await reader.ReadAsync(buffer.AsMemory(), TestContext.Current.CancellationToken)).Should().Be(buffer.Length);

        await ((IAsyncDisposable)reader).DisposeAsync();

        AssertContextAndBufferedShapeStillWork();
    }

    [Fact]
    public void LobStream_NonSingleColumnProjection_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobStreaming();

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data, id from lob_entity where id = 1");

        FluentActions.Invoking(() => command.ToStream()).Should().Throw<InvalidOperationException>();

        AssertContextAndBufferedShapeStillWork();
    }

    [Fact]
    public async Task LobStreamAsync_NonSingleColumnProjection_ShouldReleaseReaderAndKeepContextUsable()
    {
        RequireLobStreaming();

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data, id from lob_entity where id = 1");

        var act = async () => await command.ToStreamAsync(TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();

        AssertContextAndBufferedShapeStillWork();
    }

    // --- Column-count guard: the single-value terminal must reject any FieldCount other than 1,
    // for both value types, sync and async, and at the 0/3 boundaries. ---

    [Fact]
    public void LobTextReader_NonSingleColumnProjection_ShouldThrowInvalidOperationException()
    {
        RequireLobStreaming();

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .WithSql("select body, id from lob_entity where id = 1");

        FluentActions.Invoking(() => command.ToTextReader()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task LobTextReaderAsync_NonSingleColumnProjection_ShouldThrowInvalidOperationException()
    {
        RequireLobStreaming();

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .WithSql("select body, id from lob_entity where id = 1");

        var act = async () => await command.ToTextReaderAsync(TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void LobBlob_ThreeColumnProjection_ShouldThrowInvalidOperationException()
    {
        RequireLobStreaming();

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data, id, body from lob_entity where id = 1");

        FluentActions.Invoking(() => command.ToStream()).Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public async Task LobBlob_ThreeColumnProjectionAsync_ShouldThrowInvalidOperationException()
    {
        RequireLobStreaming();

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data, id, body from lob_entity where id = 1");

        var act = async () => await command.ToStreamAsync(TestContext.Current.CancellationToken);
        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public void LobBlob_ZeroColumnProjection_ShouldThrowInvalidOperationException()
    {
        RequireLobStreaming();
        Assert.SkipUnless(Provider.SupportsZeroColumnResult, "This provider cannot return a zero-column result set.");

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select from lob_entity where id = 1");

        FluentActions.Invoking(() => command.ToStream()).Should().Throw<InvalidOperationException>();
    }

    // --- Criterion 5, observed: buffering the same shape before and after a streaming read must
    // return the same correct rows, so the streaming terminal neither reuses nor poisons the
    // buffered plan. The key discriminator is deliberately not asserted. ---

    [Fact]
    public async Task LobBlob_BufferedToListBeforeAndAfterStream_ShouldMatch()
    {
        RequireLobStreaming();

        var before = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .ToListAsync(TestContext.Current.CancellationToken);

        using (var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream())
        {
            stream.CopyTo(Stream.Null);
        }

        var after = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .ToListAsync(TestContext.Current.CancellationToken);

        before.Should().HaveCount(1);
        before[0].Should().Equal(ExpectedBlob);
        after.Should().HaveCount(1);
        after[0].Should().Equal(ExpectedBlob);
    }

    [Fact]
    public async Task LobClob_BufferedToListBeforeAndAfterTextReader_ShouldMatch()
    {
        RequireLobStreaming();

        var before = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .ToListAsync(TestContext.Current.CancellationToken);

        using (var reader = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Body!).ToTextReader())
        {
            reader.ReadToEnd();
        }

        var after = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .ToListAsync(TestContext.Current.CancellationToken);

        before.Should().Equal(ExpectedText);
        after.Should().Equal(ExpectedText);
    }

    // --- Interceptors: the LOB path goes around ResultSetEnumerator, so it must raise the command
    // lifecycle itself, exactly once per terminal, matching the buffered path. ---

    [Fact]
    public void LobSync_Interceptor_ShouldRaiseLifecycleOncePerTerminal()
    {
        RequireLobStreaming();

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        using (var stream = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Data!).ToStream())
        {
            stream.CopyTo(Stream.Null);
        }

        interceptor.Executing.Should().Be(1);
        interceptor.Executed.Should().Be(1);

        using (var reader = _sut.LobEntity.Where(it => it.Id == 1).Select(it => it.Body!).ToTextReader())
        {
            reader.ReadToEnd();
        }

        interceptor.Executing.Should().Be(2);
        interceptor.Executed.Should().Be(2);
        interceptor.Failed.Should().Be(0);
    }

    [Fact]
    public async Task LobAsync_Interceptor_ShouldRaiseLifecycleOncePerTerminal()
    {
        RequireLobStreaming();

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        await using (var stream = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .ToStreamAsync(TestContext.Current.CancellationToken))
        {
            await stream.CopyToAsync(Stream.Null, TestContext.Current.CancellationToken);
        }

        interceptor.Executing.Should().Be(1);
        interceptor.Executed.Should().Be(1);

        using (var reader = await _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Body!)
            .ToTextReaderAsync(TestContext.Current.CancellationToken))
        {
            await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        }

        interceptor.Executing.Should().Be(2);
        interceptor.Executed.Should().Be(2);
        interceptor.Failed.Should().Be(0);
    }

    [Fact]
    public void LobStream_Interceptor_OnFailure_ShouldRaiseFailedOnce()
    {
        RequireLobStreaming();

        var interceptor = new CountingQueryInterceptor();
        ((DataContext)_sut.DataProvider).AddInterceptor(interceptor);

        var command = _sut.LobEntity
            .Where(it => it.Id == 1)
            .Select(it => it.Data!)
            .WithSql("select data from lob_entity_missing where id = 1");

        FluentActions.Invoking(() => command.ToStream()).Should().Throw<DbException>();

        interceptor.Executing.Should().Be(1);
        interceptor.Failed.Should().Be(1);
        interceptor.Executed.Should().Be(0);
    }

    private sealed class CountingQueryInterceptor : IQueryInterceptor
    {
        private int _executing;
        private int _executed;
        private int _failed;

        public int Executing => Volatile.Read(ref _executing);
        public int Executed => Volatile.Read(ref _executed);
        public int Failed => Volatile.Read(ref _failed);

        public void CommandExecuting(CommandEventData eventData, DbCommand command)
            => Interlocked.Increment(ref _executing);

        public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
            => Interlocked.Increment(ref _executed);

        public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
            => Interlocked.Increment(ref _failed);
    }
}
