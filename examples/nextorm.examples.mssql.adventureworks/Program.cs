using Microsoft.Extensions.Logging;
using NextORM.Core;
using NextORM.Examples.SqlServer.AdventureWorks;
using NextORM.SqlServer;

var connectionString = ParseConnectionString(args, "--connection", "-c");
var logSql = args.Contains("--log-sql");

await using var database = await DemoDatabase.StartAsync(connectionString, CancellationToken.None);

using var loggerFactory = logSql
    ? LoggerFactory.Create(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Debug))
    : null;

var contextBuilder = new DataContextBuilder();
if (loggerFactory is not null)
    contextBuilder.UseLoggerFactory(loggerFactory);

await using var ctx = new SqlServerDataContext(database.ConnectionString, contextBuilder);

await AdventureWorksQueries.VipChurnAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.RollingKpiAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.SupplyChainAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.ProductAbcXyzAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.QuarterlyPivotAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.TopProductsByCategoryAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.TerritoryYearOverYearAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.CustomerRfmAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.QuotaAttainmentAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.TerritoryGrowthMonthOverMonthAsync(ctx, CancellationToken.None);
await AdventureWorksQueries.CustomerParetoAsync(ctx, CancellationToken.None);

static string? ParseConnectionString(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (names.Contains(args[i]))
            return args[i + 1];
    }

    return null;
}
