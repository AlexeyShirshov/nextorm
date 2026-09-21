using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Examples.ClickHouse.Analytics;

var connectionString = ParseConnectionString(args);
await using var database = await DemoDatabase.Start(connectionString, CancellationToken.None);

await using var ctx = new ClickHouseDataContext(database.ConnectionString, new DataContextBuilder());

var results = new List<(string Name, Exception? Error)>();

await Run("ArrayAnalytics", () => ClickHouseQueries.ArrayAnalytics(ctx, CancellationToken.None));
await Run("Funnel", () => ClickHouseQueries.Funnel(ctx, CancellationToken.None));
await Run("Incremental", () => ClickHouseQueries.Incremental(ctx, CancellationToken.None));
await Run("Retention", () => ClickHouseQueries.Retention(ctx, CancellationToken.None));
await Run("Sessions", () => ClickHouseQueries.Sessions(ctx, CancellationToken.None));
await Run("DailyTraffic", () => ClickHouseQueries.DailyTraffic(ctx, CancellationToken.None));
await Run("TopLandingPages", () => ClickHouseQueries.TopLandingPages(ctx, CancellationToken.None));
await Run("DeviceSplit", () => ClickHouseQueries.DeviceSplit(ctx, CancellationToken.None));
await Run("TopReferrers", () => ClickHouseQueries.TopReferrers(ctx, CancellationToken.None));
await Run("SessionDepth", () => ClickHouseQueries.SessionDepth(ctx, CancellationToken.None));
await Run("RollingActivity", () => ClickHouseQueries.RollingActivity(ctx, CancellationToken.None));

Console.WriteLine();
Console.WriteLine($"{results.Count(r => r.Error is null)}/{results.Count} queries succeeded; " +
                  $"{results.Count(r => r.Error is not null)} not working (see README).");

async Task Run(string name, Func<Task> query)
{
    try
    {
        await query();
        results.Add((name, null));
        Console.WriteLine($"[ OK ] {name}");
    }
    catch (Exception ex) when (ex is NotSupportedException or QueryPreparationException)
    {
        results.Add((name, ex));
        Console.WriteLine($"[FAIL] {name}: {ex.GetType().Name}: {ex.Message}");
    }
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
