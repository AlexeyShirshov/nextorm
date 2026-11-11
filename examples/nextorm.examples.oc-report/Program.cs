
// oc-report: сводка по сессиям OpenCode из opencode.db — без Python и без raw SQL.
// AOT-эталон: приложение читает реальную базу (13 ГБ в исходном кейсе) только на чтение.
// Запуск: dotnet run --project examples/nextorm.examples.oc-report -- [days] [--agent pdca-executor] [--db path] [--all]
//
// Пример: dotnet run --project examples/nextorm.examples.oc-report -- 3
//         dotnet run --project examples/nextorm.examples.oc-report -- 1 --all
// Синтетическая база: sqlite3 /tmp/oc-report.db < examples/nextorm.examples.oc-report/seed.sql

using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NextORM.Core;
using NextORM.Sqlite;

var opts = Options.Parse(args);
var since = DateTimeOffset.UtcNow.AddDays(-opts.Days).ToUnixTimeMilliseconds();

var connectionString = new SqliteConnectionStringBuilder
{
    DataSource = opts.DbPath,
    Mode = SqliteOpenMode.ReadOnly,
    Pooling = false,
}.ToString();

using var connection = new SqliteConnection(connectionString);
connection.Open();
using var db = new DataContextBuilder().UseSqlite(connection).CreateDataContext();

var query = db.From<ISessionRow>().Where(s => s.TimeCreated >= since);
if (opts.Agent is not null)
    query = query.Where(s => s.Agent == opts.Agent);

var rows = await query
    .Select(s => new
    {
        s.Id,
        s.Agent,
        s.Model,
        s.Title,
        s.TimeCreated,
        s.TimeUpdated,
        s.TokensInput,
        s.TokensOutput,
        s.TokensCacheRead,
        s.Cost,
        s.IdleOutcome,
    })
    .ToListAsync();

var sessions = rows
    .Select(r => new Session(
        r.Id,
        r.Agent,
        ParseModel(r.Model),
        r.Title,
        r.TimeCreated ?? 0,
        r.TimeUpdated ?? 0,
        r.TokensInput ?? 0,
        r.TokensOutput ?? 0,
        r.TokensCacheRead ?? 0,
        r.Cost ?? 0,
        r.IdleOutcome))
    .ToList();

Console.WriteLine(
    $"window: {FromMs(since):yyyy-MM-dd HH:mm}Z .. {DateTime.UtcNow:yyyy-MM-dd HH:mm}Z " +
    $"({opts.Days}d), agent={(opts.Agent ?? "any")}, sessions={sessions.Count}");
Console.WriteLine();

var aggQuery = db.From<ISessionRow>().Where(s => s.TimeCreated >= since);
if (opts.Agent is not null)
    aggQuery = aggQuery.Where(s => s.Agent == opts.Agent);

// Агрегация в SQL: model — JSON, поэтому ключ группы извлекается json_extract,
// а не сравнивается как текст (иначе одна модель распадается на несколько групп).
var byModel = await aggQuery
    .GroupBy(s => new
    {
        Provider = SqlFunctions.Sqlite.json_extract<string>(s.Model, "$.providerID"),
        Id = SqlFunctions.Sqlite.json_extract<string>(s.Model, "$.id"),
        s.IdleOutcome,
    })
    .Select(s => new
    {
        Provider = SqlFunctions.Sqlite.json_extract<string>(s.Model, "$.providerID"),
        Id = SqlFunctions.Sqlite.json_extract<string>(s.Model, "$.id"),
        Outcome = s.IdleOutcome,
        N = SqlFunctions.Sql.count(),
        Input = SqlFunctions.Sql.sum(s.TokensInput),
        Output = SqlFunctions.Sql.sum(s.TokensOutput),
        Cache = SqlFunctions.Sql.sum(s.TokensCacheRead),
        Cost = SqlFunctions.Sql.sum(s.Cost),
    })
    .ToListAsync();

