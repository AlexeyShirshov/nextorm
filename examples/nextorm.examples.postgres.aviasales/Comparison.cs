using System.Collections;
using System.Text.Json;
using LinqToDB;
using NextORM.Core;

namespace NextORM.Examples.Postgres.Aviasales;

/// <summary>
/// Runs the two comparison arms for one query: nextorm and linq2db over the same dataset. Each arm
/// prints the first rows and the runner checks that both returned the same number of rows. Known engine
/// gaps (<see cref="NotSupportedException"/>, nextorm's <see cref="QueryPreparationException"/>, and
/// linq2db's <see cref="LinqToDBException"/>) are reported as <c>[FAIL]</c>; any other exception aborts
/// the run, exactly like the nextorm-only examples did.
/// </summary>
public static class Comparison
{
    public static async Task<bool> Run(
        string name,
        Func<CancellationToken, Task<object>> nextorm,
        Func<CancellationToken, Task<object>> linq2db,
        CancellationToken ct)
    {
        var nextormRows = await Arm($"{name} · nextorm", nextorm, ct);
        var linq2dbRows = await Arm($"{name} · linq2db", linq2db, ct);

        if (nextormRows is null || linq2dbRows is null)
            return false;

        var nextormCount = Count(nextormRows);
        var linq2dbCount = Count(linq2dbRows);

        if (nextormCount != linq2dbCount)
        {
            Console.WriteLine($"[DIFF] {name}: nextorm {nextormCount} row(s), linq2db {linq2dbCount} row(s)");
            Console.WriteLine();
            return false;
        }

        Console.WriteLine($"[ OK ] {name}: {nextormCount} row(s) in both arms");
        Console.WriteLine();
        return true;
    }

    private static async Task<object?> Arm(
        string title,
        Func<CancellationToken, Task<object>> query,
        CancellationToken ct)
    {
        try
        {
            var rows = await query(ct);
            Print(title, rows);
            return rows;
        }
        catch (Exception ex) when (ex is NotSupportedException or QueryPreparationException or LinqToDBException)
        {
            Console.WriteLine($"[FAIL] {title}: {ex.GetType().Name}: {ex.Message}");
            Console.WriteLine();
            return null;
        }
    }

    public static void Print(string title, object rows)
    {
        var items = rows is IEnumerable enumerable
            ? enumerable.Cast<object>().ToList()
            : new List<object> { rows };

        Console.WriteLine($"-- {title}: {items.Count} row(s)");

        foreach (var row in items.Take(10))
            Console.WriteLine("   " + JsonSerializer.Serialize(row));

        if (items.Count > 10)
            Console.WriteLine("   ...");
    }

    private static int Count(object rows) =>
        rows is IEnumerable enumerable ? enumerable.Cast<object>().Count() : 1;
}
