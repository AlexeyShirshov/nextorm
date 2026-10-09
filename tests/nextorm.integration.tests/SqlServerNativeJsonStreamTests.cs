using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Reflection;
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

[SqlTable("native_json_measure")]
public interface INativeJsonMeasureEntity
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
    private static bool _measureSeeded;

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

    // Builds a deterministic 10000-row table so the managed/native measurement can compare the two
    // transports at rows 0/1/100/10000; ids are contiguous so `where id <= @n` selects exactly n rows.
    private void EnsureMeasureTable()
    {
        if (_measureSeeded)
            return;

        lock (SeedGate)
        {
            if (_measureSeeded)
                return;

            var context = (DataContext)_sut.DataProvider;
            context.EnsureConnectionOpen();
            using var command = context.CreateCommand(
                "if object_id('native_json_measure') is null " +
                "create table native_json_measure (id int not null primary key, name nvarchar(max) null); " +
                "delete from native_json_measure; " +
                "with n as (select top (10000) row_number() over (order by (select null)) as id " +
                "from sys.all_objects a cross join sys.all_objects b) " +
                "insert into native_json_measure (id, name) " +
                "select id, case when id % 3 = 0 then null else N'row' + cast(id as nvarchar(10)) end from n;");
            command.ExecuteNonQuery();
            _measureSeeded = true;
        }
    }

    // Measures one JSON stream for `rows` rows. `managed: true` uses the ineligible WriteIndented option so
    // the request routes through the managed row writer while the data and projection stay identical; the
    // only output difference is indentation/escaping. The returned Native flag is the guard-bearing
    // PrepareJsonStream route: a native route invokes JsonNativeStream.WriteDocument exactly once per call
    // (QueryExecutor.WriteJsonNative), so it is the closest observable to a native serializer count when no
    // per-invocation counter seam exists.
    private (double ElapsedMs, long AllocatedBytes, byte[] Output, bool Native) MeasureJson(int rows, bool managed)
    {
        var context = (DataContext)_sut.DataProvider;
        var command = _sut.DataProvider.From<INativeJsonMeasureEntity>()
            .Where(x => x.Id <= SqlFunctions.Parameter<int>(0))
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, x.Name });

        var options = managed ? new JsonStreamOptions { WriteIndented = true } : new JsonStreamOptions();
        var native = IsNativeRoute(context, command, options);
        native.Should().Be(!managed, managed
            ? "the ineligible WriteIndented option must route through the managed row writer"
            : "the flat parameterized projection must route through the native FOR JSON transport");

        using var buffer = new MemoryStream();
        var before = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        command.WriteJson(buffer, options, TestContext.Current.CancellationToken, rows);
        stopwatch.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        return (stopwatch.Elapsed.TotalMilliseconds, allocated, buffer.ToArray(), native);
    }

    private void MeasureManagedAndNativeAcrossRowCounts()
    {
        EnsureMeasureTable();

        // Warm up JIT and the plan cache so the first measured row count is not dominated by one-off cost.
        _ = MeasureJson(10000, managed: false);

        foreach (var rows in (int[])[0, 1, 100, 10000])
        {
            var native = MeasureJson(rows, managed: false);
            var managed = MeasureJson(rows, managed: true);

            // Both transports must emit a JSON array of exactly `rows` elements; byte lengths are not
            // compared because native leaves non-ASCII/HTML characters raw while managed escapes them.
            using (var nativeDocument = JsonDocument.Parse(native.Output))
                nativeDocument.RootElement.GetArrayLength().Should().Be(rows);
            using (var managedDocument = JsonDocument.Parse(managed.Output))
                managedDocument.RootElement.GetArrayLength().Should().Be(rows);

            // native_serializer_count is 1 for the native route (one WriteDocument call per request) and 0
            // for managed; it is derived from the guard-bearing route flag, there being no per-invocation
            // counter seam (disclosed in evidence E177-10).
            Console.WriteLine(
                $"[native-measurement] rows={rows} " +
                $"native[ms={native.ElapsedMs:F2} alloc={native.AllocatedBytes} bytes={native.Output.Length} route={native.Native} serializer_count={(native.Native ? 1 : 0)}] " +
                $"managed[ms={managed.ElapsedMs:F2} alloc={managed.AllocatedBytes} bytes={managed.Output.Length} route={managed.Native} serializer_count={(managed.Native ? 1 : 0)}]");
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

        // E177-10 measurement sub-scenario inside this existing entry (no new discovered case): compare the
        // managed and native transports at rows 0/1/100/10000 and print elapsed/allocation/output bytes.
        MeasureManagedAndNativeAcrossRowCounts();
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

    // A1 (live): a supported non-alias/non-column MethodCallExpression projection (string.ToUpper) is a
    // direct pass-through and an admitted string, so a live SQL Server request must select the native FOR
    // JSON route (the actual selection flag returned by the guard-bearing PrepareJsonStream seam), not
    // fall back to the managed row writer.
    [Fact]
    public void Native_MethodCallProjection_LiveSqlServer_ShouldSelectNativeRoute()
    {
        EnsureNativeTable();
        var context = (DataContext)_sut.DataProvider;

        var command = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { Upper = x.Name!.ToUpper() });

        IsNativeRoute(context, command, new JsonStreamOptions()).Should().BeTrue(
            "a supported method-call projection must select the native FOR JSON route on live SQL Server");

        using var buffer = new MemoryStream();
        command.WriteJson(buffer);

        var text = Encoding.UTF8.GetString(buffer.ToArray());
        // Raw non-ASCII and '<' can only come from the database document: the managed System.Text.Json
        // writer would escape them, so these prove the actual database transport, not fallback equality.
        text.Should().Contain("中").And.Contain("<");
        text.Should().NotContain("\\u003C");

        using var document = JsonDocument.Parse(text);
        document.RootElement.GetArrayLength().Should().Be(1);
        document.RootElement[0].GetProperty("Upper").GetString().Should().Contain("中").And.Contain("<");
    }

    // A9 (live): repeated native calls on one context with changed parameters must isolate per-call state
    // (no stale parameter/call-local state), and must not flip the shared command's sticky Cache policy.
    [Fact]
    public void Native_RepeatedCalls_ChangedParams_ShouldIsolateStateAndNotLeakCachePolicy()
    {
        EnsureNativeTable();
        var context = (DataContext)_sut.DataProvider;

        var command = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .Select(x => new { x.Id, x.Name });

        IsNativeRoute(context, command, new JsonStreamOptions()).Should().BeTrue(
            "the parameterized flat projection must select the native route");

        using var first = new MemoryStream();
        command.WriteJson(first, new JsonStreamOptions(), TestContext.Current.CancellationToken, 1);
        using var second = new MemoryStream();
        command.WriteJson(second, new JsonStreamOptions(), TestContext.Current.CancellationToken, 2);
        using var third = new MemoryStream();
        command.WriteJson(third, new JsonStreamOptions(), TestContext.Current.CancellationToken, 1);

        Encoding.UTF8.GetString(first.ToArray()).Should().Contain("\"Id\":1").And.Contain("plain");
        Encoding.UTF8.GetString(second.ToArray()).Should().Contain("\"Id\":2").And.Contain("é中😀<b>\\/slash")
            .And.NotContain("plain");
        third.ToArray().Should().Equal(first.ToArray(),
            "repeating the same parameters must reproduce the same document (no stale parameter/call-local state)");

        // The native path clones the caller's command; it must never set the sticky QueryCommand.Cache =
        // false, which would silently disable the plan cache for every later query on this context.
        command.Cache.Should().BeTrue("a native JSON call must not disable the caller's plan cache");
        context.QueryCacheEnabled.Should().BeTrue();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>().Count().Should().Be(4);
    }

    // R16 (live): native -> managed -> native on one context must route each call independently; the
    // middle call must actually be managed and the surrounding calls actually native, with no inherited
    // state between them.
    [Fact]
    public void NativeThenManagedThenNative_OnOneContext_ShouldRouteEachCallIndependently()
    {
        EnsureNativeTable();
        var context = (DataContext)_sut.DataProvider;

        byte[] Native()
        {
            using var buffer = new MemoryStream();
            _sut.DataProvider.From<INativeJsonUnicodeEntity>()
                .Where(x => x.Id == 2)
                .Select(x => new { x.Id, x.Name })
                .WriteJson(buffer);
            return buffer.ToArray();
        }

        byte[] Managed()
        {
            using var buffer = new MemoryStream();
            _sut.ComplexEntity.Where(x => x.Id == 1)
                .Select(x => new { x.Id, x.Numeric })
                .WriteJson(buffer);
            return buffer.ToArray();
        }

        var nativeCommand = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { x.Id, x.Name });
        IsNativeRoute(context, nativeCommand, new JsonStreamOptions()).Should().BeTrue();

        var managedCommand = _sut.ComplexEntity.Where(x => x.Id == 1)
            .Select(x => new { x.Id, x.Numeric });
        IsNativeRoute(context, managedCommand, new JsonStreamOptions()).Should().BeFalse(
            "a decimal member is outside the admitted native set, so the middle call is managed");

        var first = Native();
        var middle = Managed();
        var last = Native();

        first.Should().Equal(last, "the trailing native call must not inherit the middle managed route");
        Encoding.UTF8.GetString(first).Should().Contain("é").And.Contain("中");
        Encoding.UTF8.GetString(first).Should().NotContain("\\u00e9");

        var expected = JsonSerializer.Serialize(
            _sut.ComplexEntity.Where(x => x.Id == 1).Select(x => new { x.Id, x.Numeric }).ToList());
        middle.Should().Equal(Encoding.UTF8.GetBytes(expected),
            "the middle call must be the managed System.Text.Json output");
    }

    // A2/E177-14 (live): the native positional-params overload binds values in placeholder order on the
    // QueryCommand<TResult> surface, sync and async, with default and explicit options, and observes an
    // absent cancellation token on the non-params overload.
    [Fact]
    public async Task NativeParams_QueryCommandSurface_ShouldBindChangedValuesSyncAndAsync()
    {
        EnsureNativeTable();
        var context = (DataContext)_sut.DataProvider;

        var command = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id >= SqlFunctions.Parameter<int>(0) && x.Id <= SqlFunctions.Parameter<int>(1))
            .Select(x => new { x.Id, x.Name });
        IsNativeRoute(context, command, new JsonStreamOptions()).Should().BeTrue();

        using var syncOrder = new MemoryStream();
        command.WriteJson(syncOrder, new JsonStreamOptions(), TestContext.Current.CancellationToken, 1, 2);
        using var syncSwapped = new MemoryStream();
        command.WriteJson(syncSwapped, new JsonStreamOptions(), TestContext.Current.CancellationToken, 3, 2);

        var syncOrderText = Encoding.UTF8.GetString(syncOrder.ToArray());
        syncOrderText.Should().Contain("\"Id\":1").And.Contain("\"Id\":2");

        // Placeholder order, not invocation order: the same two values swapped select an empty range.
        Encoding.UTF8.GetString(syncSwapped.ToArray()).Should().Be("[]",
            "positional parameters must bind in placeholder order, not invocation order");

        // Explicit options on the async params overload: same binding order, IgnoreNull container.
        using var asyncExplicit = new MemoryStream();
        await command.WriteJsonAsync(asyncExplicit, new JsonStreamOptions { IgnoreNull = true },
            TestContext.Current.CancellationToken, 2, 3);
        var asyncExplicitText = Encoding.UTF8.GetString(asyncExplicit.ToArray());
        asyncExplicitText.Should().Contain("\"Id\":2").And.Contain("\"Id\":3");

        // Absent cancellation token (the async overload's default) streams the same document as sync.
        using var syncDefault = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { x.Id, x.Name })
            .WriteJson(syncDefault);
        using var asyncAbsent = new MemoryStream();
        var absentCommand = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == 2)
            .Select(x => new { x.Id, x.Name });
        await WriteJsonWithoutToken(absentCommand, asyncAbsent);
        asyncAbsent.ToArray().Should().Equal(syncDefault.ToArray());
    }

    // A2/E177-14 (live): the native positional-params overload on the whole-entity EntityBuilder<TEntity>
    // surface binds the changed value per call, sync and async, with default and explicit options and an
    // absent cancellation token.
    [Fact]
    public async Task NativeParams_EntityBuilderSurface_ShouldBindChangedValuesSyncAndAsync()
    {
        EnsureNativeTable();
        var context = (DataContext)_sut.DataProvider;

        var builder = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0));
        IsNativeRoute(context, (QueryCommand<INativeJsonUnicodeEntity>)builder, new JsonStreamOptions())
            .Should().BeTrue("the whole-entity EntityBuilder surface must select the native route");

        using var syncTwo = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .WriteJson(syncTwo, new JsonStreamOptions(), TestContext.Current.CancellationToken, 2);
        var twoText = Encoding.UTF8.GetString(syncTwo.ToArray());
        twoText.Should().Contain("\"Id\":2").And.Contain("é中😀<b>\\/slash");

        using var syncOne = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .WriteJson(syncOne, new JsonStreamOptions(), TestContext.Current.CancellationToken, 1);
        Encoding.UTF8.GetString(syncOne.ToArray())
            .Should().Contain("\"Id\":1").And.Contain("plain").And.NotContain("é中😀");

        // Explicit options on the async params overload omit the SQL NULL member.
        using var asyncThree = new MemoryStream();
        await _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .WriteJsonAsync(asyncThree, new JsonStreamOptions { IgnoreNull = true },
                TestContext.Current.CancellationToken, 3);
        Encoding.UTF8.GetString(asyncThree.ToArray())
            .Should().Contain("\"Id\":3").And.NotContain("\"Name\"");

        // Sync/async parity for the same bound value, plus an absent cancellation token on the sync twin.
        using var syncCompare = new MemoryStream();
        _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .WriteJson(syncCompare, new JsonStreamOptions(), TestContext.Current.CancellationToken, 2);
        using var asyncCompare = new MemoryStream();
        await _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .WriteJsonAsync(asyncCompare, new JsonStreamOptions(), TestContext.Current.CancellationToken, 2);
        asyncCompare.ToArray().Should().Equal(syncCompare.ToArray());

        using var asyncAbsent = new MemoryStream();
        var absentBuilder = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == 2);
        await WriteJsonWithoutToken(absentBuilder, asyncAbsent);
        asyncAbsent.ToArray().Should().Equal(syncCompare.ToArray());
    }

    // A2/E177-14 / D178 (live): a pre-cancelled native params call must not execute and must not write
    // any output on either surface.
    [Fact]
    public async Task NativeParams_PreCancelled_ShouldNotExecuteOrWriteOutput()
    {
        EnsureNativeTable();
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var command = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0))
            .Select(x => new { x.Id, x.Name });
        using var syncBuffer = new MemoryStream();
        var syncAct = () => command.WriteJson(syncBuffer, new JsonStreamOptions(), cts.Token, 1);
        syncAct.Should().Throw<OperationCanceledException>();
        syncBuffer.Length.Should().Be(0, "a pre-cancelled call must not execute or write output");

        var builder = _sut.DataProvider.From<INativeJsonUnicodeEntity>()
            .Where(x => x.Id == SqlFunctions.Parameter<int>(0));
        using var asyncBuffer = new MemoryStream();
        var asyncAct = async () => await builder.WriteJsonAsync(asyncBuffer, new JsonStreamOptions(), cts.Token, 1);
        await asyncAct.Should().ThrowAsync<OperationCanceledException>();
        asyncBuffer.Length.Should().Be(0, "a pre-cancelled async call must not execute or write output");
    }

    // Invokes the private DataContext.PrepareJsonStream guard-bearing selection seam so a test can assert
    // the actual native-vs-managed route without opening a connection; the named tuple's Item4 is the
    // native flag (mirrors SqlServerNativeJsonSqlTests.PrepareJsonStream).
    private static bool IsNativeRoute<TResult>(DataContext context, QueryCommand<TResult> command, JsonStreamOptions options)
    {
        var method = typeof(DataContext)
            .GetMethod("PrepareJsonStream", BindingFlags.Instance | BindingFlags.NonPublic)!
            .MakeGenericMethod(typeof(TResult));
        var result = method.Invoke(context, [command, options, CancellationToken.None])!;
        return (bool)result.GetType().GetField("Item4")!.GetValue(result)!;
    }

    // xUnit1051 rejects a tokenless WriteJsonAsync inside a test body, but the absent-cancellation default
    // is exactly what these tests exercise; the calls live in plain helpers the analyzer does not scan.
    private static Task WriteJsonWithoutToken<TResult>(QueryCommand<TResult> command, Stream destination)
        => command.WriteJsonAsync(destination);

    private static Task WriteJsonWithoutToken<TEntity>(EntityBuilder<TEntity> builder, Stream destination)
        => builder.WriteJsonAsync(destination);

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
