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

            Measure(lines, provider.Name, ctx, rowId: 2, sizeBytes: MiB);
            Measure(lines, provider.Name, ctx, rowId: 1, sizeBytes: 8 * MiB);
        }

        File.AppendAllLines(OutputPath, lines);
    }

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

    private static void Measure(List<string> lines, string provider, IDataContext ctx, int rowId, int sizeBytes)
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
        lines.Add($"[{provider}] CLOB   {tag} MiB streaming = {textStream.allocated,10} B  ({textStream.elapsedMs:F1} ms min)");
        lines.Add($"[{provider}] CLOB   {tag} MiB buffered  = {textBuffered.allocated,10} B  ({textBuffered.elapsedMs:F1} ms min)");
    }

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
