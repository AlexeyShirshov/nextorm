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

await AdventureWorksQueries.VipChurn(ctx, CancellationToken.None);
await AdventureWorksQueries.RollingKpi(ctx, CancellationToken.None);
await AdventureWorksQueries.SupplyChain(ctx, CancellationToken.None);
await AdventureWorksQueries.ProductAbcXyz(ctx, CancellationToken.None);
await AdventureWorksQueries.QuarterlyPivot(ctx, CancellationToken.None);
await AdventureWorksQueries.TopProductsByCategory(ctx, CancellationToken.None);
await AdventureWorksQueries.TerritoryYearOverYear(ctx, CancellationToken.None);
await AdventureWorksQueries.CustomerRfm(ctx, CancellationToken.None);
await AdventureWorksQueries.QuotaAttainment(ctx, CancellationToken.None);
await AdventureWorksQueries.TerritoryGrowthMonthOverMonth(ctx, CancellationToken.None);
await AdventureWorksQueries.CustomerPareto(ctx, CancellationToken.None);

static string? ParseConnectionString(string[] args, params string[] names)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (names.Contains(args[i]))
            return args[i + 1];
    }

    return null;
}
