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
    ("csv-streaming", RunCsvStreaming),
    ("json-streaming", RunJsonStreaming),
    ("row-streaming", RunRowStreaming),
    ("raw-sql-mapping", RunRawSqlMapping),
    ("dml-builders", RunDmlBuilders),
    ("bulk-insert", RunBulkInsert),
    ("captured-parameter", RunCapturedParameter),
    ("aggregate-count-distinct", RunAggregateCountDistinct),
    ("aggregate-avg", RunAggregateAvg),
    ("aggregate-min", RunAggregateMin),
    ("aggregate-max", RunAggregateMax),
    ("having", RunHaving),
    ("window-function", RunWindowFunction),
    ("json-column", RunJsonColumn),
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

async Task<object> RunCsvStreaming(CancellationToken ct)
{
    using var output = new MemoryStream();
    await ctx.From<IOrder>()
        .OrderBy(order => order.Id)
        .Select(order => new { order.Id, order.Amount })
        .WriteCsvAsync(output, null, ct)
        .ConfigureAwait(false);
    return Encoding.UTF8.GetString(output.ToArray());
}

async Task<object> RunJsonStreaming(CancellationToken ct)
{
    using var output = new MemoryStream();
    await ctx.From<IOrder>()
        .OrderBy(order => order.Id)
        .Select(order => new { order.Id, order.Amount })
        .WriteJsonAsync(output, ct)
        .ConfigureAwait(false);
    return Encoding.UTF8.GetString(output.ToArray());
}

async Task<object> RunRowStreaming(CancellationToken ct)
{
    var ids = new List<int>();
    await foreach (var id in ctx.From<IOrder>().OrderBy(order => order.Id).Select(order => order.Id).ToAsyncEnumerable(ct))
        ids.Add(id);

    return string.Join(",", ids);
}

async Task<object> RunRawSqlMapping(CancellationToken ct)
{
    // Both raw mapping forms: a scalar set and a mapped entity bound by column name.
    var ids = new List<int>();
    await using (var result = await ctx.ExecuteRawAsync("select id from aot_order order by id", [], ct).ConfigureAwait(false))
    {
        await foreach (var id in result.ReadAsync<int>(ct))
            ids.Add(id);
    }

    var rows = new List<string>();
    await using (var result = await ctx.ExecuteRawAsync(
        "select id, customer_id, amount from aot_order order by id", [], ct).ConfigureAwait(false))
    {
        await foreach (var row in result.ReadAsync<AotOrderRow>(ct))
            rows.Add($"{row.Id}:{row.CustomerId}:{row.Amount}");
    }

    return $"scalars={string.Join(",", ids)};mapped={string.Join(";", rows)}";
}

async Task<object> RunDmlBuilders(CancellationToken ct)
{
    // insert -> update -> read -> delete. The generated key is never printed: the native arm and the
    // control run one after the other and would see different values.
    var cleaned = await ctx.CreateDeleteBuilder<AotWrite>()
        .Where(row => row.Name == "aot-dml").DeleteAsync(ct).ConfigureAwait(false);
    await ctx.CreateInsertBuilder<AotWrite>()
        .Values(new AotWrite { Name = "aot-dml", Age = 7 }).InsertAsync(ct).ConfigureAwait(false);
    var updated = await ctx.CreateUpdateBuilder<AotWrite>()
        .Set(row => row.Age, 8).Where(row => row.Name == "aot-dml").UpdateAsync(ct).ConfigureAwait(false);
    var ages = await ctx.From<AotWrite>()
        .Where(row => row.Name == "aot-dml").Select(row => row.Age).ToListAsync(ct).ConfigureAwait(false);
    var removed = await ctx.CreateDeleteBuilder<AotWrite>()
        .Where(row => row.Name == "aot-dml").DeleteAsync(ct).ConfigureAwait(false);

    return $"cleaned={cleaned};updated={updated};ages={string.Join(",", ages)};removed={removed}";
}

async Task<object> RunBulkInsert(CancellationToken ct)
{
    var cleaned = await ctx.CreateDeleteBuilder<AotWrite>()
        .Where(row => row.Name == "aot-bulk").DeleteAsync(ct).ConfigureAwait(false);
    var written = await ctx.CreateBulkInsertBuilder<AotWrite>()
        .Values(Enumerable.Range(0, 5).Select(age => new AotWrite { Name = "aot-bulk", Age = age }))
        .BulkInsertAsync(ct).ConfigureAwait(false);
    var ages = await ctx.From<AotWrite>()
        .Where(row => row.Name == "aot-bulk").OrderBy(row => row.Age).Select(row => row.Age)
        .ToListAsync(ct).ConfigureAwait(false);
    var removed = await ctx.CreateDeleteBuilder<AotWrite>()
        .Where(row => row.Name == "aot-bulk").DeleteAsync(ct).ConfigureAwait(false);

    return $"cleaned={cleaned};written={written};ages={string.Join(",", ages)};removed={removed}";
}

