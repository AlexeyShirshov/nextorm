using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Examples.Postgres.Aviasales;
using NextORM.Postgres;

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

await AviasalesQueries.AircraftDelayChains(ctx, CancellationToken.None);
await AviasalesQueries.BusinessOccupancyMatrix(ctx, CancellationToken.None);
await AviasalesQueries.PassengerNoShowAnalysis(ctx, CancellationToken.None);
await AviasalesQueries.RollingRevenueMetrics(ctx, CancellationToken.None);
await AviasalesQueries.RouteNetworkAbcXyz(ctx, CancellationToken.None);
await AviasalesQueries.TopRoutesByCity(ctx, CancellationToken.None);
await AviasalesQueries.AirportOnTimePerformance(ctx, CancellationToken.None);
await AviasalesQueries.DelayPercentilesByModel(ctx, CancellationToken.None);
await AviasalesQueries.FrequentFlyers(ctx, CancellationToken.None);
await AviasalesQueries.PassengerGrowth(ctx, CancellationToken.None);
await AviasalesQueries.CancellationByRoute(ctx, CancellationToken.None);

static string? ParseConnectionString(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (names.Contains(args[i]))
            return args[i + 1];
    }

    return null;
}
