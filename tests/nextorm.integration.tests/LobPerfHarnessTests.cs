using System.Diagnostics;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Deterministic allocation harness for the streaming LOB terminals (<c>ToStream</c>/<c>ToTextReader</c>)
/// against the buffered materialization path. It is the fallback agreed in the plan because the
/// benchmark project only references core + SQLite; adding PostgreSQL/SQL Server (and their drivers
/// plus Testcontainers) there is disproportionate. The harness runs on the integration providers that
/// implement streaming, inserts a 1 MiB row, then reports the best-of-N
/// <see cref="GC.GetAllocatedBytesForCurrentThread"/> delta for each path.
/// </summary>
[Collection("Sqlite")]
public sealed class LobPerfHarnessTests
{
    private const int MiB = 1024 * 1024;
    private const int StreamBufferBytes = 64 * 1024;
    private const int Samples = 3;

    private static string OutputPath =>
        Environment.GetEnvironmentVariable("NEXTORM_LOB_PERF_LOG") ?? "/tmp/lob-perf-lob.log";

    [Fact]
    public void Measure_Streaming_Vs_Buffered_Lob_Allocations()
    {
        // Out-of-band perf harness: run it explicitly (and only then) so the regular integration run
        // does not pay for the extra 1 MiB seed and the repeated 8 MiB reads.
        Assert.SkipUnless(
            Environment.GetEnvironmentVariable("NEXTORM_LOB_PERF") == "1",
            "Set NEXTORM_LOB_PERF=1 to run the LOB allocation harness.");

        var lines = new List<string>
        {
            $"# LOB allocation harness; runtime {Environment.Version}; host {Environment.MachineName}",
        };

        foreach (var provider in new ITestProvider[] { PostgresTestProvider.Instance, SqlServerTestProvider.Instance, SqliteTestProvider.Instance })
        {
            if (!provider.IsAvailable)
            {
                lines.Add($"[{provider.Name}] SKIP: {provider.SkipReason}");
                continue;
            }

            provider.EnsureSeeded();
            using var ctx = provider.CreateContext();
            SeedSmallBlob(ctx);

            var small = Measure(lines, provider.Name, ctx, rowId: 2, sizeBytes: MiB);
            var large = Measure(lines, provider.Name, ctx, rowId: 1, sizeBytes: 8 * MiB);

            // Assertion (not just a report): sequential streaming is O(buffer), so its allocation must
            // not grow with the value, while the buffered path must grow with it. This distinguishes
            // streaming from buffered; the gated run is the only place it is unavoidable to assert it.
            AssertBounded(provider.Name, "BLOB streaming", small.BlobStream, large.BlobStream);
            AssertBounded(provider.Name, "BLOB named-column streaming", small.BlobNamedStream, large.BlobNamedStream);
            AssertBounded(provider.Name, "CLOB streaming", small.TextStream, large.TextStream);
            AssertGrows(provider.Name, "BLOB buffered", small.BlobBuffered, large.BlobBuffered);
            AssertGrows(provider.Name, "CLOB buffered", small.TextBuffered, large.TextBuffered);

            lines.Add(
                $"[{provider.Name}] VERDICT streaming ratio BLOB={GrowthRatio(small.BlobStream, large.BlobStream):F2} " +
                $"named={GrowthRatio(small.BlobNamedStream, large.BlobNamedStream):F2} " +
                $"CLOB={GrowthRatio(small.TextStream, large.TextStream):F2}; " +
                $"buffered ratio BLOB={GrowthRatio(small.BlobBuffered, large.BlobBuffered):F2} " +
                $"CLOB={GrowthRatio(small.TextBuffered, large.TextBuffered):F2}");
        }

        File.AppendAllLines(OutputPath, lines);
    }

    /// <summary>
    /// Asserts that a streaming read stays bounded by its read buffer: the 1 -> 8 MiB allocation must
    /// not grow proportionally (a small factor absorbs driver/reader overhead and measurement noise).
    /// </summary>
    private static void AssertBounded(string provider, string label, long small, long large)
    {
        var ratio = GrowthRatio(small, large);
        ratio.Should().BeLessThanOrEqualTo(
            2.5,
            $"[{provider}] {label} must stay bounded by the read buffer, but grew {ratio:F2}x when the value grew 1->8 MiB ({small} B -> {large} B)");
    }

    /// <summary>
    /// Asserts that a buffered read grows with the value: 1 -> 8 MiB must show at least a 4x allocation
    /// increase (a full materialization is ~8x).
    /// </summary>
    private static void AssertGrows(string provider, string label, long small, long large)
    {
        var ratio = GrowthRatio(small, large);
        ratio.Should().BeGreaterThanOrEqualTo(
            4.0,
            $"[{provider}] {label} must grow with the value, but only grew {ratio:F2}x when the value grew 1->8 MiB ({small} B -> {large} B)");
    }

