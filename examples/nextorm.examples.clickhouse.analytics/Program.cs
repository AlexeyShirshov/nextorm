using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Examples.ClickHouse.Analytics;

var connectionString = ParseConnectionString(args);
await using var database = await DemoDatabase.Start(connectionString, CancellationToken.None);

await using var ctx = new ClickHouseDataContext(database.ConnectionString, new DataContextBuilder());

await ClickHouseQueries.ArrayAnalytics(ctx, CancellationToken.None);
await ClickHouseQueries.Funnel(ctx, CancellationToken.None);
await ClickHouseQueries.Incremental(ctx, CancellationToken.None);
await ClickHouseQueries.Retention(ctx, CancellationToken.None);
await ClickHouseQueries.Sessions(ctx, CancellationToken.None);
await ClickHouseQueries.DailyTraffic(ctx, CancellationToken.None);
await ClickHouseQueries.TopLandingPages(ctx, CancellationToken.None);
await ClickHouseQueries.DeviceSplit(ctx, CancellationToken.None);
await ClickHouseQueries.TopReferrers(ctx, CancellationToken.None);
await ClickHouseQueries.SessionDepth(ctx, CancellationToken.None);
await ClickHouseQueries.RollingActivity(ctx, CancellationToken.None);

static string? ParseConnectionString(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--connection" or "-c")
            return args[i + 1];
    }

    return null;
}
