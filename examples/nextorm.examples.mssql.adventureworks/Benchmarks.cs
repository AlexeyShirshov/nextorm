using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using NextORM.Core;
using NextORM.SqlServer;

namespace NextORM.Examples.SqlServer.AdventureWorks;

/// <summary>
/// BenchmarkDotNet comparison of the two arms over the AdventureWorks database. The database is
/// provisioned once by the example (<see cref="Program"/> sets <see cref="ConnectionString"/>), both
/// contexts are opened in <see cref="Setup"/>, and the measured code only builds, executes and
/// materialises the rows.
/// </summary>
[MemoryDiagnoser]
public class AdventureWorksBenchmarks
{
    public static string ConnectionString { get; set; } = "";

    private SqlServerDataContext _nextorm = null!;
    private DataConnection _linq2db = null!;

    [GlobalSetup]
    public void Setup()
    {
        _nextorm = new SqlServerDataContext(ConnectionString, new DataContextBuilder());
        _linq2db = new DataConnection(new DataOptions()
            .UseSqlServer(ConnectionString, SqlServerVersion.v2022, SqlServerProvider.MicrosoftDataSqlClient));
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _nextorm.Dispose();
        _linq2db.Dispose();
    }

    [Benchmark(Baseline = true)]
    public async Task Nextorm_VipChurn() => await AdventureWorksQueries.VipChurn(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_VipChurn() => await Linq2DbQueries.VipChurn(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_RollingKpi() => await AdventureWorksQueries.RollingKpi(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_RollingKpi() => await Linq2DbQueries.RollingKpi(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_SupplyChain() => await AdventureWorksQueries.SupplyChain(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_SupplyChain() => await Linq2DbQueries.SupplyChain(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_ProductAbcXyz() => await AdventureWorksQueries.ProductAbcXyz(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_ProductAbcXyz() => await Linq2DbQueries.ProductAbcXyz(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_QuarterlyPivot() => await AdventureWorksQueries.QuarterlyPivot(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_QuarterlyPivot() => await Linq2DbQueries.QuarterlyPivot(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_TopProductsByCategory() => await AdventureWorksQueries.TopProductsByCategory(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_TopProductsByCategory() => await Linq2DbQueries.TopProductsByCategory(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_TerritoryYearOverYear() => await AdventureWorksQueries.TerritoryYearOverYear(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_TerritoryYearOverYear() => await Linq2DbQueries.TerritoryYearOverYear(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_CustomerRfm() => await AdventureWorksQueries.CustomerRfm(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_CustomerRfm() => await Linq2DbQueries.CustomerRfm(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_QuotaAttainment() => await AdventureWorksQueries.QuotaAttainment(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_QuotaAttainment() => await Linq2DbQueries.QuotaAttainment(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_TerritoryGrowthMonthOverMonth() => await AdventureWorksQueries.TerritoryGrowthMonthOverMonth(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_TerritoryGrowthMonthOverMonth() => await Linq2DbQueries.TerritoryGrowthMonthOverMonth(_linq2db, default);

    [Benchmark]
    public async Task Nextorm_CustomerPareto() => await AdventureWorksQueries.CustomerPareto(_nextorm, default);

    [Benchmark]
    public async Task Linq2Db_CustomerPareto() => await Linq2DbQueries.CustomerPareto(_linq2db, default);
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
