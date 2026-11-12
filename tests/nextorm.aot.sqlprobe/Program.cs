using System.Collections;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using MySqlConnector;
using NextORM.AotSqlProbe;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.MariaDb;
using NextORM.MySql;

// NativeAOT probe for the MySQL and MariaDB providers (#370, Stream B): provisions a tiny deterministic
// schema, then runs a fixed set of query shapes through the selected provider. Each shape prints its row
// count and a digest of the formatted rows, so a JIT run and a native run of the same source are directly
// comparable. Nothing here depends on a dataset image: the seed is part of the probe.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var connectionString = ParseValue(args, "--connection");
if (connectionString is null)
{
    Console.Error.WriteLine(
        "usage: nextorm.aot.sqlprobe --connection <cs> [--provider mysql|mariadb] [--only Name1,Name2]");
    return 2;
}

var provider = (ParseValue(args, "--provider") ?? "mysql").ToLowerInvariant();
if (provider is not ("mysql" or "mariadb"))
{
    Console.Error.WriteLine("--provider must be 'mysql' or 'mariadb'.");
    return 2;
}

Console.WriteLine($"[aot-sqlprobe] provider={provider}");

await SeedAsync(connectionString).ConfigureAwait(false);

await using var ctx = provider == "mariadb"
    ? new MariaDbDataContext(connectionString, new DataContextBuilder())
    : new MySqlDataContext(connectionString, new DataContextBuilder());

var shapes = new (string Name, Func<CancellationToken, Task<object>> Run)[]
{
    ("customer-filter", RunCustomerFilter),
    ("orders-page", RunOrdersPage),
    ("aggregate", RunAggregate),
    ("selectgroup", RunSelectGroup),
    ("join", RunJoin),
    ("cte", RunCte),
    ("prepared-reuse", RunPreparedReuse),
};

var only = ParseList(args, "--only");
if (only.Length > 0)
    shapes = shapes.Where(shape => only.Contains(shape.Name, StringComparer.OrdinalIgnoreCase)).ToArray();

var failed = 0;
foreach (var shape in shapes)
{
    try
    {
        var rows = await shape.Run(CancellationToken.None).ConfigureAwait(false);
        var (count, digest) = Describe(rows);
        Console.WriteLine($"[ OK ] {shape.Name}: {count} row(s) digest={digest}");
    }
    catch (Exception exception)
    {
        failed++;
        Console.WriteLine($"[FAIL] {shape.Name}: {DescribeFailure(exception)}");
    }
}

Console.WriteLine();
Console.WriteLine($"{shapes.Length - failed}/{shapes.Length} shapes completed; {failed} failed.");
return failed == 0 ? 0 : 1;

async Task<object> RunCustomerFilter(CancellationToken ct)
    => await ctx.From<ICustomer>()
        .Where(customer => customer.Country == "RU")
        .OrderBy(customer => customer.Id)
        .Select(customer => customer.Name)
        .ToListAsync(ct).ConfigureAwait(false);

async Task<object> RunOrdersPage(CancellationToken ct)
{
    var query = ctx.From<IOrder>().OrderBy(order => order.Id);
    query.Paging = new Paging { Offset = 1, Limit = 3 };
    return await query
        .Select(order => new { order.Id, order.Amount })
        .ToListAsync(ct).ConfigureAwait(false);
}

async Task<object> RunAggregate(CancellationToken ct)
{
    var rows = await ctx.From<IOrder>()
        .GroupBy(order => order.CustomerId)
        .Select(group => new
        {
            group.CustomerId,
            Count = SqlFunctions.Sql.count(),
            Total = SqlFunctions.Sql.sum(group.Amount)
        })
        .ToListAsync(ct).ConfigureAwait(false);

    return rows.OrderBy(row => row.CustomerId).ToList();
}

async Task<object> RunSelectGroup(CancellationToken ct)
{
    var rows = await ctx.From<IOrder>()
        .SelectGroup(order => new { order.CustomerId, Count = SqlFunctions.Sql.count() })
        .ToListAsync(ct).ConfigureAwait(false);

    return rows.OrderBy(row => row.CustomerId).ToList();
}

async Task<object> RunJoin(CancellationToken ct)
    => await ctx.From<IOrder>()
        .Join(ctx.From<ICustomer>(), (order, customer) => order.CustomerId == customer.Id)
        .OrderBy(pair => pair.Item1.Id)
        .Select(pair => new { pair.Item1.Id, pair.Item2.Name })
        .ToListAsync(ct).ConfigureAwait(false);

async Task<object> RunCte(CancellationToken ct)
{
    var large = ctx.From<IOrder>()
        .Where(order => order.Amount > 50m)
        .Select(order => new { order.Id, order.Amount })
        .AsCte("large_orders");

    return await ctx.From(large)
        .OrderBy(order => order.Id)
        .Select(order => new { order.Id, order.Amount })
        .ToListAsync(ct).ConfigureAwait(false);
}

async Task<object> RunPreparedReuse(CancellationToken ct)
{
    var command = ctx.From<ICustomer>()
        .OrderBy(customer => customer.Id)
        .Select(customer => customer.Id);

    var first = await command.ToListAsync(ct).ConfigureAwait(false);
    var second = await command.ToListAsync(ct).ConfigureAwait(false);
    return $"first={string.Join(",", first)};second={string.Join(",", second)}";
}

static async Task SeedAsync(string connectionString)
{
    string[] statements =
    [
        "DROP TABLE IF EXISTS aot_order",
        "DROP TABLE IF EXISTS aot_customer",
        "CREATE TABLE aot_customer (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, country VARCHAR(2) NOT NULL)",
        "CREATE TABLE aot_order (id INT NOT NULL PRIMARY KEY, customer_id INT NOT NULL, amount DECIMAL(10,2) NOT NULL, order_date DATETIME(6) NOT NULL)",
        "INSERT INTO aot_customer (id, name, country) VALUES (1, 'Alpha', 'RU'), (2, 'Beta', 'DE'), (3, 'Gamma', 'RU')",
        "INSERT INTO aot_order (id, customer_id, amount, order_date) VALUES " +
        "(1, 1, 10.50, '2024-01-02 03:04:05'), " +
        "(2, 1, 99.99, '2024-01-03 04:05:06'), " +
        "(3, 2, 5.00, '2024-01-04 05:06:07'), " +
        "(4, 2, 60.00, '2024-02-05 06:07:08'), " +
        "(5, 3, 120.25, '2024-02-06 07:08:09'), " +
        "(6, 3, 0.75, '2024-03-07 08:09:10')",
    ];

    await using var connection = new MySqlConnection(connectionString);
    await connection.OpenAsync().ConfigureAwait(false);
    foreach (var statement in statements)
    {
        await using var command = new MySqlCommand(statement, connection);
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}

static string? ParseValue(string[] args, string name)
{
    for (var i = 0; i < args.Length - 1; i++)
    {
        if (string.Equals(args[i], name, StringComparison.Ordinal))
            return args[i + 1];
    }

    return null;
}

static string[] ParseList(string[] args, string name)
{
    var value = ParseValue(args, name);
    return string.IsNullOrWhiteSpace(value)
        ? []
        : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
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
    if (rows is string text)
        return (text.Length, DigestOf(text));

    var builder = new StringBuilder();
    var count = 0;
    foreach (var row in (IEnumerable)rows)
    {
        builder.Append(row?.ToString() ?? "<null>").Append('\n');
        count++;
    }

    return (count, DigestOf(builder.ToString()));
}

static string DigestOf(string text)
    => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16].ToLowerInvariant();