    private static double GrowthRatio(long small, long large)
        => small > 0 ? (double)large / small : double.NaN;

    private static void SeedSmallBlob(IDataContext ctx)
    {
        using (ctx.ExecuteRaw("delete from lob_entity where id = @id", [new ProcedureParameter("id", 2)]))
        {
        }

        using (ctx.ExecuteRaw(
            "insert into lob_entity (id, data, body) values (@id, @data, @body)",
            [
                new ProcedureParameter("id", 2),
                new ProcedureParameter("data", new byte[MiB]),
                new ProcedureParameter("body", new string('x', MiB)),
            ]))
        {
        }
    }

    private static Allocations Measure(List<string> lines, string provider, IDataContext ctx, int rowId, int sizeBytes)
    {
        var blobStream = MeasureAlloc(() =>
        {
            using var stream = ctx.From<LobEntity>().Where(it => it.Id == rowId).Select(it => it.Data!).ToStream();
            var buffer = new byte[StreamBufferBytes];
            var total = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                total += read;

            total.Should().Be(sizeBytes);
        });

        var blobBuffered = MeasureAlloc(() =>
        {
            var data = ctx.From<LobEntity>().Where(it => it.Id == rowId).Select(it => it.Data!).First()!;
            data.Length.Should().Be(sizeBytes);
        });

        // The named-column path selects the same payload through the TableAlias streaming accessor,
        // which on SQLite also appends the trailing rowid locator.
        var blobNamedStream = MeasureAlloc(() =>
        {
            using var stream = ctx
                .From("lob_entity")
                .Where(t => t.GetInt32("id") == rowId)
                .Select(t => t.GetStream("data"))
                .ToStream();
            var buffer = new byte[StreamBufferBytes];
            var total = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                total += read;

            total.Should().Be(sizeBytes);
        });

        var textStream = MeasureAlloc(() =>
        {
            using var reader = ctx.From<LobEntity>().Where(it => it.Id == rowId).Select(it => it.Body!).ToTextReader();
            var buffer = new char[StreamBufferBytes];
            var total = 0;
            int read;
            while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                total += read;

            total.Should().Be(sizeBytes);
        });

        var textBuffered = MeasureAlloc(() =>
        {
            var text = ctx.From<LobEntity>().Where(it => it.Id == rowId).Select(it => it.Body!).First()!;
            text.Length.Should().Be(sizeBytes);
        });

        var tag = sizeBytes / MiB;
        lines.Add($"[{provider}] BLOB   {tag} MiB streaming = {blobStream.allocated,10} B  ({blobStream.elapsedMs:F1} ms min)");
        lines.Add($"[{provider}] BLOB   {tag} MiB buffered  = {blobBuffered.allocated,10} B  ({blobBuffered.elapsedMs:F1} ms min)");
        lines.Add($"[{provider}] BLOB   {tag} MiB named-streaming = {blobNamedStream.allocated,10} B  ({blobNamedStream.elapsedMs:F1} ms min)");
        lines.Add($"[{provider}] CLOB   {tag} MiB streaming = {textStream.allocated,10} B  ({textStream.elapsedMs:F1} ms min)");
        lines.Add($"[{provider}] CLOB   {tag} MiB buffered  = {textBuffered.allocated,10} B  ({textBuffered.elapsedMs:F1} ms min)");

        return new Allocations(
            blobStream.allocated,
            blobBuffered.allocated,
            blobNamedStream.allocated,
            textStream.allocated,
            textBuffered.allocated);
    }

    /// <summary>Best-of-<see cref="Samples"/> allocation deltas for the harness paths at one value size.</summary>
    private readonly record struct Allocations(
        long BlobStream,
        long BlobBuffered,
        long BlobNamedStream,
        long TextStream,
        long TextBuffered);

    private static (long allocated, double elapsedMs) MeasureAlloc(Action action)
    {
        // Warm up JIT, static initialization, plan building and the plan cache.
        action();
        action();

        var bestAlloc = long.MaxValue;
        var bestMs = double.MaxValue;
        for (var i = 0; i < Samples; i++)
        {
            var before = GC.GetAllocatedBytesForCurrentThread();
            var sw = Stopwatch.StartNew();
            action();
            sw.Stop();
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            if (allocated < bestAlloc)
                bestAlloc = allocated;
            if (sw.Elapsed.TotalMilliseconds < bestMs)
                bestMs = sw.Elapsed.TotalMilliseconds;
        }

        return (bestAlloc, bestMs);
    }
}
