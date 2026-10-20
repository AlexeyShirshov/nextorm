using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.ClickHouse;
using NextORM.ClickHouse;
using NextORM.Core;

namespace NextORM.Examples.ClickHouse.Analytics;

/// <summary>
/// BenchmarkDotNet comparison of the two arms over <c>datasets.hits_v1</c>. The database is provisioned
/// once by the example (<see cref="Program"/> sets <see cref="ConnectionString"/>), both contexts are
/// opened in <see cref="Setup"/>, and the measured code only builds, executes and materialises rows.
/// <c>Incremental</c> has no nextorm LINQ surface and is benchmarked on the linq2db arm only.
/// </summary>
[MemoryDiagnoser]
public class ClickHouseBenchmarks
{
    public static string ConnectionString { get; set; } = "";

    private ClickHouseDataContext _nextorm = null!;
    private DataConnection _linq2db = null!;

    [GlobalSetup]
    public void Setup()
    {
        _nextorm = new ClickHouseDataContext(ConnectionString, new DataContextBuilder());
        _linq2db = new DataConnection(new DataOptions()
            .UseClickHouse(ConnectionString, ClickHouseProvider.ClickHouseDriver));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nextorm.Dispose();
        _linq2db.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task Nextorm_ArrayAnalytics() => await ClickHouseQueries.ArrayAnalytics(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_ArrayAnalytics() => await Linq2DbQueries.ArrayAnalytics(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_Funnel() => await ClickHouseQueries.Funnel(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_Funnel() => await Linq2DbQueries.Funnel(_linq2db, default);

    [Benchmark]
    public async Task Linq2Db_Incremental() => await Linq2DbQueries.Incremental(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_Retention() => await ClickHouseQueries.Retention(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_Retention() => await Linq2DbQueries.Retention(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_Sessions() => await ClickHouseQueries.Sessions(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_Sessions() => await Linq2DbQueries.Sessions(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_DailyTraffic() => await ClickHouseQueries.DailyTraffic(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_DailyTraffic() => await Linq2DbQueries.DailyTraffic(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_TopLandingPages() => await ClickHouseQueries.TopLandingPages(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_TopLandingPages() => await Linq2DbQueries.TopLandingPages(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_DeviceSplit() => await ClickHouseQueries.DeviceSplit(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_DeviceSplit() => await Linq2DbQueries.DeviceSplit(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_TopReferrers() => await ClickHouseQueries.TopReferrers(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_TopReferrers() => await Linq2DbQueries.TopReferrers(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_SessionDepth() => await ClickHouseQueries.SessionDepth(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_SessionDepth() => await Linq2DbQueries.SessionDepth(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_RollingActivity() => await ClickHouseQueries.RollingActivity(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_RollingActivity() => await Linq2DbQueries.RollingActivity(_linq2db, default);
}

/// <summary>
/// Short in-process run by default (warmup 3 / iterations 3, no generated project or child process).
/// Set <c>NEXTORM_EXAMPLES_BENCH_FULL=1</c> for the full out-of-process BenchmarkDotNet job.
/// </summary>
public sealed class ExampleBenchmarkConfig : ManualConfig
{
    public ExampleBenchmarkConfig()
    {
        AddColumnProvider(DefaultColumnProviders.Instance);
        AddExporter(MarkdownExporter.GitHub);
        AddLogger(ConsoleLogger.Default);

        AddJob(Environment.GetEnvironmentVariable("NEXTORM_EXAMPLES_BENCH_FULL") == "1"
            ? Job.Default
            : Job.ShortRun.WithToolchain(InProcessEmitToolchain.Instance));
    }
}
