using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using NextORM.Core;
using NextORM.Sqlite;
using IDataContext = NextORM.Core.IDataContext;

namespace NextORM.Benchmark;

/// <summary>Compares genuine SQLite async streaming into a class and readonly-struct constructor projection.</summary>
[MemoryDiagnoser]
[Config(typeof(SqliteStreamProjectionConfig))]
public class SqliteBenchmarkStreamProjection
{
    private IDataContext _db = null!;
    private TestDataRepository _ctx = null!;

    [Params(1_000, 10_000)]
    public int Rows { get; set; }

    [GlobalSetup]
    public async Task Setup()
    {
        var builder = new DataContextBuilder();
        builder.UseSqlite(BenchDb.FilePath);
        _db = builder.CreateDataContext();
        _ctx = new TestDataRepository(_db);
        ((IConnectionManager)_db).EnsureConnectionOpen();

        var classQuery = _ctx.LargeEntity.Where(e => e.Id <= Rows)
            .Select(e => new ClassProjection(e.Id)).Prepare(new PrepareOptions { Streaming = true });
        var structQuery = _ctx.LargeEntity.Where(e => e.Id <= Rows)
            .Select(e => new StructProjection(e.Id)).Prepare(new PrepareOptions { Streaming = true });

        var classResult = await Consume(classQuery);
        var structResult = await Consume(structQuery);
        var expected = (Count: Rows, Checksum: (long)Rows * (Rows + 1) / 2);
        BenchmarkComparisonValidation.EnsureCount(expected.Count, classResult.Count, "StreamProjection/class");
        BenchmarkComparisonValidation.EnsureChecksum(expected.Checksum, classResult.Checksum, "StreamProjection/class");
        BenchmarkComparisonValidation.EnsureCount(expected.Count, structResult.Count, "StreamProjection/struct");
        BenchmarkComparisonValidation.EnsureChecksum(expected.Checksum, structResult.Checksum, "StreamProjection/struct");
        BenchmarkComparisonValidation.Report("StreamProjection/class", classResult.Count, classResult.Checksum);
        BenchmarkComparisonValidation.Report("StreamProjection/struct", structResult.Count, structResult.Checksum);
    }

    [Benchmark(Baseline = true)]
    public Task<(int Count, long Checksum)> ClassProjectionStreaming()
    {
        var query = _ctx.LargeEntity.Where(e => e.Id <= Rows)
            .Select(e => new ClassProjection(e.Id)).Prepare(new PrepareOptions { Streaming = true });
        return Consume(query);
    }

    [Benchmark]
    public Task<(int Count, long Checksum)> ReadonlyStructProjectionStreaming()
    {
        var query = _ctx.LargeEntity.Where(e => e.Id <= Rows)
            .Select(e => new StructProjection(e.Id)).Prepare(new PrepareOptions { Streaming = true });
        return Consume(query);
    }

    private async Task<(int Count, long Checksum)> Consume<T>(IPreparedQueryCommand<T> query)
    {
        var count = 0;
        long checksum = 0;
        await foreach (var row in query.ToAsyncEnumerable(_db))
        {
            count++;
            checksum += row switch
            {
                ClassProjection c => c.Id,
                StructProjection s => s.Id,
                _ => throw new InvalidOperationException($"Unexpected projection type {typeof(T)}")
            };
        }
        return (count, checksum);
    }

    public sealed class ClassProjection(long id)
    {
        public long Id { get; } = id;
    }

    public readonly struct StructProjection(long id)
    {
        public long Id { get; } = id;
    }
}

internal sealed class SqliteStreamProjectionConfig : ManualConfig
{
    public SqliteStreamProjectionConfig()
    {
        AddJob(Job.Default
            .WithToolchain(InProcessEmitToolchain.Instance)
            .WithWarmupCount(3)
            .WithIterationCount(5));
    }
}
