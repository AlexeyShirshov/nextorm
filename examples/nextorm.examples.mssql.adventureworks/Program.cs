using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Examples.SqlServer.AdventureWorks;
using NextORM.SqlServer;

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

var results = new List<(string Name, Exception? Error)>();

await Run("VipChurn", () => AdventureWorksQueries.VipChurn(ctx, CancellationToken.None));
await Run("RollingKpi", () => AdventureWorksQueries.RollingKpi(ctx, CancellationToken.None));
await Run("SupplyChain", () => AdventureWorksQueries.SupplyChain(ctx, CancellationToken.None));
await Run("ProductAbcXyz", () => AdventureWorksQueries.ProductAbcXyz(ctx, CancellationToken.None));
await Run("QuarterlyPivot", () => AdventureWorksQueries.QuarterlyPivot(ctx, CancellationToken.None));
await Run("TopProductsByCategory", () => AdventureWorksQueries.TopProductsByCategory(ctx, CancellationToken.None));
await Run("TerritoryYearOverYear", () => AdventureWorksQueries.TerritoryYearOverYear(ctx, CancellationToken.None));
await Run("CustomerRfm", () => AdventureWorksQueries.CustomerRfm(ctx, CancellationToken.None));
await Run("QuotaAttainment", () => AdventureWorksQueries.QuotaAttainment(ctx, CancellationToken.None));
await Run("TerritoryGrowthMonthOverMonth", () => AdventureWorksQueries.TerritoryGrowthMonthOverMonth(ctx, CancellationToken.None));
await Run("CustomerPareto", () => AdventureWorksQueries.CustomerPareto(ctx, CancellationToken.None));

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
