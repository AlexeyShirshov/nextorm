using System.Globalization;
using NextORM.Core;

namespace NextORM.AotScenarios;

/// <summary>
/// The native scenario catalog (#370 Stream B). Each entry is one ledger capability shape; every native
/// instrument and the JIT control execute this same list, so a capability cannot be marked proven by a
/// shape the other arm does not run.
/// </summary>
public static partial class AotScenarioCatalog
{
    /// <summary>
    /// Shapes executable against the SQLite integration seed (`simple_entity`, `complex_entity`, `person`,
    /// `orders`, `insert_entity`). Read-only, or idempotent: the JIT control and the native binary run
    /// against the same database, so a write shape scopes its rows with a fixed marker and removes them at
    /// both ends, and never prints a generated key.
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
        new("terminal-any", TerminalAny),
        new("terminal-first-scalar", TerminalFirstScalar),
        new("terminal-first-entity", TerminalFirstEntity),
        new("terminal-single-scalar", TerminalSingleScalar),
        new("terminal-single-entity", TerminalSingleEntity),
        new("interface-entity-materialization", InterfaceEntityMaterialization),
        new("window-function", WindowFunction),
        new("value-converters", ValueConverters),
        new("relationship-load-with", RelationshipLoadWith),
        new("row-streaming", RowStreaming),
        new("raw-sql-mapping", RawRowMapping),
        new("dml-builders", DmlBuilders),
        new("bulk-insert", BulkInsert),
        new("many-to-many-load-with", ManyToManyLoadWith)
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
        // Prepared explicitly, then executed twice: the second execution must reuse the prepared
        // statement and its parameter binding instead of preparing again.
        var command = context.From<AotSimpleEntity>().OrderBy(x => x.Id).Select(x => x.Id).PrepareBuffered();
        var first = command.ToList(context);
        var second = command.ToList(context);
        return $"first={string.Join(",", first)};second={string.Join(",", second)}";
    }

    /// <summary>
    /// The query-command-producing terminal family, each prepared explicitly and executed:
    /// <c>AnyCommand</c> and both <c>FirstOrFirstOrDefaultCommand</c> / <c>SingleOrSingleOrDefaultCommand</c>
    /// overloads (selector and no-selector). The no-selector entity shape builds its projection from
    /// metadata, one lambda per mapped property, so it also covers a nullable <c>DateTime</c> column.
    /// </summary>
    private static object? TerminalAny(IDataContext context)
    {
        var command = context.From<AotSimpleEntity>()
            .Where(x => x.Id > 5)
            .AnyCommand()
            .PrepareBuffered();
        return $"any={command.ExecuteScalar(context, throwIfNull: true)}";
    }

    private static object? TerminalFirstScalar(IDataContext context)
    {
        var command = context.From<AotSimpleEntity>()
            .OrderBy(x => x.Id)
            .FirstOrFirstOrDefaultCommand(x => x.Id)
            .PrepareBuffered();
        return $"first={command.FirstOrDefault(context)}";
    }

    private static object? TerminalFirstEntity(IDataContext context)
    {
        var command = context.From<AotComplexEntity>()
            .OrderBy(x => x.Id)
            .FirstOrFirstOrDefaultCommand()
            .PrepareBuffered();
        var row = command.FirstOrDefault(context);
        return row is null
            ? "null"
            : $"{row.Id}:{row.String ?? "null"}:{row.Timestamp?.Ticks.ToString(CultureInfo.InvariantCulture) ?? "null"}";
    }

    private static object? TerminalSingleScalar(IDataContext context)
    {
        var command = context.From<AotSimpleEntity>()
            .Where(x => x.Id == 3)
            .SingleOrSingleOrDefaultCommand(x => x.Id)
            .PrepareBuffered();
        return $"single={command.SingleOrDefault(context)}";
    }

    private static object? TerminalSingleEntity(IDataContext context)
    {
        var command = context.From<AotComplexEntity>()
            .Where(x => x.Id == 2)
            .SingleOrSingleOrDefaultCommand()
            .PrepareBuffered();
        var row = command.SingleOrDefault(context);
        return row is null
            ? "null"
            : $"{row.Id}:{row.Numeric?.ToString(CultureInfo.InvariantCulture) ?? "null"}";
    }

    private static object? WindowFunction(IDataContext context)
    {
        var rows = context.From<AotSimpleEntity>()
            .OrderBy(x => x.Id)
            .Select(x => new { x.Id, Running = SqlFunctions.Sql.sum_over(x.Id).Over() })
            .ToList();

        return string.Join(";", rows.Select(row => $"{row.Id}:{row.Running.ToString(CultureInfo.InvariantCulture)}"));
    }

    private static object? ValueConverters(IDataContext context)
    {
        // Both directions of a converter: the write binds the model value through the converter, the read
        // applies it back. The update is idempotent, so the JIT control and the native arm share the row
        // instead of accumulating one insert per arm.
        context.CreateUpdateBuilder<AotConverterEntity>()
            .Set(x => x.Amount, 12.5m)
            .Set(x => x.Payload, new AotJsonPayload { Name = "native", Value = 42 })
            .Where(x => x.Id == 1)
            .Update();

        var rows = context.From<AotConverterEntity>().OrderBy(x => x.Id).ToList();
        return string.Join(";", rows.Select(row =>
            $"{row.Id}:{row.Amount.ToString(CultureInfo.InvariantCulture)}:{row.Payload?.Name ?? "null"}/{row.Payload?.Value.ToString(CultureInfo.InvariantCulture) ?? "null"}"));
    }

    private static object? RelationshipLoadWith(IDataContext context)
    {
        // The navigation, its keys and the child mapping all come from the entity metadata: the shape runs
        // only if the compile-time description carries the [Relationship] declaration.
        var customers = context.From<AotRelCustomer>()
            .OrderBy(customer => customer.Id)
            .LoadWith(customer => customer.Orders)
            .ToList();
        return string.Join(";", customers.Select(customer =>
            $"{customer.Id}:{customer.Name ?? "null"}[{string.Join(",", customer.Orders.OrderBy(order => order.Id).Select(order => order.Id))}]"));
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
