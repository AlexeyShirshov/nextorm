using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using NextORM.Core;
using NextORM.Extensions;
using NextORM.Sqlite;

if (args.Length != 1)
{
    Console.Error.WriteLine("Expected the parent SQLite connection string as args[0].");
    return 2;
}

Console.WriteLine($"connection={args[0]}");
using var context = new SqliteDataContext($"Data Source='{args[0]}';Default Timeout=30", new DataContextBuilder());
Run("interface-entity", () =>
{
    var ids = context.From<ISimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id).ToList();
    return string.Join(",", ids);
});
Run("captured-predicate", () =>
{
    var minimum = 5;
    var count = context.From<SimpleEntity>().Where(x => x.Id > minimum).Count();
    return $"minimum={minimum},count={count}";
});
Run("anonymous-projection", () =>
{
    var rows = context.From<IComplexEntity>().OrderBy(x => x.Id).Select(x => new { x.Id, x.String }).ToList();
    return string.Join(";", rows.Select(row => $"id={row.Id},string={row.String}"));
});
Run("nullable-scalar", () =>
{
    var values = context.From<IComplexEntity>().OrderBy(x => x.Id).Select(x => x.Int).ToList();
    return string.Join(",", values.Select(value => value?.ToString() ?? "null"));
});
Run("sqlite-json", () =>
{
    var value = context.From<SimpleEntity>().Where(x => x.Id == 1)
        .Select(x => new
        {
            Extract = SqlFunctions.Sqlite.json_extract<string>("{\"a\":\"hello\"}", "$.a"),
            Text = SqlFunctions.Sqlite.json_get_text("{\"a\":\"hello\"}", "$.a")
        }).First();
    return $"extract={value.Extract},text={value.Text}";
});
Run("aot-numeric-operator", () =>
{
    // Regression gate for the decimal-operator defect reported by the AOT probes (issue #370, 2026-11-12
    // comments): the pipeline rebuilds binary nodes from operands whose types are only known at run time,
    // and Expression.MakeBinary resolves a decimal operator through reflection. If the trimmer removed
    // the operators, this build throws — so the scenario fails and the native harness reports it.
    var left = System.Linq.Expressions.Expression.Parameter(typeof(decimal?), "a");
    var right = System.Linq.Expressions.Expression.Parameter(typeof(decimal?), "b");
    var body = System.Linq.Expressions.Expression.MakeBinary(
        System.Linq.Expressions.ExpressionType.Divide, left, right);
    var divide = System.Linq.Expressions.Expression
        .Lambda<Func<decimal?, decimal?, decimal?>>(body, left, right)
        .Compile();
    return $"decimalOperatorResolution=available;divide={divide(3m, 2m)?.ToString() ?? "null"}";
});
Run("groupby-aggregates", () =>
{
    var groups = context.From<IComplexEntity>().GroupBy(x => x.Int)
        .Having(g => SqlFunctions.Sql.count() > 1)
        .Select(g => new { g.Int, Count = SqlFunctions.Sql.count(), Sum = SqlFunctions.Sql.sum(g.Id) })
        .ToList();
    return string.Join(";", groups.Select(group => $"int={group.Int?.ToString() ?? "null"},count={group.Count},sum={group.Sum}"));
});
RunComponentSlices(args[0]);
return 0;

static void RunComponentSlices(string databasePath)
{
    using var context = new SqliteDataContext($"Data Source='{databasePath}';Default Timeout=30", new DataContextBuilder());
    try
    {
        var minRows = context.From<SimpleEntity>().SelectWhereMin(entity => entity.Id).ToList();
        Console.WriteLine($"component-slice=nextorm.extensions.SelectWhereMin\tstatus=pass\trows={minRows.Count}");
        var maxRows = context.From<SimpleEntity>().SelectWhereMax(entity => entity.Id).ToList();
        Console.WriteLine($"component-slice=nextorm.extensions.SelectWhereMax\tstatus=pass\trows={maxRows.Count}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"component-slice=nextorm.extensions\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}\tdetails={Escape(ex.ToString())}");
    }

    try
    {
        using var csvOutput = new MemoryStream();
        context.From<SimpleEntity>().WriteCsv(csvOutput, new CsvStreamOptions());
        Console.WriteLine($"component-slice=CsvStreamOptions\tstatus=pass\tbytes={csvOutput.Length}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"component-slice=CsvStreamOptions\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}");
    }
    try
    {
        using var jsonOutput = new MemoryStream();
        context.From<SimpleEntity>().WriteJson(jsonOutput, new JsonStreamOptions());
        Console.WriteLine($"component-slice=JsonShapePlan\tstatus=pass\tbytes={jsonOutput.Length}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"component-slice=JsonShapePlan\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}");
    }
}

static void Run(string id, Func<object?> action)
{
    try
    {
        var value = action();
        Console.WriteLine($"scenario={id}\tstatus=pass\tvalues={Escape(value?.ToString() ?? "null")}\texception=\tmessage=\tframe=");
    }
    catch (Exception ex)
    {
        var detail = ex;
        while (detail.InnerException is not null)
            detail = detail.InnerException;
        var firstFrame = new StackTrace(detail, true).GetFrames()?.FirstOrDefault();
        var stack = new StackTrace(detail, true).ToString();
        var location = firstFrame is null
            ? null
            : firstFrame.GetFileName() is { } fileName && firstFrame.GetFileLineNumber() > 0
                ? $"{fileName}:{firstFrame.GetFileLineNumber()}"
                : "line-unavailable";
        var message = ReferenceEquals(detail, ex)
            ? ex.Message
            : $"{ex.Message} InnerException={detail.GetType().FullName}: {detail.Message}";
        message = string.IsNullOrEmpty(stack) ? message : $"{message} StackTrace={stack}";
        Console.WriteLine($"scenario={id}\tstatus=fail\tvalues=\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(message)}\tframe={Escape(location ?? "")}");
    }
}

static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

[SqlTable("simple_entity")]
public interface ISimpleEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

[SqlTable("simple_entity")]
public sealed class SimpleEntity
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
}

[SqlTable("complex_entity")]
public interface IComplexEntity
{
    [Key]
    [Column("id")]
    long Id { get; set; }
    [Column("nullableint")]
    int? Int { get; set; }
    [Column("somestring")]
    string? String { get; set; }
}
