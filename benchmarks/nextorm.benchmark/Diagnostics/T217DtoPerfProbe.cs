using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using NextORM.ClickHouse;
using NextORM.Core;
using Testcontainers.ClickHouse;

namespace NextORM.Benchmark;

[SqlTable("t217_perf")]
public interface IT217PerfDateTime
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("ts")]
    DateTime Ts { get; set; }
}

[SqlTable("t217_perf")]
public interface IT217PerfDto
{
    [Key]
    [Column("id")]
    int Id { get; set; }

    [Column("ts")]
    DateTimeOffset Ts { get; set; }
}

/// <summary>
/// T217 (#217) E217-14 bounded perf probe. It reads one identical ClickHouse
/// <c>DateTime64(6, 'UTC')</c> data set twice over the same rows: once projected as a
/// <see cref="DateTime"/> scalar (the base mapper) and once as a <see cref="DateTimeOffset"/> (the
/// T217 DTO branch of <c>ClickHouseDataContext.MapColumnExpression</c>). After a warm-up it records
/// row count, wall time and allocated bytes per measured batch and writes TSV/environment files.
/// It records measured numbers only and draws no performance conclusion.
/// </summary>
internal static class T217DtoPerfProbe
{
    private const int RowCount = 200_000;
    private const int WarmupBatches = 3;
    private const int MeasuredBatches = 5;

    public static async Task<int> RunAsync(string outDir)
    {
        Directory.CreateDirectory(outDir);

        var container = new ClickHouseBuilder("clickhouse/clickhouse-server:25.8-alpine")
            .WithEnvironment("TZ", "UTC")
            .WithReuse(true)
            .WithLabel("reuse-id", "nextorm-clickhouse")
            .Build();

        Console.WriteLine("starting/reusing the ClickHouse test container...");
        await container.StartAsync();
        var connectionString = container.GetConnectionString();

        using var ctx = new ClickHouseDataContext(connectionString, new DataContextBuilder());
        Exec(ctx, "drop table if exists t217_perf");
        Exec(ctx, "create table t217_perf (id Int32, ts DateTime64(6, 'UTC')) engine = Memory");
        Exec(
            ctx,
            $"insert into t217_perf (id, ts) select toInt32(number), toDateTime64(number, 6, 'UTC') from numbers({RowCount})");

        Func<int> readDateTime = () => ReadDateTime(ctx);
        Func<int> readDto = () => ReadDto(ctx);

        for (var i = 0; i < WarmupBatches; i++)
        {
            _ = readDateTime();
            _ = readDto();
        }

        var rows = new List<(string Kind, int Batch, int Rows, double Ms, long Alloc)>();
        for (var batch = 1; batch <= MeasuredBatches; batch++)
        {
            // Alternate the order so neither kind always reads against a warmer cache.
            if (batch % 2 == 1)
            {
                rows.Add(Measure("datetime", batch, readDateTime));
                rows.Add(Measure("dto", batch, readDto));
            }
            else
            {
                rows.Add(Measure("dto", batch, readDto));
                rows.Add(Measure("datetime", batch, readDateTime));
            }
        }

        var tsv = Path.Combine(outDir, "perf.tsv");
        using (var writer = new StreamWriter(tsv, false, new UTF8Encoding(false)))
        {
            writer.WriteLine("kind\tbatch\trows\telapsed_ms\tallocated_bytes");
            foreach (var r in rows)
            {
                writer.WriteLine(
                    $"{r.Kind}\t{r.Batch}\t{r.Rows}\t{r.Ms.ToString("F3", CultureInfo.InvariantCulture)}\t{r.Alloc}");
            }
        }

        var summary = Path.Combine(outDir, "summary.tsv");
        using (var writer = new StreamWriter(summary, false, new UTF8Encoding(false)))
        {
            writer.WriteLine(
                "kind\tbatches\tmin_elapsed_ms\tmedian_elapsed_ms\tmax_elapsed_ms\tmin_alloc_bytes\tmedian_alloc_bytes\tmax_alloc_bytes");
            foreach (var kind in new[] { "datetime", "dto" })
            {
                var mine = rows.Where(r => r.Kind == kind).ToList();
                var ms = mine.Select(r => r.Ms).OrderBy(x => x).ToList();
                var alloc = mine.Select(r => (double)r.Alloc).OrderBy(x => x).ToList();
                writer.WriteLine(
                    $"{kind}\t{mine.Count}\t{ms[0].ToString("F3", CultureInfo.InvariantCulture)}\t{Median(ms).ToString("F3", CultureInfo.InvariantCulture)}\t{ms[^1].ToString("F3", CultureInfo.InvariantCulture)}\t" +
                    $"{alloc[0].ToString("F0", CultureInfo.InvariantCulture)}\t{Median(alloc).ToString("F0", CultureInfo.InvariantCulture)}\t{alloc[^1].ToString("F0", CultureInfo.InvariantCulture)}");
            }
        }

        var env = Path.Combine(outDir, "environment.txt");
        File.WriteAllLines(env, new[]
        {
            $"timestamp_utc={DateTime.UtcNow:O}",
            $"framework={RuntimeInformation.FrameworkDescription}",
            $"os={RuntimeInformation.OSDescription}",
            $"process_arch={RuntimeInformation.ProcessArchitecture}",
            $"processor_count={Environment.ProcessorCount}",
            "container_image=clickhouse/clickhouse-server:25.8-alpine",
            $"clickhouse_connection={connectionString}",
            $"row_count={RowCount}",
            $"warmup_batches={WarmupBatches}",
            $"measured_batches={MeasuredBatches}",
            "projection_datetime=IT217PerfDateTime.Ts (base DateTime scalar mapper)",
            "projection_dto=IT217PerfDto.Ts (T217 ClickHouseDataContext.MapColumnExpression DTO branch)",
            "note=with/without DTO branch on identical data is not runnable (the generic typed reader throws for a DateTimeOffset projection); the recorded comparison is DTO vs DateTime reading over identical rows",
        });

        Console.WriteLine($"wrote {tsv}, {summary}, {env}");
        return 0;
    }

    private static (string Kind, int Batch, int Rows, double Ms, long Alloc) Measure(
        string kind,
        int batch,
        Func<int> enumerate)
    {
        var allocBefore = GC.GetAllocatedBytesForCurrentThread();
        var stopwatch = Stopwatch.StartNew();
        var rows = enumerate();
        stopwatch.Stop();
        var alloc = GC.GetAllocatedBytesForCurrentThread() - allocBefore;
        return (kind, batch, rows, stopwatch.Elapsed.TotalMilliseconds, alloc);
    }

    private static int ReadDateTime(IDataContext ctx) =>
        ctx.From<IT217PerfDateTime>().Select(x => x.Ts).ToList().Count;

    private static int ReadDto(IDataContext ctx) =>
        ctx.From<IT217PerfDto>().Select(x => x.Ts).ToList().Count;

    private static double Median(List<double> sorted) => sorted.Count == 0 ? 0 : sorted[sorted.Count / 2];

    private static void Exec(IDataContext ctx, string sql)
    {
        using (ctx.ExecuteRaw(sql))
        {
        }
    }
}