var modelAggs = byModel
    .GroupBy(r => (Provider: r.Provider ?? "?", Id: r.Id ?? "?"))
    .Select(g => new ModelAgg(
        g.Key.Provider,
        g.Key.Id,
        g.Sum(r => r.N),
        g.Where(r => r.Outcome == "succeeded").Sum(r => r.N),
        g.Where(r => r.Outcome == "failed").Sum(r => r.N),
        g.Sum(r => r.Input ?? 0),
        g.Sum(r => r.Output ?? 0),
        g.Sum(r => r.Cache ?? 0),
        g.Sum(r => r.Cost ?? 0)))
    .OrderByDescending(a => a.N)
    .ToList();

PrintModels(modelAggs);
PrintOutcomes(sessions);

var ids = sessions.Select(s => s.Id).ToHashSet(StringComparer.Ordinal);
var messages = await db.From<IMessageRow>()
    .Where(m => m.TimeCreated >= since && m.Data != null && m.Data.Contains("\"error\""))
    .Select(m => new { m.SessionId, m.Type, m.Data })
    .ToListAsync();

PrintErrors(messages
    .Where(m => m.SessionId is not null && ids.Contains(m.SessionId))
    .Select(m => ((string?)m.SessionId, (string?)m.Type, (string?)m.Data)));

static void PrintModels(List<ModelAgg> aggs)
{
    Console.WriteLine("by model:");
    Console.WriteLine($"  {"model",-42} {"n",4} {"ok",4} {"fail",5} {"other",6} {"input",9} {"output",8} {"cache",9} {"cost",9}");
    foreach (var a in aggs)
    {
        Console.WriteLine(
            $"  {a.Label,-42} {a.N,4} {a.Ok,4} {a.Fail,5} {a.Other,6} " +
            $"{M(a.Input),9} {K(a.Output),8} {M(a.Cache),9} {"$" + a.Cost.ToString("F3"),9}");
    }
    Console.WriteLine(
        $"  {"total",-42} {aggs.Sum(a => a.N),4} {aggs.Sum(a => a.Ok),4} {aggs.Sum(a => a.Fail),5} " +
        $"{aggs.Sum(a => a.Other),6} {M(aggs.Sum(a => a.Input)),9} {K(aggs.Sum(a => a.Output)),8} " +
        $"{M(aggs.Sum(a => a.Cache)),9} {"$" + aggs.Sum(a => a.Cost).ToString("F3"),9}");
    Console.WriteLine();
}

static void PrintOutcomes(List<Session> sessions)
{
    var failed = sessions.Where(s => s.Outcome is not null and not "succeeded").ToList();
    Console.WriteLine($"outcomes: {string.Join(", ", sessions.GroupBy(s => s.Outcome ?? "(running)").Select(g => $"{g.Key}={g.Count()}"))}");
    foreach (var s in failed.Take(10))
        Console.WriteLine($"  {s.Id[^6..]} {FromMs(s.TimeCreated):MM-dd HH:mm}Z {s.Outcome,-11} {Truncate(s.Title, 46)}");
    Console.WriteLine();
}

static void PrintErrors(IEnumerable<(string? SessionId, string? Type, string? Data)> messages)
{
    var errors = new List<(string Type, string Model, string Session, string Message)>();
    foreach (var m in messages)
    {
        if (string.IsNullOrEmpty(m.Data)) continue;
        try
        {
            using var doc = JsonDocument.Parse(m.Data);
            if (!doc.RootElement.TryGetProperty("error", out var error) || error.ValueKind != JsonValueKind.Object)
                continue;
            var type = error.TryGetProperty("type", out var t) ? t.GetString() ?? "?" : "?";
            var message = error.TryGetProperty("message", out var msg) ? msg.GetString() ?? "" : "";
            var model = doc.RootElement.TryGetProperty("model", out var mm) && mm.ValueKind == JsonValueKind.Object
                ? ParseModelRaw(mm)
                : "(none)";
            errors.Add((type, model, m.SessionId![^6..], message));
        }
        catch (JsonException)
        {
            // не JSON — пропускаем
        }
    }

    Console.WriteLine($"errors: {errors.Count}");
    foreach (var g in errors.GroupBy(e => e.Type).OrderByDescending(g => g.Count()))
    {
        var sample = g.First();
        Console.WriteLine($"  {g.Key,-24} {g.Count(),3}  {Truncate(sample.Message, 70)}");
        foreach (var e in g.GroupBy(x => x.Model).OrderByDescending(x => x.Count()))
            Console.WriteLine($"      {e.Key,-34} {e.Count(),3}  first ses_…{e.First().Session}");
    }
}

