using NextORM.ClickHouse;
using NextORM.Core;
using NextORM.Examples.ClickHouse.Analytics;

var connectionString = ParseConnectionString(args);
await using var database = await DemoDatabase.StartAsync(connectionString, CancellationToken.None);

await using var ctx = new ClickHouseDataContext(database.ConnectionString, new DataContextBuilder());

await ClickHouseQueries.ArrayAnalyticsAsync(ctx, CancellationToken.None);
await ClickHouseQueries.FunnelAsync(ctx, CancellationToken.None);
await ClickHouseQueries.IncrementalAsync(ctx, CancellationToken.None);
await ClickHouseQueries.RetentionAsync(ctx, CancellationToken.None);
await ClickHouseQueries.SessionsAsync(ctx, CancellationToken.None);
await ClickHouseQueries.DailyTrafficAsync(ctx, CancellationToken.None);
await ClickHouseQueries.TopLandingPagesAsync(ctx, CancellationToken.None);
await ClickHouseQueries.DeviceSplitAsync(ctx, CancellationToken.None);
await ClickHouseQueries.TopReferrersAsync(ctx, CancellationToken.None);
await ClickHouseQueries.SessionDepthAsync(ctx, CancellationToken.None);
await ClickHouseQueries.RollingActivityAsync(ctx, CancellationToken.None);

static string? ParseConnectionString(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--connection" or "-c")
            return args[i + 1];
    }

    return null;
}
