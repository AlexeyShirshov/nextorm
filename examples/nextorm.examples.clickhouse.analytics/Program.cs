using BenchmarkDotNet.Running;
using LinqToDB;
using LinqToDB.Data;
using LinqToDB.DataProvider.ClickHouse;
using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Examples.ClickHouse.Analytics;

if (args.Contains("--benchmark"))
{
    await RunBenchmarks(args);
    return;
}

await RunComparison(args);
return;

static async Task RunComparison(string[] args)
{
    var connectionString = ParseConnectionString(args);
    await using var database = await DemoDatabase.Start(connectionString, CancellationToken.None);

    await using var ctx = new ClickHouseDataContext(database.ConnectionString, new DataContextBuilder());
    using var l2db = new DataConnection(new DataOptions()
        .UseClickHouse(database.ConnectionString, ClickHouseProvider.ClickHouseDriver));

    var queries = new (string Name, Func<CancellationToken, Task<object>> Nextorm, Func<CancellationToken, Task<object>> Linq2Db)[]
    {
        ("ArrayAnalytics", ct => ClickHouseQueries.ArrayAnalytics(ctx, ct), ct => Linq2DbQueries.ArrayAnalytics(l2db, ct)),
        ("Funnel", ct => ClickHouseQueries.Funnel(ctx, ct), ct => Linq2DbQueries.Funnel(l2db, ct)),
        ("Incremental", ct => ClickHouseQueries.Incremental(ctx, ct), ct => Linq2DbQueries.Incremental(l2db, ct)),
        ("Retention", ct => ClickHouseQueries.Retention(ctx, ct), ct => Linq2DbQueries.Retention(l2db, ct)),
        ("Sessions", ct => ClickHouseQueries.Sessions(ctx, ct), ct => Linq2DbQueries.Sessions(l2db, ct)),
        ("DailyTraffic", ct => ClickHouseQueries.DailyTraffic(ctx, ct), ct => Linq2DbQueries.DailyTraffic(l2db, ct)),
        ("TopLandingPages", ct => ClickHouseQueries.TopLandingPages(ctx, ct), ct => Linq2DbQueries.TopLandingPages(l2db, ct)),
        ("DeviceSplit", ct => ClickHouseQueries.DeviceSplit(ctx, ct), ct => Linq2DbQueries.DeviceSplit(l2db, ct)),
        ("TopReferrers", ct => ClickHouseQueries.TopReferrers(ctx, ct), ct => Linq2DbQueries.TopReferrers(l2db, ct)),
        ("SessionDepth", ct => ClickHouseQueries.SessionDepth(ctx, ct), ct => Linq2DbQueries.SessionDepth(l2db, ct)),
        ("RollingActivity", ct => ClickHouseQueries.RollingActivity(ctx, ct), ct => Linq2DbQueries.RollingActivity(l2db, ct)),
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
    var connectionString = ParseConnectionString(args);
    await using var database = await DemoDatabase.Start(connectionString, CancellationToken.None);

    ClickHouseBenchmarks.ConnectionString = database.ConnectionString;
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
        BenchmarkRunner.Run<ClickHouseBenchmarks>(new ExampleBenchmarkConfig());
    else
        BenchmarkSwitcher.FromTypes([typeof(ClickHouseBenchmarks)]).Run([.. bdnArgs], new ExampleBenchmarkConfig());
}

static string? ParseConnectionString(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--connection" or "-c")
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