static string ParseModel(string? raw)
{
    if (string.IsNullOrEmpty(raw)) return "(none)";
    try
    {
        using var doc = JsonDocument.Parse(raw);
        return ParseModelRaw(doc.RootElement);
    }
    catch (JsonException)
    {
        return raw;
    }
}

static string ParseModelRaw(JsonElement model)
{
    var provider = model.TryGetProperty("providerID", out var p) ? p.GetString() : null;
    var id = model.TryGetProperty("id", out var i) ? i.GetString() : null;
    if (string.IsNullOrEmpty(provider) && string.IsNullOrEmpty(id)) return "(none)";
    return $"{provider ?? "?"}/{id ?? "?"}";
}

static DateTime FromMs(long ms) => DateTimeOffset.FromUnixTimeMilliseconds(ms).UtcDateTime;
static string M(long v) => $"{v / 1_000_000.0:0.00}M";
static string K(long v) => $"{v / 1_000.0:0.0}k";
static string Truncate(string? s, int n) => s is null ? "" : s.Length <= n ? s : s[..n];

record Session(
    string Id,
    string? Agent,
    string ModelLabel,
    string? Title,
    long TimeCreated,
    long TimeUpdated,
    long TokensInput,
    long TokensOutput,
    long TokensCacheRead,
    double Cost,
    string? Outcome);

record ModelAgg(
    string Provider,
    string Id,
    int N,
    int Ok,
    int Fail,
    long Input,
    long Output,
    long Cache,
    double Cost)
{
    public string Label => $"{Provider}/{Id}";
    public int Other => N - Ok - Fail;
}

sealed record Options(int Days, string? Agent, string DbPath)
{
    public static Options Parse(string[] args)
    {
        var days = 3;
        string? agent = "pdca-executor";
        var dbPath = Environment.GetEnvironmentVariable("OPENCODE_DB")
                     ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                         ".local", "share", "opencode", "opencode.db");

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--all":
                    agent = null;
                    break;
                case "--agent" when i + 1 < args.Length:
                    agent = args[++i];
                    break;
                case "--db" when i + 1 < args.Length:
                    dbPath = args[++i];
                    break;
                default:
                    if (int.TryParse(args[i], out var d) && d > 0) days = d;
                    break;
            }
        }

        if (!File.Exists(dbPath))
            throw new FileNotFoundException($"opencode.db not found: {dbPath}", dbPath);

        return new Options(days, agent, dbPath);
    }
}

[SqlTable("session_v2")]
public interface ISessionRow
{
    [Key]
    [Column("id")]
    string Id { get; set; }

    [Column("agent")]
    string? Agent { get; set; }

    [Column("model")]
    string? Model { get; set; }

    [Column("title")]
    string? Title { get; set; }

    [Column("time_created")]
    long? TimeCreated { get; set; }

    [Column("time_updated")]
    long? TimeUpdated { get; set; }

    [Column("tokens_input")]
    long? TokensInput { get; set; }

    [Column("tokens_output")]
    long? TokensOutput { get; set; }

    [Column("tokens_cache_read")]
    long? TokensCacheRead { get; set; }

    [Column("cost")]
    double? Cost { get; set; }

    [Column("idle_outcome")]
    string? IdleOutcome { get; set; }
}

[SqlTable("session_message")]
public interface IMessageRow
{
    [Key]
    [Column("id")]
    string Id { get; set; }

    [Column("session_id")]
    string? SessionId { get; set; }

    [Column("type")]
    string? Type { get; set; }

    [Column("time_created")]
    long? TimeCreated { get; set; }

    [Column("data")]
    string? Data { get; set; }
}