async Task<object> RunCapturedParameter(CancellationToken ct)
{
    // A filter whose operand is a captured local, not a constant folded into the SQL text.
    var minimum = 50m;
    var ids = await ctx.From<IOrder>()
        .Where(order => order.Amount > minimum)
        .OrderBy(order => order.Id)
        .Select(order => order.Id)
        .ToListAsync(ct).ConfigureAwait(false);
    return $"minimum={minimum};ids={string.Join(",", ids)}";
}

async Task<object> RunAggregateCountDistinct(CancellationToken ct)
{
    var value = await ctx.From<IOrder>()
        .Select(order => SqlFunctions.Sql.count_distinct(order.CustomerId))
        .FirstAsync(ct).ConfigureAwait(false);
    return value.ToString(CultureInfo.InvariantCulture);
}

async Task<object> RunAggregateAvg(CancellationToken ct)
{
    var value = await ctx.From<IOrder>()
        .Select(order => SqlFunctions.Sql.avg(order.Amount))
        .FirstAsync(ct).ConfigureAwait(false);
    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
}

async Task<object> RunAggregateMin(CancellationToken ct)
{
    var value = await ctx.From<IOrder>()
        .Select(order => SqlFunctions.Sql.min(order.Id))
        .FirstAsync(ct).ConfigureAwait(false);
    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
}

async Task<object> RunAggregateMax(CancellationToken ct)
{
    var value = await ctx.From<IOrder>()
        .Select(order => SqlFunctions.Sql.max(order.Id))
        .FirstAsync(ct).ConfigureAwait(false);
    return Convert.ToString(value, CultureInfo.InvariantCulture) ?? "null";
}

async Task<object> RunHaving(CancellationToken ct)
{
    var rows = await ctx.From<IOrder>()
        .GroupBy(order => order.CustomerId)
        .Having(group => SqlFunctions.Sql.count() > 1)
        .Select(group => new { group.CustomerId, Count = SqlFunctions.Sql.count() })
        .ToListAsync(ct).ConfigureAwait(false);

    return rows.OrderBy(row => row.CustomerId)
        .Select(row => $"{row.CustomerId}:{row.Count}")
        .ToList();
}

async Task<object> RunWindowFunction(CancellationToken ct)
{
    var rows = await ctx.From<IOrder>()
        .OrderBy(order => order.Id)
        .Select(order => new { order.Id, Running = SqlFunctions.Sql.sum_over(order.Id).Over() })
        .ToListAsync(ct).ConfigureAwait(false);

    return rows.Select(row => $"{row.Id}:{row.Running.ToString(CultureInfo.InvariantCulture)}").ToList();
}

async Task<object> RunJsonColumn(CancellationToken ct)
{
    // Both directions of the JSON converter: the write binds the model through the converter, the read
    // applies it back. The update is idempotent, so the JIT control and the native arm share the row.
    await ctx.CreateUpdateBuilder<AotJsonEntity>()
        .Set(row => row.Payload, new AotJsonPayload { Name = "native", Value = 42 })
        .Where(row => row.Id == 1)
        .UpdateAsync(ct).ConfigureAwait(false);

    var rows = await ctx.From<AotJsonEntity>().OrderBy(row => row.Id).ToListAsync(ct).ConfigureAwait(false);
    return rows.Select(row =>
        $"{row.Id}:{row.Payload?.Name ?? "null"}/{row.Payload?.Value.ToString(CultureInfo.InvariantCulture) ?? "null"}").ToList();
}

static async Task SeedAsync(string connectionString)
{
    string[] statements =
    [
        "DROP TABLE IF EXISTS aot_order",
        "DROP TABLE IF EXISTS aot_customer",
        "CREATE TABLE aot_customer (id INT NOT NULL PRIMARY KEY, name VARCHAR(64) NOT NULL, country VARCHAR(2) NOT NULL)",
        "CREATE TABLE aot_order (id INT NOT NULL PRIMARY KEY, customer_id INT NOT NULL, amount DECIMAL(10,2) NOT NULL, order_date DATETIME(6) NOT NULL)",
        "DROP TABLE IF EXISTS aot_write",
        "CREATE TABLE aot_write (id INT NOT NULL AUTO_INCREMENT PRIMARY KEY, name VARCHAR(64) NOT NULL, age INT NOT NULL)",
        "DROP TABLE IF EXISTS aot_json",
        "CREATE TABLE aot_json (id INT NOT NULL PRIMARY KEY, payload TEXT NULL)",
        "INSERT INTO aot_json (id, payload) VALUES (1, '{\"name\":\"seed\",\"value\":7}'), (2, NULL)",
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
