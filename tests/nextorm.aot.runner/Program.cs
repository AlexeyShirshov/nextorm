using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Text;
using System.Globalization;
using Microsoft.Data.Sqlite;
using NextORM.AotScenarios;
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
Run("selectgroup-keyed", () =>
{
    // Regression gate for the class-3 defect in the AOT probes (issue #370, 2026-11-12 comments): the
    // packaged SelectGroup infers its GROUP BY keys at preparation and used to wrap each key body in the
    // non-generic Expression.Lambda, which closes Expression<Func<TEntity, TKey>> at run time — a
    // generic instantiation NativeAOT never sees. The delegate type is now closed by the compile-time
    // entity type. If the run-time close comes back, this scenario throws on a native publish.
    var groups = context.From<IComplexEntity>()
        .SelectGroup(x => new { x.Int, Count = SqlFunctions.Sql.count() })
        .ToList();
    return string.Join(";", groups
        .OrderBy(group => group.Int is null ? 1 : 0)
        .ThenBy(group => group.Int)
        .Select(group => $"int={group.Int?.ToString() ?? "null"},count={group.Count}"));
});
Run("selectgroup-having", () =>
{
    // Same gate for the SelectGroupHaving path: the condition is rebound onto the source entity, and
    // both the rebind and the HAVING combination used to close Expression<Func<TEntity, bool>> at run
    // time. The delegate type is now closed by the compile-time entity type.
    var groups = context.From<IComplexEntity>()
        .SelectGroupHaving(
            x => new { x.Int, Count = SqlFunctions.Sql.count() },
            result => result.Count > 1)
        .ToList();
    return string.Join(";", groups
        .OrderBy(group => group.Int is null ? 1 : 0)
        .ThenBy(group => group.Int)
        .Select(group => $"int={group.Int?.ToString() ?? "null"},count={group.Count}"));
});
Run("extreme-min", () =>
{
    // Native/JIT comparator for the packaged extreme-row component: the globally minimal row of the
    // seeded simple_entity rows.
    var rows = context.From<SimpleEntity>()
        .SelectWhereMin(row => row.Id)
        .ToList();
    return string.Join(";", rows.OrderBy(row => row.Id).Select(row => row.Id));
});
Run("extreme-max", () =>
{
    var rows = context.From<SimpleEntity>()
        .SelectWhereMax(row => row.Id)
        .ToList();
    return string.Join(";", rows.OrderBy(row => row.Id).Select(row => row.Id));
});
Run("extreme-group", () =>
{
    // Per-group extreme rows: one row per parity group of the seeded ids.
    var rows = context.From<SimpleEntity>()
        .SelectWhereMin(row => row.Id, groupBy: row => row.Id % 2)
        .ToList();
    return string.Join(";", rows.OrderBy(row => row.Id).Select(row => row.Id));
});
Run("correlated-any", () =>
{
    // Correlated subquery: the inner query references the outer row's Id. Regression gate for the
    // outer-reference marker defect (#370 S11): the marker node is non-generic, so building it never
    // resolves a constructor or property of a type closed at run time.
    var ids = context.From<SimpleEntity>()
        .Where(row => context.From<IComplexEntity>().Where(inner => inner.Int == row.Id).Any())
        .OrderBy(row => row.Id)
        .Select(row => row.Id)
        .ToList();
    return string.Join(";", ids);
});
Run("correlated-count", () =>
{
    // Correlated aggregate terminal: the inner Count is rewritten into a scalar subquery over the
    // outer-reference marker.
    var rows = context.From<SimpleEntity>()
        .Select(row => new { row.Id, N = context.From<IComplexEntity>().Where(inner => inner.Int == row.Id).Count() })
        .ToList();
    return string.Join(";", rows.OrderBy(row => row.Id).Select(row => $"{row.Id}:{row.N}"));
});
Run("correlated-scalar-first", () =>
{
    // A correlated scalar subquery built from a projection terminal (FirstOrDefault) rather than an
    // aggregate: the inner command keeps its own SELECT list and the outer row is read through the
    // marker.
    var rows = context.From<SimpleEntity>()
        .Select(row => new { row.Id, V = context.From<IComplexEntity>().Where(inner => inner.Int == row.Id).Select(inner => inner.Id).FirstOrDefault() })
        .ToList();
    return string.Join(";", rows.OrderBy(row => row.Id).Select(row => $"{row.Id}:{row.V}"));
});
Run("correlated-sum", () =>
{
    // Correlated aggregate with a selector: Sum(long) goes through the aggregate projection host with a
    // body, not the selector-free Count shape.
    var rows = context.From<SimpleEntity>()
        .Select(row => new { row.Id, S = context.From<IComplexEntity>().Where(inner => inner.Int == row.Id).Sum(inner => inner.Numeric) })
        .ToList();
    return string.Join(";", rows.OrderBy(row => row.Id).Select(row => $"{row.Id}:{row.S?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null"}"));
});
Run("correlated-nested-depth2", () =>
{
    // Correlation depth 2: the innermost command reads both the middle row and the outermost row.
    var ids = context.From<SimpleEntity>()
        .Where(outer => context.From<IComplexEntity>().Where(inner => context.From<SimpleEntity>().Where(leaf => leaf.Id == outer.Id && leaf.Id == inner.Int).Any()).Any())
        .OrderBy(outer => outer.Id)
        .Select(outer => outer.Id)
        .ToList();
    return string.Join(";", ids);
});
Run("correlated-exists", () =>
{
    // The exists(...) function over a correlated command built inline as the function argument.
    var ids = context.From<SimpleEntity>()
        .Where(outer => SqlFunctions.Sql.exists(context.From<IComplexEntity>().Where(inner => inner.Int == outer.Id)))
        .OrderBy(outer => outer.Id)
        .Select(outer => outer.Id)
        .ToList();
    return string.Join(";", ids);
});
Run("correlated-outer-member", () =>
{
    // A member of a join-projection item read inside a correlated body: the outer reference is the
    // projection (`p.Item2`), so the marker's `Ref` is followed by a member access.
    var rows = context.From<AotNavOrder>()
        .Join<AotNavPerson>((order, person) => order.BuyerId == person.Id)
        .Select(projection => new { projection.Item1.Id, N = context.From<AotNavOrder>().Where(inner => inner.BuyerId == projection.Item2.Id).Count() })
        .ToList();
    return string.Join(";", rows.OrderBy(row => row.Id).Select(row => $"{row.Id}:{row.N}"));
});
Run("join-into", () =>
{
    var counter = new AotQueryCounter();
    context.AddInterceptor(counter);
    var parents = context.From<AotRelCustomer>()
        .OrderBy(customer => customer.Id)
        .JoinInto(
            context.From<AotRelOrder>(),
            (customer, order) => customer.Id == order.BuyerId,
            customer => customer.Orders)
        .ToList();
    return FormatRelCustomerGraph(parents, counter.CommandCount);
});
Run("load-with-single-query", () =>
{
    using var eagerContext = new SqliteDataContext(
        $"Data Source='{args[0]}';Default Timeout=30",
        new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
    var counter = new AotQueryCounter();
    eagerContext.AddInterceptor(counter);
    var parents = eagerContext.From<AotRelCustomer>()
        .OrderBy(customer => customer.Id)
        .LoadWith(customer => customer.Orders)
        .ToList();
    return FormatRelCustomerGraph(parents, counter.CommandCount);
});
Run("load-with-nested-single-query", () =>
{
    using var eagerContext = new SqliteDataContext(
        $"Data Source='{args[0]}';Default Timeout=30",
        new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
    var counter = new AotQueryCounter();
    eagerContext.AddInterceptor(counter);
    var parents = eagerContext.From<AotRelCustomer>()
        .OrderBy(customer => customer.Id)
        .LoadWith(
            customer => customer.Orders,
            childContext => childContext.From<AotRelOrder>().LoadWith(order => order.Approver))
        .ToList();
    return FormatNestedRelCustomerGraph(parents, counter.CommandCount);
});
Run("generated-row-materializer", () =>
{
    // The [RowMaterializer] DTO is materialized by the source-generated provider registered by its module
    // initializer, not by a compiled expression tree. Under NativeAOT the expression path is interpreted,
    // so this scenario proves the generated provider is present, registered and correct on a native build:
    // its values are compared with the JIT run of the same binary by the harness.
    var rows = context.From<IComplexEntity>()
        .OrderBy(x => x.Id)
        .Select(x => new AotComplexDto(x.Id, x.Int, x.String, x.Numeric))
        .ToList();
    return string.Join(";", rows.Select(row =>
        $"{row.Id}:{row.Int?.ToString(CultureInfo.InvariantCulture) ?? "null"}:{row.String ?? "null"}:{row.Numeric?.ToString(CultureInfo.InvariantCulture) ?? "null"}"));
});
Run("streaming-writers", () =>
{
    // WriteCsv/WriteJson do not materialize rows: a flat projection is written by the direct scalar
    // column writers, which are ordinary compiled code rather than a compiled expression tree. Comparing
    // the full text with the JIT control proves the native build emits byte-identical CSV and JSON.
    using var csvOutput = new MemoryStream();
    context.From<SimpleEntity>().Where(x => x.Id <= 3).OrderBy(x => x.Id).WriteCsv(csvOutput, new CsvStreamOptions());
    using var jsonOutput = new MemoryStream();
    context.From<SimpleEntity>().Where(x => x.Id <= 3).OrderBy(x => x.Id).WriteJson(jsonOutput, new JsonStreamOptions());
    return $"csv={Encoding.UTF8.GetString(csvOutput.ToArray())}|json={Encoding.UTF8.GetString(jsonOutput.ToArray())}";
});
foreach (var scenario in AotScenarioCatalog.Sqlite)
    Run(scenario.Id, () => scenario.Run(context));
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

    RunManyToManySingleQuerySlice(databasePath);
    RunCompositeManyToManySingleQuerySlice(databasePath);
    RunJoinIntoKeyTypeMatrix(databasePath);

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

static void RunManyToManySingleQuerySlice(string databasePath)
{
    const int parentId = 1_500_001_000;
    const int emptyParentId = parentId + 1;
    const int firstTagId = parentId + 300;
    const int secondTagId = parentId + 301;
    const int firstLinkId = parentId + 400;

    using var context = new SqliteDataContext(
        $"Data Source='{databasePath}';Default Timeout=30",
        new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
    context.From<AotJunctionTag>();
    context.From<AotJunctionLink>();
    context.From<AotJunctionParent>(builder => builder
        .HasManyThrough<AotJunctionTag, AotJunctionLink, int, int>(
            parent => parent.Tags, parent => parent.Id, link => link.ParentId, tag => tag.Id, link => link.ChildId));

    context.CreateDeleteBuilder<AotJunctionLink>()
        .Where(link => link.Id >= firstLinkId && link.Id <= firstLinkId + 2)
        .Delete();
    context.CreateDeleteBuilder<AotJunctionTag>()
        .Where(tag => tag.Id == firstTagId || tag.Id == secondTagId)
        .Delete();
    context.CreateDeleteBuilder<AotJunctionParent>()
        .Where(parent => parent.Id == parentId || parent.Id == emptyParentId)
        .Delete();

    context.CreateInsertBuilder<AotJunctionParent>()
        .Values([
            new AotJunctionParent { Id = parentId, Name = "native-m2m" },
            new AotJunctionParent { Id = emptyParentId, Name = "native-empty" }
        ])
        .Insert();
    context.CreateInsertBuilder<AotJunctionTag>()
        .Values([
            new AotJunctionTag { Id = firstTagId, Name = "first" },
            new AotJunctionTag { Id = secondTagId, Name = "second" }
        ])
        .Insert();
    context.CreateInsertBuilder<AotJunctionLink>()
        .Values([
            new AotJunctionLink { Id = firstLinkId, ParentId = parentId, ChildId = firstTagId },
            new AotJunctionLink { Id = firstLinkId + 1, ParentId = parentId, ChildId = firstTagId },
            new AotJunctionLink { Id = firstLinkId + 2, ParentId = parentId, ChildId = secondTagId }
        ])
        .Insert();

    var joinIntoCounter = new AotQueryCounter();
    context.AddInterceptor(joinIntoCounter);
    try
    {
        var parents = context.From<AotJunctionParent>()
            .Where(parent => parent.Id == parentId || parent.Id == emptyParentId)
            .OrderBy(parent => parent.Id)
            .JoinInto(
                context.From<AotJunctionTag>(),
                (parent, tag) => parent.Id > 0,
                parent => parent.Tags)
            .ToList();
        Console.WriteLine($"component-slice=join-into-many-to-many-join-into\tstatus=pass\tvalues={Escape(FormatJunctionGraph(parents, joinIntoCounter.CommandCount))}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"component-slice=join-into-many-to-many-join-into\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}\tdetails={Escape(ex.ToString())}");
    }

    var loadWithCounter = new AotQueryCounter();
    context.AddInterceptor(loadWithCounter);
    try
    {
        var parents = context.From<AotJunctionParent>()
            .Where(parent => parent.Id == parentId || parent.Id == emptyParentId)
            .OrderBy(parent => parent.Id)
            .LoadWith(parent => parent.Tags)
            .ToList();
        Console.WriteLine($"component-slice=join-into-many-to-many\tstatus=pass\tvalues={Escape(FormatJunctionGraph(parents, loadWithCounter.CommandCount))}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"component-slice=join-into-many-to-many\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}\tdetails={Escape(ex.ToString())}");
    }
}

static void RunCompositeManyToManySingleQuerySlice(string databasePath)
{
    const int parentId = 1_500_002_000;
    const int emptyParentId = parentId + 1;
    const int firstTagId = parentId + 300;
    const int secondTagId = parentId + 301;
    const int firstLinkId = parentId + 400;

    EnsureCompositeJunctionTables(databasePath);

    using var context = new SqliteDataContext(
        $"Data Source='{databasePath}';Default Timeout=30",
        new DataContextBuilder().UseEagerLoadMode(EagerLoadMode.SingleQuery));
    RegisterCompositeJunctionNavigation(context);

    context.CreateDeleteBuilder<AotCompositeJunctionLink>()
        .Where(link => link.Id >= firstLinkId && link.Id <= firstLinkId + 2)
        .Delete();
    context.CreateDeleteBuilder<AotCompositeJunctionTag>()
        .Where(tag => tag.Id == firstTagId || tag.Id == secondTagId)
        .Delete();
    context.CreateDeleteBuilder<AotCompositeJunctionParent>()
        .Where(parent => parent.Id == parentId || parent.Id == emptyParentId)
        .Delete();

    context.CreateInsertBuilder<AotCompositeJunctionParent>()
        .Values([
            new AotCompositeJunctionParent { Id = parentId, ParentKeyA = 5001, ParentKeyB = 5002, Name = "native-composite" },
            new AotCompositeJunctionParent { Id = emptyParentId, ParentKeyA = 5003, ParentKeyB = 5004, Name = "native-composite-empty" }
        ])
        .Insert();
    context.CreateInsertBuilder<AotCompositeJunctionTag>()
        .Values([
            new AotCompositeJunctionTag { Id = firstTagId, TagKeyA = 6001, TagKeyB = 6002, Name = "first" },
            new AotCompositeJunctionTag { Id = secondTagId, TagKeyA = 6003, TagKeyB = 6004, Name = "second" }
        ])
        .Insert();
    context.CreateInsertBuilder<AotCompositeJunctionLink>()
        .Values([
            new AotCompositeJunctionLink { Id = firstLinkId, ParentKeyA = 5001, ParentKeyB = 5002, TagKeyA = 6001, TagKeyB = 6002 },
            new AotCompositeJunctionLink { Id = firstLinkId + 1, ParentKeyA = 5001, ParentKeyB = 5002, TagKeyA = 6001, TagKeyB = 6002 },
            new AotCompositeJunctionLink { Id = firstLinkId + 2, ParentKeyA = 5001, ParentKeyB = 5002, TagKeyA = 6003, TagKeyB = 6004 }
        ])
        .Insert();

    var joinIntoCounter = new AotQueryCounter();
    context.AddInterceptor(joinIntoCounter);
    try
    {
        var parents = context.From<AotCompositeJunctionParent>()
            .Where(parent => parent.Id == parentId || parent.Id == emptyParentId)
            .OrderBy(parent => parent.Id)
            .JoinInto(
                context.From<AotCompositeJunctionTag>(),
                (parent, tag) => parent.Id > 0,
                parent => parent.Tags)
            .ToList();
        Console.WriteLine($"component-slice=join-into-many-to-many-composite-join-into\tstatus=pass\tvalues={Escape(FormatCompositeJunctionGraph(parents, joinIntoCounter.CommandCount))}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"component-slice=join-into-many-to-many-composite-join-into\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}\tdetails={Escape(ex.ToString())}");
    }

    var loadWithCounter = new AotQueryCounter();
    context.AddInterceptor(loadWithCounter);
    try
    {
        var parents = context.From<AotCompositeJunctionParent>()
            .Where(parent => parent.Id == parentId || parent.Id == emptyParentId)
            .OrderBy(parent => parent.Id)
            .LoadWith(parent => parent.Tags)
            .ToList();
        Console.WriteLine($"component-slice=join-into-many-to-many-composite\tstatus=pass\tvalues={Escape(FormatCompositeJunctionGraph(parents, loadWithCounter.CommandCount))}");
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"component-slice=join-into-many-to-many-composite\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}\tdetails={Escape(ex.ToString())}");
    }
}

static void RunJoinIntoKeyTypeMatrix(string databasePath)
{
    try
    {
        using var context = new SqliteDataContext(
            $"Data Source='{databasePath}';Default Timeout=30", new DataContextBuilder());
        var counter = new AotQueryCounter();
        context.AddInterceptor(counter);

        context.From<AotJunctionTag>();
        context.From<AotJunctionLink>();
        context.From<AotJunctionParent>(builder => builder
            .HasManyThrough<AotJunctionTag, AotJunctionLink, int, int>(
                parent => parent.Tags, parent => parent.Id, link => link.ParentId, tag => tag.Id, link => link.ChildId));

        context.From<AotIntLongChild>();
        context.From<AotIntLongLink>();
        context.From<AotIntLongParent>(builder => builder
            .HasManyThrough<AotIntLongChild, AotIntLongLink, int, long>(
                parent => parent.Children, parent => parent.Id, link => link.ParentId, child => child.Id, link => link.ChildId));

        context.From<AotConvertedKeyLink>();

        context.From<AotNullableKeyChild>();
        context.From<AotNullableKeyLink>();
        context.From<AotNullableKeyParent>(builder => builder
            .HasManyThrough<AotNullableKeyChild, AotNullableKeyLink>(
                parent => parent.Children,
                parent => new { TenantId = parent.TenantId, Id = parent.Id },
                link => new { TenantId = link.ParentTenant, Id = link.ParentId },
                child => child.Id,
                link => link.ChildId));

        RunKeyTypeCase("join-into-key-int-int", () =>
        {
            var parents = context.From<AotJunctionParent>()
                .Where(parent => parent.Id == 1_500_001_000 || parent.Id == 1_500_001_001)
                .OrderBy(parent => parent.Id)
                .JoinInto(context.From<AotJunctionTag>(), (parent, tag) => parent.Id > 0, parent => parent.Tags)
                .ToList();
            return FormatKeyGraph(parents, parent => parent.Id, parent => parent.Tags, tag => tag.Id, counter.CommandCount);
        });

        RunKeyTypeCase("join-into-key-int-long", () =>
        {
            var parents = context.From<AotIntLongParent>()
                .OrderBy(parent => parent.Id)
                .JoinInto(context.From<AotIntLongChild>(), (parent, child) => parent.Id > 0, parent => parent.Children)
                .ToList();
            return FormatKeyGraph(parents, parent => parent.Id, parent => parent.Children, child => child.Id, counter.CommandCount);
        });

        RunKeyTypeCase("join-into-key-converted-int", () =>
        {
            var parents = context.From<AotRelCustomer>()
                .OrderBy(parent => parent.Amount)
                .JoinInto(
                    context.From<AotRelOrder>(),
                    (parent, child) => parent.Id > 0,
                    parent => parent.Orders,
                    options => options.ManyToMany<AotRelCustomer, AotRelOrder, AotConvertedKeyLink, decimal, int>(
                        parent => parent.Amount, link => link.ParentId, child => child.Id, link => link.ChildId))
                .ToList();
            var graph = string.Join(";", parents.OrderBy(parent => parent.Amount)
                .Select(parent =>
                    $"{parent.Amount.ToString(CultureInfo.InvariantCulture)}" +
                    $"[{string.Join(",", parent.Orders.Select(child => child.Id).OrderBy(id => id))}]"));
            return $"graph={graph};commands={counter.CommandCount}";
        });

        RunKeyTypeCase("join-into-key-nullable-composite", () =>
        {
            var parents = context.From<AotNullableKeyParent>()
                .OrderBy(parent => parent.Id)
                .JoinInto(context.From<AotNullableKeyChild>(), (parent, child) => parent.Id > 0, parent => parent.Children)
                .ToList();
            var graph = string.Join(";", parents
                .OrderBy(parent => parent.TenantId is null ? 0 : 1)
                .ThenBy(parent => parent.Id)
                .Select(parent =>
                    $"{parent.TenantId?.ToString(CultureInfo.InvariantCulture) ?? "null"}/{parent.Id}" +
                    $"[{string.Join(",", parent.Children.Select(child => child.Id).OrderBy(id => id))}]"));
            return $"graph={graph};commands={counter.CommandCount}";
        });

        void RunKeyTypeCase(string name, Func<string> run)
        {
            counter.Reset();
            try
            {
                Console.WriteLine($"component-slice={name}\tstatus=pass\tvalues={Escape(run())}");
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"component-slice={name}\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}\tdetails={Escape(ex.ToString())}");
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine(
            $"component-slice=join-into-key-type-registration\tstatus=fail\texception={Escape(ex.GetType().FullName ?? ex.GetType().Name)}\tmessage={Escape(ex.Message)}\tdetails={Escape(ex.ToString())}");
    }
}

static string FormatKeyGraph<TParent, TChild>(
    IEnumerable<TParent> parents,
    Func<TParent, long> parentId,
    Func<TParent, IEnumerable<TChild>> children,
    Func<TChild, long> childId,
    int commandCount)
{
    var graph = string.Join(";", parents.OrderBy(parentId)
        .Select(parent => $"{parentId(parent)}[{string.Join(",", children(parent).Select(childId).OrderBy(id => id))}]"));
    return $"graph={graph};commands={commandCount}";
}

static void RegisterCompositeJunctionNavigation(IDataContext context)
{
    context.From<AotCompositeJunctionTag>();
    context.From<AotCompositeJunctionLink>();
    context.From<AotCompositeJunctionParent>(builder => builder
        .HasManyThrough<AotCompositeJunctionTag, AotCompositeJunctionLink>(
            parent => parent.Tags,
            parent => new { parent.ParentKeyA, parent.ParentKeyB },
            link => new { link.ParentKeyA, link.ParentKeyB },
            tag => new { tag.TagKeyA, tag.TagKeyB },
            link => new { link.TagKeyA, link.TagKeyB }));
}

static void EnsureCompositeJunctionTables(string databasePath)
{
    using var connection = new SqliteConnection($"Data Source='{databasePath}';Default Timeout=30");
    connection.Open();
    foreach (var commandText in new[]
    {
        "CREATE TABLE IF NOT EXISTS aot_composite_parent (id INTEGER PRIMARY KEY, parent_key_a INTEGER NOT NULL, parent_key_b INTEGER NOT NULL, name TEXT NULL)",
        "CREATE TABLE IF NOT EXISTS aot_composite_tag (id INTEGER PRIMARY KEY, tag_key_a INTEGER NOT NULL, tag_key_b INTEGER NOT NULL, name TEXT NULL)",
        "CREATE TABLE IF NOT EXISTS aot_composite_link (id INTEGER PRIMARY KEY, parent_key_a INTEGER NOT NULL, parent_key_b INTEGER NOT NULL, tag_key_a INTEGER NOT NULL, tag_key_b INTEGER NOT NULL)"
    })
    {
        using var command = connection.CreateCommand();
        command.CommandText = commandText;
        command.ExecuteNonQuery();
    }
}

static string FormatJunctionGraph(IEnumerable<AotJunctionParent> parents, int commandCount)
{
    var graph = string.Join(";", parents
        .OrderBy(parent => parent.Id)
        .Select(parent => $"{parent.Id}[{string.Join(",", parent.Tags.OrderBy(tag => tag.Id).Select(tag => $"{tag.Id}:{tag.Name}"))}]"));
    return $"graph={graph};commands={commandCount}";
}

static string FormatCompositeJunctionGraph(IEnumerable<AotCompositeJunctionParent> parents, int commandCount)
{
    var graph = string.Join(";", parents
        .OrderBy(parent => parent.Id)
        .Select(parent => $"{parent.Id}[{string.Join(",", parent.Tags.OrderBy(tag => tag.Id).Select(tag => $"{tag.Id}:{tag.Name}"))}]"));
    return $"graph={graph};commands={commandCount}";
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

static string FormatRelCustomerGraph(IEnumerable<AotRelCustomer> parents, int commandCount)
{
    var graph = string.Join(";", parents
        .OrderBy(parent => parent.Id)
        .Select(parent => $"{parent.Id}[{string.Join(",", parent.Orders.Select(order => order.Id).OrderBy(id => id))}]"));
    return $"graph={graph};commands={commandCount}";
}

static string FormatNestedRelCustomerGraph(IEnumerable<AotRelCustomer> parents, int commandCount)
{
    var graph = string.Join(";", parents.OrderBy(parent => parent.Id)
        .Select(parent =>
            $"{parent.Id}[{string.Join(",", parent.Orders.OrderBy(order => order.Id).Select(order =>
                $"{order.Id}:{order.Approver?.Id.ToString(CultureInfo.InvariantCulture) ?? "null"}"))}]"));
    return $"graph={graph};commands={commandCount}";
}

static string Escape(string value) => value.Replace("\\", "\\\\").Replace("\t", "\\t").Replace("\r", "\\r").Replace("\n", "\\n");

/// <summary>A projection DTO covered by the compile-time row materializer generator.</summary>
[RowMaterializer]
public sealed record AotComplexDto(long Id, int? Int, string? String, decimal? Numeric);

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
    [Column("m")]
    decimal? Numeric { get; set; }
}

[SqlTable("orders")]
public sealed class AotNavOrder
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("buyer_id")]
    public int BuyerId { get; set; }
    [Column("approver_id")]
    public int ApproverId { get; set; }
    public AotNavPerson? Buyer { get; set; }
}

[SqlTable("person")]
public sealed class AotNavPerson
{
    [Key]
    [Column("id")]
    public int Id { get; set; }
    [Column("name")]
    public string? Name { get; set; }
}

internal sealed class AotQueryCounter : IQueryInterceptor
{
    private int _commandCount;

    public int CommandCount => System.Threading.Volatile.Read(ref _commandCount);

    public void CommandExecuting(CommandEventData eventData, System.Data.Common.DbCommand command)
        => System.Threading.Interlocked.Increment(ref _commandCount);

    public void Reset()
        => System.Threading.Interlocked.Exchange(ref _commandCount, 0);
}
