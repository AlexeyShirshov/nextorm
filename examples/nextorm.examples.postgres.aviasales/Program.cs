using BenchmarkDotNet.Running;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.PostgreSQL;
using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Examples.Postgres.Aviasales;
using NextORM.Postgres;

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

    await using var ctx = new PostgresDataContext(database.ConnectionString, contextBuilder);
    using var l2db = new DataConnection(new DataOptions()
        .UsePostgreSQL(database.ConnectionString, PostgreSQLVersion.v15));

    var queries = new (string Name, Func<CancellationToken, Task<object>> Nextorm, Func<CancellationToken, Task<object>> Linq2Db)[]
    {
        ("AircraftDelayChains", ct => AviasalesQueries.AircraftDelayChains(ctx, ct), ct => Linq2DbQueries.AircraftDelayChains(l2db, ct)),
        ("BusinessOccupancyMatrix", ct => AviasalesQueries.BusinessOccupancyMatrix(ctx, ct), ct => Linq2DbQueries.BusinessOccupancyMatrix(l2db, ct)),
        ("PassengerNoShowAnalysis", ct => AviasalesQueries.PassengerNoShowAnalysis(ctx, ct), ct => Linq2DbQueries.PassengerNoShowAnalysis(l2db, ct)),
        ("RollingRevenueMetrics", ct => AviasalesQueries.RollingRevenueMetrics(ctx, ct), ct => Linq2DbQueries.RollingRevenueMetrics(l2db, ct)),
        ("RouteNetworkAbcXyz", ct => AviasalesQueries.RouteNetworkAbcXyz(ctx, ct), ct => Linq2DbQueries.RouteNetworkAbcXyz(l2db, ct)),
        ("TopRoutesByCity", ct => AviasalesQueries.TopRoutesByCity(ctx, ct), ct => Linq2DbQueries.TopRoutesByCity(l2db, ct)),
        ("AirportOnTimePerformance", ct => AviasalesQueries.AirportOnTimePerformance(ctx, ct), ct => Linq2DbQueries.AirportOnTimePerformance(l2db, ct)),
        ("DelayPercentilesByModel", ct => AviasalesQueries.DelayPercentilesByModel(ctx, ct), ct => Linq2DbQueries.DelayPercentilesByModel(l2db, ct)),
        ("FrequentFlyers", ct => AviasalesQueries.FrequentFlyers(ctx, ct), ct => Linq2DbQueries.FrequentFlyers(l2db, ct)),
        ("PassengerGrowth", ct => AviasalesQueries.PassengerGrowth(ctx, ct), ct => Linq2DbQueries.PassengerGrowth(l2db, ct)),
        ("CancellationByRoute", ct => AviasalesQueries.CancellationByRoute(ctx, ct), ct => Linq2DbQueries.CancellationByRoute(l2db, ct)),
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

    AviasalesBenchmarks.ConnectionString = database.ConnectionString;
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
        BenchmarkRunner.Run<AviasalesBenchmarks>(new ExampleBenchmarkConfig());
    else
        BenchmarkSwitcher.FromTypes([typeof(AviasalesBenchmarks)]).Run([.. bdnArgs], new ExampleBenchmarkConfig());
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
