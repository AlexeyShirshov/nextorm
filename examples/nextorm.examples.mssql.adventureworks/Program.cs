using BenchmarkDotNet.Running;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.SqlServer;
using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Examples.SqlServer.AdventureWorks;
using NextORM.SqlServer;

if (args.Contains("--benchmark"))
{
    await RunBenchmarks(args);
    return;
}

await RunComparison(args);
return;

static async Task RunComparison(string[] args)
{
    var connectionString = ParseConnectionString(args, "--connection", "-c");
    var logSql = args.Contains("--log-sql");

    await using var database = await DemoDatabase.Start(connectionString, CancellationToken.None);

    using var loggerFactory = logSql
        ? LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
        : null;

    var contextBuilder = new DataContextBuilder();
    if (loggerFactory is not null)
        contextBuilder.UseLoggerFactory(loggerFactory);

    await using var ctx = new SqlServerDataContext(database.ConnectionString, contextBuilder);
    using var l2db = new DataConnection(new DataOptions()
        .UseSqlServer(database.ConnectionString, SqlServerVersion.v2022, SqlServerProvider.MicrosoftDataSqlClient));

    var queries = new (string Name, Func<CancellationToken, Task<object>> Nextorm, Func<CancellationToken, Task<object>> Linq2Db)[]
    {
        ("VipChurn", ct => AdventureWorksQueries.VipChurn(ctx, ct), ct => Linq2DbQueries.VipChurn(l2db, ct)),
        ("RollingKpi", ct => AdventureWorksQueries.RollingKpi(ctx, ct), ct => Linq2DbQueries.RollingKpi(l2db, ct)),
        ("SupplyChain", ct => AdventureWorksQueries.SupplyChain(ctx, ct), ct => Linq2DbQueries.SupplyChain(l2db, ct)),
        ("ProductAbcXyz", ct => AdventureWorksQueries.ProductAbcXyz(ctx, ct), ct => Linq2DbQueries.ProductAbcXyz(l2db, ct)),
        ("QuarterlyPivot", ct => AdventureWorksQueries.QuarterlyPivot(ctx, ct), ct => Linq2DbQueries.QuarterlyPivot(l2db, ct)),
        ("TopProductsByCategory", ct => AdventureWorksQueries.TopProductsByCategory(ctx, ct), ct => Linq2DbQueries.TopProductsByCategory(l2db, ct)),
        ("TerritoryYearOverYear", ct => AdventureWorksQueries.TerritoryYearOverYear(ctx, ct), ct => Linq2DbQueries.TerritoryYearOverYear(l2db, ct)),
        ("CustomerRfm", ct => AdventureWorksQueries.CustomerRfm(ctx, ct), ct => Linq2DbQueries.CustomerRfm(l2db, ct)),
        ("QuotaAttainment", ct => AdventureWorksQueries.QuotaAttainment(ctx, ct), ct => Linq2DbQueries.QuotaAttainment(l2db, ct)),
        ("TerritoryGrowthMonthOverMonth", ct => AdventureWorksQueries.TerritoryGrowthMonthOverMonth(ctx, ct), ct => Linq2DbQueries.TerritoryGrowthMonthOverMonth(l2db, ct)),
        ("CustomerPareto", ct => AdventureWorksQueries.CustomerPareto(ctx, ct), ct => Linq2DbQueries.CustomerPareto(l2db, ct)),
    };

    var only = ParseList(args, "--only");
    if (only.Length > 0)
        queries = queries.Where(q => only.Contains(q.Name, StringComparer.OrdinalIgnoreCase)).ToArray();

    var passed = 0;
    foreach (var query in queries)
    {
        if (await Comparison.Run(query.Name, query.Nextorm, query.Linq2Db, CancellationToken.None))
            passed++;
    }

    Console.WriteLine();
    Console.WriteLine($"{passed}/{queries.Length} queries matched in both arms; " +
                      $"{queries.Length - passed} not working (see README).");
}

static async Task RunBenchmarks(string[] args)
{
    var connectionString = ParseConnectionString(args, "--connection", "-c");
    await using var database = await DemoDatabase.Start(connectionString, CancellationToken.None);

    AdventureWorksBenchmarks.ConnectionString = database.ConnectionString;
    var bdnArgs = new List<string>();
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--filter" or "--anyCategories" or "--allCategories")
        {
            bdnArgs.Add(args[i]);
            bdnArgs.Add(args[i + 1]);
        }
    }

    if (bdnArgs.Count == 0)
        BenchmarkRunner.Run<AdventureWorksBenchmarks>(new ExampleBenchmarkConfig());
    else
        BenchmarkSwitcher.FromTypes([typeof(AdventureWorksBenchmarks)]).Run([.. bdnArgs], new ExampleBenchmarkConfig());
}

static string? ParseConnectionString(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (names.Contains(args[i]))
            return args[i + 1];
    }

    return null;
}

static string[] ParseList(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] == name)
            return args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    return [];
}
