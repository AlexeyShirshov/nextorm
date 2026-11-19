using System.Collections;
using System.Data.Common;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using NextORM.AotSqlProbe;
using NextORM.Core;
using NextORM.Extensions;

// NativeAOT probe for every data-context provider (#370, Stream B): provisions a tiny deterministic schema
// in the provider's own dialect, then runs a fixed set of query shapes through it. Each shape prints its row
// count and a digest of the formatted rows, so a JIT run and a native run of the same source are directly
// comparable. Nothing here depends on a dataset image: the seed is part of the probe.

CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

var connectionString = ParseValue(args, "--connection");
if (connectionString is null)
{
    Console.Error.WriteLine(
        $"usage: nextorm.aot.sqlprobe --connection <cs> [--provider {ProbeProvider.Accepted}] [--only Name1,Name2]");
    return 2;
}

var providerName = (ParseValue(args, "--provider") ?? "mysql").ToLowerInvariant();
var provider = ProbeProvider.Create(providerName);
if (provider is null)
{
    Console.Error.WriteLine($"--provider must be one of {ProbeProvider.Accepted}.");
    return 2;
}

Console.WriteLine(
    $"[aot-sqlprobe] provider={provider.Name} dynamic-code-supported={RuntimeFeature.IsDynamicCodeSupported} dynamic-code-compiled={RuntimeFeature.IsDynamicCodeCompiled}");

await SeedAsync(provider, connectionString).ConfigureAwait(false);

await using var ctx = provider.CreateContext(connectionString);

// Fixed row ids for the write shapes: the key is declared on the entity, so no provider needs an identity
// column (ClickHouse has none), and the native arm and its control cannot disagree on a generated value.
const int DmlRowId = 9_001;
const int BulkRowId = 9_100;

var shapes = new (string Name, Func<CancellationToken, Task<object>> Run)[]
{
    ("customer-filter", RunCustomerFilter),
    ("orders-page", RunOrdersPage),
    ("aggregate", RunAggregate),
    ("selectgroup", RunSelectGroup),
    ("selectgroup-having", RunSelectGroupHaving),
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
    ("relationship-load-with", RunRelationshipLoadWith),
    ("eager-load-single-query", RunEagerLoadSingleQuery),
    ("join-into-collection", RunJoinIntoCollection),
    ("eager-load-many-to-many", RunEagerLoadManyToMany),
    ("join-into-many-to-many", RunJoinIntoManyToMany),
    ("eager-load-composite-many-to-many", RunEagerLoadCompositeManyToMany),
    ("join-into-composite-many-to-many", RunJoinIntoCompositeManyToMany),
    ("value-converters", RunValueConverters),
};

var only = ParseList(args, "--only");
if (only.Length > 0)
    shapes = shapes.Where(shape => only.Contains(shape.Name, StringComparer.OrdinalIgnoreCase)).ToArray();

