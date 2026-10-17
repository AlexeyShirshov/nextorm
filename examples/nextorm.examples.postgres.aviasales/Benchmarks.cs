using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.PostgreSQL;
using NextORM.Core;
using NextORM.Postgres;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// BenchmarkDotNet comparison of the two arms. The database is provisioned once by the example
/// (<see cref="Program"/> sets <see cref="ConnectionString"/> before the run) and both contexts are
/// opened in <see cref="Setup"/>, so the measured code is only "build + execute + materialise the
/// rows": no printing, no JSON serialization, no container work.
/// </summary>
[MemoryDiagnoser]
public class AviasalesBenchmarks
{
    public static string ConnectionString { get; set; } = "";

    private PostgresDataContext _nextorm = null!;
    private DataConnection _linq2db = null!;

    [GlobalSetup]
    public void Setup()
    {
        _nextorm = new PostgresDataContext(ConnectionString, new DataContextBuilder());
        _linq2db = new DataConnection(new DataOptions()
            .UsePostgreSQL(ConnectionString, PostgreSQLVersion.v15));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nextorm.Dispose();
        _linq2db.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task Nextorm_AircraftDelayChains() => await AviasalesQueries.AircraftDelayChains(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_AircraftDelayChains() => await Linq2DbQueries.AircraftDelayChains(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_BusinessOccupancyMatrix() => await AviasalesQueries.BusinessOccupancyMatrix(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_BusinessOccupancyMatrix() => await Linq2DbQueries.BusinessOccupancyMatrix(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_PassengerNoShowAnalysis() => await AviasalesQueries.PassengerNoShowAnalysis(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_PassengerNoShowAnalysis() => await Linq2DbQueries.PassengerNoShowAnalysis(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_RollingRevenueMetrics() => await AviasalesQueries.RollingRevenueMetrics(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_RollingRevenueMetrics() => await Linq2DbQueries.RollingRevenueMetrics(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_RouteNetworkAbcXyz() => await AviasalesQueries.RouteNetworkAbcXyz(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_RouteNetworkAbcXyz() => await Linq2DbQueries.RouteNetworkAbcXyz(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_TopRoutesByCity() => await AviasalesQueries.TopRoutesByCity(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_TopRoutesByCity() => await Linq2DbQueries.TopRoutesByCity(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_AirportOnTimePerformance() => await AviasalesQueries.AirportOnTimePerformance(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_AirportOnTimePerformance() => await Linq2DbQueries.AirportOnTimePerformance(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_DelayPercentilesByModel() => await AviasalesQueries.DelayPercentilesByModel(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_DelayPercentilesByModel() => await Linq2DbQueries.DelayPercentilesByModel(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_FrequentFlyers() => await AviasalesQueries.FrequentFlyers(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_FrequentFlyers() => await Linq2DbQueries.FrequentFlyers(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_PassengerGrowth() => await AviasalesQueries.PassengerGrowth(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_PassengerGrowth() => await Linq2DbQueries.PassengerGrowth(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_CancellationByRoute() => await AviasalesQueries.CancellationByRoute(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_CancellationByRoute() => await Linq2DbQueries.CancellationByRoute(_linq2db, default);
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
