using System.Globalization;
using NextORM.Core;

namespace NextORM.AotScenarios;

/// <summary>
/// The native scenario catalog (#370 Stream B). Each entry is one ledger capability shape; every native
/// instrument and the JIT control execute this same list, so a capability cannot be marked proven by a
/// shape the other arm does not run.
/// </summary>
public static class AotScenarioCatalog
{
    /// <summary>
    /// Shapes executable against the SQLite integration seed (`simple_entity`, `complex_entity`, `person`,
    /// `orders`). Read-only: the JIT control and the native binary run against the same database, so a
    /// shape that mutated it would make the second arm disagree with the first.
    /// </summary>
    public static IReadOnlyList<AotScenario> Sqlite { get; } =
    [
        new("paging", Paging),
        new("aggregate-count-distinct", AggregateCountDistinct),
        new("aggregate-avg", AggregateAvg),
        new("aggregate-min", AggregateMin),
        new("aggregate-max", AggregateMax),
        new("join", Join),
        new("cte", Cte),
        new("prepared-reuse", PreparedReuse),
        new("window-function", WindowFunction)
    ];

    private static object? Paging(IDataContext context)
        => string.Join(",", context.From<AotSimpleEntity>()
            .OrderBy(x => x.Id)
            .Offset(2)
            .Limit(3)
            .Select(x => x.Id)
            .ToList());

    private static object? AggregateCountDistinct(IDataContext context)
        => context.From<AotComplexEntity>()
            .Select(x => SqlFunctions.Sql.count_distinct(x.Int))
            .First()
            .ToString(CultureInfo.InvariantCulture);

    private static object? AggregateAvg(IDataContext context)
        => Convert.ToString(context.From<AotComplexEntity>()
            .Select(x => SqlFunctions.Sql.avg(x.Id))
            .First(), CultureInfo.InvariantCulture);

    private static object? AggregateMin(IDataContext context)
        => Convert.ToString(context.From<AotComplexEntity>()
            .Select(x => SqlFunctions.Sql.min(x.Int))
            .First(), CultureInfo.InvariantCulture);

    private static object? AggregateMax(IDataContext context)
        => Convert.ToString(context.From<AotComplexEntity>()
            .Select(x => SqlFunctions.Sql.max(x.Int))
            .First(), CultureInfo.InvariantCulture);

    private static object? Join(IDataContext context)
    {
        var rows = context.From<AotOrder>()
            .Join<AotPerson>((order, person) => order.BuyerId == person.Id)
            .OrderBy(projection => projection.Item1.Id)
            .Select(projection => new { projection.Item1.Id, projection.Item2.Name })
            .ToList();

        return string.Join(";", rows.Select(row => $"{row.Id}:{row.Name ?? "null"}"));
    }

    private static object? PreparedReuse(IDataContext context)
    {
        // One command executed twice: the second execution must reuse the prepared statement and the
        // cached plan rather than rebuild either.
        var command = context.From<AotSimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id);
        var first = command.ToList();
        var second = command.ToList();
        return $"first={string.Join(",", first)};second={string.Join(",", second)}";
    }

    private static object? WindowFunction(IDataContext context)
    {
        var rows = context.From<AotSimpleEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Running = SqlFunctions.Sql.sum_over(x.Id).Over() })
            .ToList();

        return string.Join(";", rows.Select(row => $"{row.Id}:{row.Running.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static object? Cte(IDataContext context)
    {
        var recent = context.From<AotSimpleEntity>()
            .Where(x => x.Id > 7)
            .Select(x => x.Id)
            .AsCte("aot_recent_ids");

        return string.Join(",", context.From(recent).OrderBy(id => id).ToList());
    }
}