var failed = 0;
var skipped = 0;
foreach (var shape in shapes)
{
    if (!provider.Supports(shape.Name))
    {
        skipped++;
        Console.WriteLine($"[SKIP] {shape.Name}: the engine does not execute this shape");
        continue;
    }

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
Console.WriteLine($"{shapes.Length - failed - skipped}/{shapes.Length} shapes completed; {failed} failed; {skipped} unsupported.");
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

async Task<object> RunSelectGroupHaving(CancellationToken ct)
{
    // The grouped terminal with a HAVING condition on its own result shape: the condition is rebound onto
    // the source entity, so the shape also proves that rebinding runs without a run-time-closed delegate.
    var rows = await ctx.From<IOrder>()
        .SelectGroupHaving(
            order => new { order.CustomerId, Count = SqlFunctions.Sql.count() },
            group => group.Count > 1)
        .ToListAsync(ct).ConfigureAwait(false);

    return rows.OrderBy(row => row.CustomerId).Select(row => $"{row.CustomerId}:{row.Count}").ToList();
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
        .Values(new AotWrite { Id = DmlRowId, Name = "aot-dml", Age = 7 }).InsertAsync(ct).ConfigureAwait(false);
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
        .Values(Enumerable.Range(0, 5).Select(age => new AotWrite { Id = BulkRowId + age, Name = "aot-bulk", Age = age }))
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

async Task<object> RunRelationshipLoadWith(CancellationToken ct)
{
    // Keep this default-mode split load independent from the explicit one-command SingleQuery shape below.
    var counter = new ProbeCommandCounter();
    await using var split = CreateCountedContext(counter);
    var parents = await split.From<AotParent>()
        .OrderBy(parent => parent.Id)
        .LoadWith(parent => parent.Children)
        .ToListAsync(ct).ConfigureAwait(false);

    var graph = FormatGraph(parents.Select(parent =>
        (parent.Id, parent.Children.Select(child => child.Id))));
    RequireGraph("relationship-load-with", graph, "1[10,11];2[]");
    RequireCommandCount(counter, "relationship-load-with", 2);
    ReportGraph("relationship-load-with", graph, 2);

    return parents.Select(parent =>
        $"{parent.Id}:{parent.Name}[{string.Join(",", parent.Children.OrderBy(child => child.Id).Select(child => child.Id))}]").ToList();
}

async Task<object> RunEagerLoadSingleQuery(CancellationToken ct)
{
    var counter = new ProbeCommandCounter();
    await using var single = CreateCountedContext(counter, EagerLoadMode.SingleQuery);
    var parents = await single.From<AotParent>()
        .OrderBy(parent => parent.Id)
        .LoadWith(parent => parent.Children)
        .ToListAsync(ct).ConfigureAwait(false);

    var graph = FormatGraph(parents.Select(parent =>
        (parent.Id, parent.Children.Select(child => child.Id))));
    RequireGraph("eager-load-single-query", graph, "1[10,11];2[]");
    RequireCommandCount(counter, "eager-load-single-query", 1);
    ReportGraph("eager-load-single-query", graph, 1);
    return graph;
}

async Task<object> RunJoinIntoCollection(CancellationToken ct)
{
    var counter = new ProbeCommandCounter();
    await using var joined = CreateCountedContext(counter);
    var parents = await joined.From<AotParent>()
        .OrderBy(parent => parent.Id)
        .JoinInto(joined.From<AotChild>(), (parent, child) => parent.Id == child.ParentId, parent => parent.Children)
        .ToListAsync(ct).ConfigureAwait(false);

    var graph = FormatGraph(parents.Select(parent =>
        (parent.Id, parent.Children.Select(child => child.Id))));
    RequireGraph("join-into-collection", graph, "1[10,11];2[]");
    RequireCommandCount(counter, "join-into-collection", 1);
    ReportGraph("join-into-collection", graph, 1);
    return graph;
}

async Task<object> RunEagerLoadManyToMany(CancellationToken ct)
{
    var counter = new ProbeCommandCounter();
    await using var many = CreateCountedContext(counter, EagerLoadMode.SingleQuery);
    RegisterScalarM2m(many);
    var parents = await many.From<AotM2mParent>()
        .OrderBy(parent => parent.Id)
        .LoadWith(parent => parent.Tags)
        .ToListAsync(ct).ConfigureAwait(false);

    var graph = FormatGraph(parents.Select(parent =>
        (parent.Id, parent.Tags.Select(tag => tag.Id))));
    RequireGraph("eager-load-many-to-many", graph, "1[10,10,11];2[12];3[]");
    RequireCommandCount(counter, "eager-load-many-to-many", 1);
    ReportGraph("eager-load-many-to-many", graph, 1);
    return graph;
}

async Task<object> RunJoinIntoManyToMany(CancellationToken ct)
{
    var counter = new ProbeCommandCounter();
    await using var many = CreateCountedContext(counter);
    RegisterScalarM2m(many);
    var parents = await many.From<AotM2mParent>()
        .OrderBy(parent => parent.Id)
        .JoinInto(many.From<AotM2mTag>(), (parent, tag) => true, parent => parent.Tags)
        .ToListAsync(ct).ConfigureAwait(false);

    var graph = FormatGraph(parents.Select(parent =>
        (parent.Id, parent.Tags.Select(tag => tag.Id))));
    RequireGraph("join-into-many-to-many", graph, "1[10,10,11];2[12];3[]");
    RequireCommandCount(counter, "join-into-many-to-many", 1);
    ReportGraph("join-into-many-to-many", graph, 1);
    return graph;
}

async Task<object> RunEagerLoadCompositeManyToMany(CancellationToken ct)
{
    var counter = new ProbeCommandCounter();
    await using var many = CreateCountedContext(counter, EagerLoadMode.SingleQuery);
    RegisterCompositeM2m(many);
    var parents = await many.From<AotCompositeM2mParent>()
        .OrderBy(parent => parent.Id)
        .LoadWith(parent => parent.Tags)
        .ToListAsync(ct).ConfigureAwait(false);

    var graph = FormatGraph(parents.Select(parent =>
        (parent.Id, parent.Tags.Select(tag => tag.Id))));
    RequireGraph("eager-load-composite-many-to-many", graph, "1[10,10,11];2[11];3[]");
    RequireCommandCount(counter, "eager-load-composite-many-to-many", 1);
    ReportGraph("eager-load-composite-many-to-many", graph, 1);
    return graph;
}

async Task<object> RunJoinIntoCompositeManyToMany(CancellationToken ct)
{
    var counter = new ProbeCommandCounter();
    await using var many = CreateCountedContext(counter);
    RegisterCompositeM2m(many);
    var parents = await many.From<AotCompositeM2mParent>()
        .OrderBy(parent => parent.Id)
        .JoinInto(many.From<AotCompositeM2mTag>(), (parent, tag) => true, parent => parent.Tags)
        .ToListAsync(ct).ConfigureAwait(false);

    var graph = FormatGraph(parents.Select(parent =>
        (parent.Id, parent.Tags.Select(tag => tag.Id))));
    RequireGraph("join-into-composite-many-to-many", graph, "1[10,10,11];2[11];3[]");
    RequireCommandCount(counter, "join-into-composite-many-to-many", 1);
    ReportGraph("join-into-composite-many-to-many", graph, 1);
    return graph;
}

IDataContext CreateCountedContext(ProbeCommandCounter counter, EagerLoadMode? eagerLoadMode = null)
{
    var builder = new DataContextBuilder().AddInterceptor(counter);
    if (eagerLoadMode is { } mode)
        builder.UseEagerLoadMode(mode);

    return provider.CreateContext(connectionString, builder);
}

void RegisterScalarM2m(IDataContext target)
{
    target.From<AotM2mTag>();
    target.From<AotM2mLink>();
    target.From<AotM2mParent>(builder => builder
        .HasManyThrough<AotM2mTag, AotM2mLink, int, int>(
            parent => parent.Tags, parent => parent.Id, link => link.ParentId, tag => tag.Id, link => link.ChildId));
}

void RegisterCompositeM2m(IDataContext target)
{
    target.From<AotCompositeM2mTag>();
    target.From<AotCompositeM2mLink>();
    target.From<AotCompositeM2mParent>(builder => builder
        .HasManyThrough<AotCompositeM2mTag, AotCompositeM2mLink>(
            parent => parent.Tags,
            parent => new { TenantId = parent.ParentTenantId, LinkId = parent.LinkId },
            link => new { TenantId = link.ParentTenantId, LinkId = link.ParentLinkId },
            tag => new { TenantId = tag.TagTenantId, TagId = tag.TagId },
            link => new { TenantId = link.TagTenantId, TagId = link.TagId }));
}

async Task<object> RunValueConverters(CancellationToken ct)
{
    // Both directions: the read applies the converter, and a write binds the model through it. A mapping
    // that ignored the converter would read the stored text as a number and print a different value.
    await ctx.CreateUpdateBuilder<AotConverted>()
        .Set(row => row.Amount, 7.25m)
        .Where(row => row.Id == 3)
        .UpdateAsync(ct).ConfigureAwait(false);

    var rows = await ctx.From<AotConverted>().OrderBy(row => row.Id).ToListAsync(ct).ConfigureAwait(false);
    return rows.Select(row => $"{row.Id}:{row.Amount.ToString(CultureInfo.InvariantCulture)}").ToList();
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

static async Task SeedAsync(ProbeProvider provider, string connectionString)
{
    await using var connection = provider.CreateConnection(connectionString);
    await connection.OpenAsync().ConfigureAwait(false);
    foreach (var statement in provider.Schema)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = statement;
        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
}

static string FormatGraph(IEnumerable<(int ParentId, IEnumerable<int> ChildIds)> parents)
    => string.Join(";", parents
        .OrderBy(parent => parent.ParentId)
        .Select(parent => $"{parent.ParentId}[{string.Join(",", parent.ChildIds.OrderBy(id => id))}]"));

static void RequireGraph(string shapeName, string actual, string expected)
{
    if (!string.Equals(actual, expected, StringComparison.Ordinal))
        throw new InvalidOperationException($"{shapeName}: expected graph '{expected}', received '{actual}'.");
}

static void RequireCommandCount(ProbeCommandCounter counter, string shapeName, int expected)
{
    if (counter.Executing != expected || counter.Executed != expected || counter.Failed != 0)
    {
        throw new InvalidOperationException(
            $"Expected {expected} successful SQL command(s) for {shapeName}; observed executing={counter.Executing}, executed={counter.Executed}, failed={counter.Failed}.");
    }
}

static void ReportGraph(string shapeName, string graph, int commands)
    => Console.WriteLine($"[GRAPH] {shapeName}: {graph};commands={commands}");

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

internal sealed class ProbeCommandCounter : IQueryInterceptor
{
    private int _executing;
    private int _executed;
    private int _failed;

    public int Executing => Volatile.Read(ref _executing);
    public int Executed => Volatile.Read(ref _executed);
    public int Failed => Volatile.Read(ref _failed);

    public void CommandExecuting(CommandEventData eventData, DbCommand command)
        => Interlocked.Increment(ref _executing);

    public void CommandExecuted(CommandEventData eventData, DbCommand command, TimeSpan elapsed)
        => Interlocked.Increment(ref _executed);

    public void CommandFailed(CommandEventData eventData, DbCommand command, Exception exception)
        => Interlocked.Increment(ref _failed);
}
