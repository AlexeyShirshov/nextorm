using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using NextORM.AotExamples.ClickHouse.Analytics;
using NextORM.Core;
using NextORM.ClickHouse;

// NativeAOT probe for the ClickHouse provider: the same queries as the comparison example, without
// linq2db, Testcontainers or BenchmarkDotNet, so the whole process can be published with PublishAot
// and run natively. Each query prints its row count and a digest of the formatted rows, which makes
// a JIT run and a native run directly comparable.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var connectionString = ParseConnectionString(args);
if (connectionString is null)
{
    Console.Error.WriteLine(
        "usage: nextorm.aotexamples.clickhouse.analytics [--connection <cs>] [--only Name1,Name2]" +
        Environment.NewLine +
        "The connection string also comes from $CLICKHOUSE_ANALYTICS_CONNECTION or $NEXTORM_DEMODB_CLICKHOUSE_CONNECTION; " +
        "this project provisions nothing.");
    return 2;
}

Console.WriteLine("[aot-clickhouse] using the provided connection string");

await using var ctx = new ClickHouseDataContext(connectionString, new DataContextBuilder());

var queries = new (string Name, Func<CancellationToken, Task<object>> Run)[]
{
    ("ArrayAnalytics", ct => ClickHouseQueries.ArrayAnalytics(ctx, ct)),
    ("Funnel", ct => ClickHouseQueries.Funnel(ctx, ct)),
    ("Sessions", ct => ClickHouseQueries.Sessions(ctx, ct)),
    ("DailyTraffic", ct => ClickHouseQueries.DailyTraffic(ctx, ct)),
    ("TopLandingPages", ct => ClickHouseQueries.TopLandingPages(ctx, ct)),
    ("DeviceSplit", ct => ClickHouseQueries.DeviceSplit(ctx, ct)),
    ("TopReferrers", ct => ClickHouseQueries.TopReferrers(ctx, ct)),
    ("SessionDepth", ct => ClickHouseQueries.SessionDepth(ctx, ct)),
    ("RollingActivity", ct => ClickHouseQueries.RollingActivity(ctx, ct))
};

var only = ParseList(args, "--only");
if (only.Length > 0)
    queries = queries.Where(q => only.Contains(q.Name, StringComparer.OrdinalIgnoreCase)).ToArray();

var failed = 0;
foreach (var query in queries)
{
    try
    {
        var rows = await query.Run(CancellationToken.None);
        var (count, digest) = Describe(rows);
        Console.WriteLine($"[ OK ] {query.Name}: {count} row(s) digest={digest}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"[FAIL] {query.Name}: {DescribeFailure(exception)}");
    }
}

Console.WriteLine();
Console.WriteLine($"{queries.Length - failed}/{queries.Length} queries completed; {failed} failed.");
return failed == 0 ? 0 : 1;

static string? ParseConnectionString(string[] args)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (args[i] is "--connection" or "-c")
            return args[i + 1];
    }

    var fromEnvironment = Environment.GetEnvironmentVariable("CLICKHOUSE_ANALYTICS_CONNECTION")
        ?? Environment.GetEnvironmentVariable("NEXTORM_DEMODB_CLICKHOUSE_CONNECTION");
    return string.IsNullOrWhiteSpace(fromEnvironment) ? null : fromEnvironment;
}

static string[] ParseList(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.Ordinal))
        {
            return args[i + 1].Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        }
    }

    return [];
}

static string DescribeFailure(Exception exception)
{
    var parts = new List<string>();
    for (Exception? current = exception; current is not null; current = current.InnerException)
        parts.Add($"{current.GetType().Name}: {current.Message}");
    return string.Join(" -> ", parts);
}

static (int Count, string Digest) Describe(object rows)
{
    var builder = new StringBuilder();
    var count = 0;
    foreach (var row in (IEnumerable)rows)
    {
        builder.Append(row?.ToString() ?? "<null>").Append('\n');
        count++;
    }

    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
    return (count, Convert.ToHexString(hash)[..16].ToLowerInvariant());
}
