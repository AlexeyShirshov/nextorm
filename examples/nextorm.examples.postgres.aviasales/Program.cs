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

var results = new List<(string Name, Exception? Error)>();

await Run("AircraftDelayChains", () => AviasalesQueries.AircraftDelayChains(ctx, CancellationToken.None));
await Run("BusinessOccupancyMatrix", () => AviasalesQueries.BusinessOccupancyMatrix(ctx, CancellationToken.None));
await Run("PassengerNoShowAnalysis", () => AviasalesQueries.PassengerNoShowAnalysis(ctx, CancellationToken.None));
await Run("RollingRevenueMetrics", () => AviasalesQueries.RollingRevenueMetrics(ctx, CancellationToken.None));
await Run("RouteNetworkAbcXyz", () => AviasalesQueries.RouteNetworkAbcXyz(ctx, CancellationToken.None));
await Run("TopRoutesByCity", () => AviasalesQueries.TopRoutesByCity(ctx, CancellationToken.None));
await Run("AirportOnTimePerformance", () => AviasalesQueries.AirportOnTimePerformance(ctx, CancellationToken.None));
await Run("DelayPercentilesByModel", () => AviasalesQueries.DelayPercentilesByModel(ctx, CancellationToken.None));
await Run("FrequentFlyers", () => AviasalesQueries.FrequentFlyers(ctx, CancellationToken.None));
await Run("PassengerGrowth", () => AviasalesQueries.PassengerGrowth(ctx, CancellationToken.None));
await Run("CancellationByRoute", () => AviasalesQueries.CancellationByRoute(ctx, CancellationToken.None));

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

static string? ParseConnectionString(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (names.Contains(args[i]))
            return args[i + 1];
    }

    return null;
}
