using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Examples.Postgres.Aviasales;
using NextORM.Postgres;

var connectionString = ParseConnectionString(args, "--connection", "-c");
var logSql = args.Contains("--log-sql");

await using var database = await DemoDatabase.StartAsync(connectionString, CancellationToken.None);

using var loggerFactory = logSql
    ? LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
    : null;

var contextBuilder = new DataContextBuilder();
if (loggerFactory is not null)
    contextBuilder.UseLoggerFactory(loggerFactory);

await using var ctx = new PostgresDataContext(database.ConnectionString, contextBuilder);

await AviasalesQueries.AircraftDelayChainsAsync(ctx, CancellationToken.None);
await AviasalesQueries.BusinessOccupancyMatrixAsync(ctx, CancellationToken.None);
await AviasalesQueries.PassengerNoShowAnalysisAsync(ctx, CancellationToken.None);
await AviasalesQueries.RollingRevenueMetricsAsync(ctx, CancellationToken.None);
await AviasalesQueries.RouteNetworkAbcXyzAsync(ctx, CancellationToken.None);
await AviasalesQueries.TopRoutesByCityAsync(ctx, CancellationToken.None);
await AviasalesQueries.AirportOnTimePerformanceAsync(ctx, CancellationToken.None);
await AviasalesQueries.DelayPercentilesByModelAsync(ctx, CancellationToken.None);
await AviasalesQueries.FrequentFlyersAsync(ctx, CancellationToken.None);
await AviasalesQueries.PassengerGrowthAsync(ctx, CancellationToken.None);
await AviasalesQueries.CancellationByRouteAsync(ctx, CancellationToken.None);

static string? ParseConnectionString(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (names.Contains(args[i]))
            return args[i + 1];
    }

    return null;
}
