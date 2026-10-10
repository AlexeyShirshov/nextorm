using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using Microsoft.Data.Sqlite;
using NextORM.Core;

namespace NextORM.Sqlite.Tests;

public enum TypedCteState
{
    Unknown,
    Active,
    Closed,
}

[SqlTable("typed_cte_person")]
public interface ITypedCtePerson
{
    [Key]
    [Column("id")]
    long Id { get; set; }

    [Column("total")]
    int Total { get; set; }

    [Column("tenant_id")]
    int TenantId { get; set; }

    [Column("state")]
    [ValueConverter(typeof(EnumToStringConverter<TypedCteState>))]
    TypedCteState State { get; set; }

    [Column("name")]
    string? Name { get; set; }
}

public sealed class TypedCtePerson : ITypedCtePerson
{
    public long Id { get; set; }
    public int Total { get; set; }
    public int TenantId { get; set; }
    public TypedCteState State { get; set; }
    public string? Name { get; set; }
}

[SqlTable("typed_cte_person")]
public sealed class TypedCteScopedPerson
{
    [Key]
    [Column("id")]
    public long Id { get; set; }

    [Column("tenant_id")]
    public int TenantId { get; set; }
}

public sealed record TypedCteRow(long Id, int Total);

public sealed class TypedCteInit
{
    public long Id { get; set; }
    public int Total { get; set; }
}

/// <summary>
/// Real SQLite execution and SQL-text coverage for the ordinary typed CTE surface (#146 slice A).
/// Every provider-agnostic projection form, converter reprojection, dependency graph and cache path
/// is exercised end-to-end against a throwaway file database (or rendered without a connection for
/// the pure SQL assertions).
/// </summary>
public class TypedCteTests
{
    private static (IDataContext Ctx, string Path) CreateDb()
    {
        var path = Path.Combine(Path.GetTempPath(), $"nextorm-typed-cte-{Guid.NewGuid():N}.db");
        using (var conn = new SqliteConnection($"Data Source={path}"))
        {
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText =
                "create table typed_cte_person (id integer primary key, total integer, tenant_id integer, state text, name text);" +
                "insert into typed_cte_person (id, total, tenant_id, state, name) values " +
                "(1, 10, 7, 'Active', 'a'), (2, 20, 7, 'Closed', 'b'), (3, 30, 8, 'Active', 'c');";
            cmd.ExecuteNonQuery();
        }

        var ctx = new SqliteDataContext($"Data Source={path}", new DataContextBuilder());
        ctx.EnsureConnectionOpen();
        return (ctx, path);
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);

    // ---------------------------------------------------------------------------------------------
    // §6 Select forms + §12 type inference: direct read and outer reselect materialize the shape.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AnonymousForm_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>()
                .Select(x => new { x.Id, x.Total })
                .AsCte("recent");

            var rows = ctx.From(recent).Select(r => new { r.Id, r.Total }).ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
            rows.Single(r => r.Id == 2).Total.Should().Be(20);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ScalarForm_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var ids = ctx.From<ITypedCtePerson>().Select(x => x.Id).AsCte("ids");

            var rows = ctx.From(ids).ToList();

            rows.OrderBy(id => id).Should().Equal(1, 2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void CtorDtoForm_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>()
                .Select(x => new TypedCteRow(x.Id, x.Total))
                .AsCte("recent");

            var rows = ctx.From(recent).Select(r => new TypedCteRow(r.Id, r.Total)).ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
            rows.Single(r => r.Id == 3).Total.Should().Be(30);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void MemberInitForm_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>()
                .Select(x => new TypedCteInit { Id = x.Id, Total = x.Total })
                .AsCte("recent");

            var rows = ctx.From(recent).Select(r => new TypedCteInit { Id = r.Id, Total = r.Total }).ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
            rows.Single(r => r.Id == 1).Total.Should().Be(10);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void WholeEntityForm_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var people = ctx.From<ITypedCtePerson>().ToCommand().AsCte("people");

            var rows = ctx.From(people).Select(r => new { r.Id, r.Name }).ToList();

            rows.Select(r => r.Name).OrderBy(n => n).Should().Equal("a", "b", "c");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void NestedProjectionForm_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // A bare join produces Projection<Person, Person>; reading it through a typed CTE flattens the
            // projection items back into a plain shape.
            var pair = ctx.From<ITypedCtePerson>()
                .Join(ctx.From<ITypedCtePerson>(), (a, b) => a.Id == b.Id)
                .ToCommand()
                .AsCte("pair");

            var rows = ctx.From(pair)
                .Select(p => new { A = p.Item1.Id, B = p.Item2.Id })
                .ToList();

            rows.Should().HaveCount(3);
            rows.Single(r => r.A == 2).B.Should().Be(2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // §12 projections: aliased / renamed / computed / reordered slots.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void AliasedComputedAndRenamed_ShouldResolveToOutputAliases()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var shaped = ctx.From<ITypedCtePerson>()
                .Select(x => new { Identifier = x.Id, Doubled = x.Total * 2 })
                .AsCte("shaped");

            var sql = SqlOf(ctx, ctx.From(shaped).Select(r => new { r.Identifier, r.Doubled }));
            sql.Should().Contain("select id as 'Identifier', (total * 2) as 'Doubled' from typed_cte_person");
            sql.Should().Contain("from shaped");

            var rows = ctx.From(shaped).Select(r => new { r.Identifier, r.Doubled }).ToList();
            rows.Single(r => r.Identifier == 2).Doubled.Should().Be(40);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ReorderedSlots_ShouldBindByAliasNotPosition()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Projection order differs from the physical column order; the outer reselect asks for Id and
            // Total in a third order and must read each by its output alias.
            var reordered = ctx.From<ITypedCtePerson>()
                .Select(x => new { Total = x.Total, Id = x.Id })
                .AsCte("reordered");

            var rows = ctx.From(reordered).Select(r => new { r.Id, r.Total }).ToList();

            rows.Single(r => r.Id == 3).Total.Should().Be(30);
            rows.Single(r => r.Total == 10).Id.Should().Be(1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // §12 converters: end-to-end plus outer member reprojection without double conversion.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Converter_WholeEntityBody_ShouldReprojectMember()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var people = ctx.From<ITypedCtePerson>()
                .Where(x => x.Id > 0)
                .ToCommand()
                .AsCte("people");

            var rows = ctx.From(people).Select(r => r.State).ToList();

            rows.OrderBy(s => s).Should().Equal(TypedCteState.Active, TypedCteState.Active, TypedCteState.Closed);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Converter_ProjectedBody_ShouldRoundTripWithoutDoubleConversion()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var projected = ctx.From<ITypedCtePerson>()
                .Select(x => new { x.Id, x.State })
                .AsCte("projected");

            var direct = ctx.From(projected).Select(r => new { r.Id, r.State }).ToList();

            direct.Single(r => r.Id == 1).State.Should().Be(TypedCteState.Active);
            direct.Single(r => r.Id == 2).State.Should().Be(TypedCteState.Closed);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // §12 multiple CTE: heterogeneous, same CLR type, self-join.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MultipleHeterogeneousCtes_ShouldJoinAndMaterialize()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var byId = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Name }).AsCte("by_id");
            var byTotal = ctx.From<ITypedCtePerson>().Select(x => new { x.Total, x.Name }).AsCte("by_total");

            var rows = ctx.From(byId)
                .Join(ctx.From(byTotal), (a, b) => a.Name == b.Name)
                .Select(p => new { p.Item1.Id, p.Item2.Total })
                .ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void TwoCtesOfSameClrType_ShouldBindBySourceSlot()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var high = ctx.From<ITypedCtePerson>().Where(x => x.Total >= 20).Select(x => new { x.Id, x.Total }).AsCte("high");
            var everything = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("everything");

            var rows = ctx.From(high)
                .Join(ctx.From(everything), (a, b) => a.Id == b.Id)
                .Select(p => new { p.Item1.Id, p.Item2.Total })
                .ToList();

            // 'high' only keeps ids 2 and 3; binding by slot (not by shared CLR type) keeps the predicates apart.
            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void SelfJoinOfSameDescriptor_ShouldMaterializeBothSides()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var people = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("p");

            var rows = ctx.From(people)
                .Join(ctx.From(people), (a, b) => a.Id == b.Id)
                .Select(p => new { p.Item1.Id, p.Item2.Total })
                .ToList();

            rows.Should().HaveCount(3);
            rows.Single(r => r.Id == 2).Total.Should().Be(20);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // §7 dependencies: nested/derived bodies hoist dependency-before-consumer.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void NestedCteBody_ShouldHoistDependencyBeforeConsumer()
    {
        using var ctx = SqliteTestContext.Create();

        var inner = ctx.From<ITypedCtePerson>().Where(x => x.Total >= 20).Select(x => new { x.Id, x.Total }).AsCte("i");
        var outer = ctx.From(inner).Select(x => new { x.Id }).AsCte("o");

        var sql = SqlOf(ctx, ctx.From(outer).Select(x => x.Id));

        // The descriptor hoists the dependency before its consumer into one top-level WITH. Every typed
        // CTE body aliases its columns to the projection property names (case-exactly), and the consumer
        // references that output name — the agreement PostgreSQL requires for its case-sensitive quoted
        // identifiers.
        sql.Should().Contain("with i as (select id as 'Id', total as 'Total' from typed_cte_person");
        sql.Should().Contain("), o as (select Id from i as 't1')");
        sql.Should().Contain("from o as 't1'");
    }

    // ---------------------------------------------------------------------------------------------
    // §6/§12 filters: only inside the defining body, never re-applied on the typed read.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void MainTypedSourceFilter_ShouldApplyOnlyInsideBody()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var scoped = ctx.From<TypedCteScopedPerson>(b => b.HasQueryFilter(e => e.TenantId == 7))
                .ToCommand()
                .AsCte("scoped");

            var sql = SqlOf(ctx, ctx.From(scoped).Select(r => r.Id));
            sql.Should().Contain("where tenant_id = 7");
            sql.Should().NotContain("t1.tenant_id = 7");

            var rows = ctx.From(scoped).Select(r => r.Id).ToList();
            rows.OrderBy(id => id).Should().Equal(1, 2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // §9/§12 cache: cached, prepared-no-cache, descriptor reuse, changed captured params.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void DescriptorReuse_AcrossCommands_ShouldMaterializeIndependently()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cte = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("shared");

            var high = ctx.From(cte).Where(x => x.Total >= 20).Select(x => x.Id).ToList();
            var low = ctx.From(cte).Where(x => x.Total < 20).Select(x => x.Id).ToList();

            high.OrderBy(id => id).Should().Equal(2, 3);
            low.Should().Equal(1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void CachedPlan_ShouldReturnCorrectRowsRepeatedly()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cte = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("cached");
            var command = ctx.From(cte).Select(x => x.Total);

            var first = command.ToList();
            var second = command.ToList();

            first.OrderBy(t => t).Should().Equal(10, 20, 30);
            second.OrderBy(t => t).Should().Equal(10, 20, 30);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void PreparedNoCache_ShouldStillMaterialize()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cte = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("local");
            var command = ctx.From(cte).Select(x => x.Total);

            var prepared = ctx.GetPreparedQueryCommand(command, false, false, CancellationToken.None);
            var rows = prepared.ToList(ctx);

            rows.OrderBy(t => t).Should().Equal(10, 20, 30);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void CachedPlan_ChangedCapturedParam_ShouldRefreshResults()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var cte = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("param");
            var threshold = 15;
            var command = ctx.From(cte).Where(x => x.Total > threshold).Select(x => x.Id);

            var first = command.ToList();
            threshold = 25;
            var second = command.ToList();

            first.OrderBy(id => id).Should().Equal(2, 3);
            second.OrderBy(id => id).Should().Equal(3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ChainedCte_SameProjectionType_ShouldMaterializeAndRenderInnerReference()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var inner = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("i");
            var outer = ctx.From(inner).Select(o => new { o.Id, o.Total }).AsCte("o");

            var sql = SqlOf(ctx, ctx.From(outer).Select(x => new { x.Id, x.Total }));
            sql.Should().Contain("with i as (").And.Contain(", o as (").And.Contain("from i").And.Contain("from o");

            var rows = ctx.From(outer).Select(x => new { x.Id, x.Total }).ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
            rows.Single(r => r.Id == 2).Total.Should().Be(20);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void NestedReadCte_SyncTerminal_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Outer read CTE whose body declares an inner read CTE: the nested-read graph under test.
            var inner = ctx.From<ITypedCtePerson>()
                .Select(x => new { x.Id, x.Total })
                .AsCte("nested_sync_inner");
            var outer = ctx.From(inner)
                .Select(o => new { o.Id, o.Total })
                .AsCte("nested_sync_outer");

            var command = ctx.From(outer).Select(x => new { x.Id, x.Total });

            var first = command.ToList();
            first.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
            first.Single(r => r.Id == 2).Total.Should().Be(20);

            // Behavioural warm-call check: a read-only nested CTE must execute again through the same
            // (prepared/reused) command and still materialize the independently specified rows.
            var second = command.ToList();
            second.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
            second.Single(r => r.Id == 2).Total.Should().Be(20);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task NestedReadCte_AsyncTerminal_Executes()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var inner = ctx.From<ITypedCtePerson>()
                .Select(x => new { x.Id, x.Total })
                .AsCte("nested_async_inner");
            var outer = ctx.From(inner)
                .Select(o => new { o.Id, o.Total })
                .AsCte("nested_async_outer");

            var rows = await ctx.From(outer).Select(x => new { x.Id, x.Total }).ToListAsync();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1, 2, 3);
            rows.Single(r => r.Id == 2).Total.Should().Be(20);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ChainedCte_DifferingProjectionType_ShouldMaterializeAndRenderInnerReference()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var inner = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("i");
            var outer = ctx.From(inner).Select(o => new { o.Id }).AsCte("o");

            var sql = SqlOf(ctx, ctx.From(outer).Select(x => x.Id));

            // The producer body aliases its columns to the projection property names and the consumer
            // body references that exact output name (no physical `id` leaking across the boundary).
            sql.Should().Contain("with i as (select id as 'Id', total as 'Total' from typed_cte_person");
            sql.Should().Contain(", o as (select Id from i as 't1')");
            sql.Should().Contain("from o as 't1'");

            var rows = ctx.From(outer).Select(x => x.Id).ToList();

            rows.OrderBy(id => id).Should().Equal(1, 2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    /// <summary>
    /// Several heterogeneous typed CTEs where one depends on another: each declaration body aliases its
    /// columns to the projection property names, so the dependent body and the joined independent CTE
    /// both reference the declared output name instead of a physical column name. Mirrors the live
    /// PostgreSQL case-sensitive failure (defect-3) without a server.
    /// </summary>
    [Fact]
    public void HeterogeneousCtesWithDependency_ShouldReferenceDeclaredColumnNames()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var dependency = ctx.From<ITypedCtePerson>()
                .Where(x => x.Id > 1)
                .Select(x => new { x.Id, x.Total })
                .AsCte("dep");
            var consumer = ctx.From(dependency)
                .Select(d => new { d.Id })
                .AsCte("consumer");
            var other = ctx.From<ITypedCtePerson>()
                .Where(x => x.Id < 9)
                .Select(x => new { x.Id, x.Total })
                .AsCte("other");

            var command = ctx.From(consumer)
                .Join(ctx.From(other), (a, b) => a.Id == b.Id)
                .Select(p => new { p.Item1.Id });

            var sql = SqlOf(ctx, command);
            sql.Should().Contain("dep as (select id as 'Id', total as 'Total' from typed_cte_person");
            sql.Should().Contain("consumer as (select Id from dep as 't1')");
            sql.Should().Contain("other as (select id as 'Id', total as 'Total' from typed_cte_person");

            var rows = ctx.From(consumer)
                .Join(ctx.From(other), (a, b) => a.Id == b.Id)
                .Select(p => new { p.Item1.Id })
                .ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    /// <summary>
    /// §7: a CTE whose body is a set operation is hoisted as one declaration and materializes through
    /// the typed read; a second CTE that consumes it is ordered after it (dependency-before-consumer).
    /// </summary>
    [Fact]
    public void UnionBody_ShouldMaterializeAndHoistConsumer()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var high = ctx.From<ITypedCtePerson>().Where(x => x.Total >= 20).Select(x => new { x.Id, x.Total });
            var low = ctx.From<ITypedCtePerson>().Where(x => x.Total < 20).Select(x => new { x.Id, x.Total });

            var union = high.Union(low).AsCte("u");

            var sql = SqlOf(ctx, ctx.From(union).Select(x => x.Id));
            sql.Should().Contain("with u as (").And.Contain("union");
            sql.Should().Contain("from u");

            var rows = ctx.From(union).Select(x => x.Id).ToList();
            rows.OrderBy(id => id).Should().Equal(1, 2, 3);

            // A consumer CTE depends on the set-operation body.
            var consumer = ctx.From(union).Select(x => new { x.Id }).AsCte("c");
            consumer.Definitions.Select(d => d.Name).Should().Equal("u", "c");

            var consumed = ctx.From(consumer).Select(x => x.Id).ToList();
            consumed.OrderBy(id => id).Should().Equal(1, 2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // #146-A audit: preparation-reset operators (Distinct/OrderBy/paging/Last) over a typed CTE
    // source must preserve the projection source and materialize correctly (not just render SQL).
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void Distinct_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.TenantId }).AsCte("recent");

            var tenants = ctx.From(recent).Select(r => r.TenantId).Distinct().ToList();

            tenants.OrderBy(t => t).Should().Equal(7, 8);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void OrderByIndex_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("recent");

            var ids = ctx.From(recent).Select(r => r.Id).OrderBy(1).ToList();

            ids.Should().Equal(1, 2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void OrderByExpression_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("recent");

            var rows = ctx.From(recent).Select(r => new { r.Id }).OrderBy(r => r.Id).ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2, 3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Paging_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("recent");

            var rows = ctx.From(recent).Select(r => new { r.Id }).OrderBy(r => r.Id).Limit(2).ToList();

            rows.Select(r => r.Id).Should().Equal(1, 2);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Last_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var recent = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.Total }).AsCte("recent");

            var last = ctx.From(recent).Select(r => new { r.Id }).OrderBy(r => r.Id).Last();

            last.Id.Should().Be(3);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ComputedScalarBody_ShouldMaterializeThroughTypedCte()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Audit candidate 4: an unaliased computed scalar body column has no PropertyName.
            var doubled = ctx.From<ITypedCtePerson>().Select(x => x.Total * 2).AsCte("doubled");

            var values = ctx.From(doubled).ToList();

            values.OrderBy(v => v).Should().Equal(20, 40, 60);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void ConverterMemberOnJoinedTypedCte_ShouldRoundTrip()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Audit candidate 6: the converter must resolve for a member read on the joined (right)
            // typed CTE, not only on the main `_from` source.
            var left = ctx.From<ITypedCtePerson>().Select(x => new { x.Id }).AsCte("l");
            var right = ctx.From<ITypedCtePerson>().Select(x => new { x.Id, x.State }).AsCte("r");

            var rows = ctx.From(left)
                .Join(ctx.From(right), (a, b) => a.Id == b.Id)
                .Select(p => new { p.Item1.Id, p.Item2.State })
                .ToList();

            rows.Single(r => r.Id == 2).State.Should().Be(TypedCteState.Closed);
            rows.Single(r => r.Id == 1).State.Should().Be(TypedCteState.Active);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // #146-B: typed recursive CTE executes end-to-end on SQLite.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public void RecursiveNumbers_ShouldMaterializeAnchorAndBoundedStep()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Scalar anchor: id 1; step: add 1 while the current value is below 5 -> 1..5.
            var nums = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id == 1)
                .Select(p => p.Id)
                .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));

            var sql = SqlOf(ctx, ctx.From(nums).Select(n => n));
            sql.Should().Contain("with recursive");
            sql.Should().Contain("union all");
            // The step's scalar self-reference and its terminating predicate must both render before any
            // execution; a broken predicate would otherwise recurse without bound.
            sql.Should().Contain("Id < 5");
            sql.Should().Contain("(Id + 1)").And.NotContain("(Id + 1)Id");

            var rows = ctx.From(nums).Limit(20).ToList();
            rows.OrderBy(n => n).Should().Equal(1L, 2L, 3L, 4L, 5L);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void Recursive_MultipleSelfReferenceOccurrences_ShouldReferenceCteOutputNameForEachOccurrence()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // V12: the same self-reference is read at two positions of one step expression. Standard SQL
            // permits only one recursive-table reference in the step's FROM (a self-join is rejected by
            // every engine), so multiple occurrences means multiple member reads: each must render the
            // CTE output alias, and the bounded recursion must terminate and materialize.
            var nums = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id })
                .AsRecursiveCte("nums", self => ctx.From(self)
                    .Where(n => n.Id < 5)
                    .Select(n => new { Id = n.Id + n.Id + 1 }));

            var sql = SqlOf(ctx, ctx.From(nums).Limit(20).Select(n => n.Id));

            sql.Should().Contain("with recursive");
            sql.Should().Contain("union all");
            sql.Should().Contain("from nums");
            sql.Should().Contain("Id + Id");
            sql.Should().NotContain("t1.1");

            // 1 -> (1+1+1)=3 -> (3+3+1)=7; 7 is not < 5, so the step stops.
            var rows = ctx.From(nums).Limit(20).Select(n => n.Id).ToList();
            rows.OrderBy(n => n).Should().Equal(1L, 3L, 7L);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void RecursiveConstantAnchor_ShouldReferenceDeclaredOutputAlias()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // D8 regression: the anchor projects a bare constant, so it has no physical column to
            // re-render. The declaration must alias it to the projection name and the recursive step
            // must read that CTE output alias — inlining the anchor body produced `t1.1`, a syntax
            // error on SQLite and an unaddressable column everywhere.
            var nums = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id <= 2)
                .Select(_ => new { n = 1 })
                .AsRecursiveCte("nums", self => ctx.From(self)
                    .Where(r => r.n < 1)
                    .Select(r => new { n = r.n + 1 }));

            var sql = SqlOf(ctx, ctx.From(nums).Limit(20).Select(r => r.n));

            sql.Should().Contain("select 1 as 'n'");
            sql.Should().Contain("select (n + 1) as 'n'");
            sql.Should().Contain("(t1.n < 1)");
            sql.Should().NotContain("t1.1").And.NotContain("(1 + 1)");

            var rows = ctx.From(nums).Limit(20).Select(r => r.n).ToList();

            // The step predicate never fires (n < 1 is false for the anchor rows), so both duplicate
            // anchor rows survive; UNION ALL (not UNION) is what keeps them.
            rows.Should().HaveCount(2).And.OnlyContain(n => n == 1);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void RecursiveAnonymousForm_ShouldPreserveSlotOrder()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // DTO/anonymous shape: two slots, the step recomputes both from the self-reference and
            // advances Id. The anchor's slot order/names must drive the recursive CTE's columns, not
            // the step's aliases. Without advancing Id the step would reuse the same row forever.
            var nums = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id == 1)
                .Select(p => new { p.Id, Next = p.Id + 1 })
                .AsRecursiveCte("nums", self => ctx.From(self)
                    .Where(n => n.Id < 4)
                    .Select(n => new { Id = n.Id + 1, Next = n.Next + 1 }));

            var sql = SqlOf(ctx, ctx.From(nums).Select(n => new { n.Id, n.Next }));
            sql.Should().Contain("with recursive");
            sql.Should().Contain("union all");
            // The step resolves the self-reference through the anchor's declared output aliases
            // (`Id`/`Next`), not the anchor's physical `id` column.
            sql.Should().Contain("t1.Id < 4");
            sql.Should().Contain("(Id + 1)").And.Contain("(Next + 1)");

            var rows = ctx.From(nums).Select(n => new { n.Id, n.Next }).Limit(20).ToList();

            rows.Select(r => r.Id).OrderBy(id => id).Should().Equal(1L, 2L, 3L, 4L);
            rows.OrderBy(r => r.Id).Select(r => r.Next).Should().Equal(2L, 3L, 4L, 5L);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void RecursiveCapturedReference_ShouldThrowBeforeAnyCommand()
    {
        var (ctx, path) = CreateDb();
        try
        {
            CteReference<long>? captured = null;
            _ = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id == 1)
                .Select(p => p.Id)
                .AsRecursiveCte("nums", self =>
                {
                    captured = self;
                    return ctx.From(self).Where(n => n < 3).Select(n => n + 1);
                });

            captured.Should().NotBeNull();
            var act = () => ctx.From(captured!);

            act.Should().Throw<InvalidOperationException>().WithMessage("*outside its defining step*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void RecursiveCallback_ShouldRunOnceAcrossPreparationAndExecution()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var calls = 0;
            var nums = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id == 1)
                .Select(p => p.Id)
                .AsRecursiveCte("nums", self =>
                {
                    calls++;
                    return ctx.From(self).Where(n => n < 5).Select(n => n + 1);
                });

            calls.Should().Be(1);

            var sql = SqlOf(ctx, ctx.From(nums).Limit(20).Select(n => n));
            sql.Should().Contain("union all");

            var rows = ctx.From(nums).Limit(20).ToList();

            rows.OrderBy(n => n).Should().Equal(1L, 2L, 3L, 4L, 5L);
            calls.Should().Be(1, "preparation and execution must not re-invoke the step callback");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void RecursiveCachedPlan_ShouldReuseAndReturnCorrectRows()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var nums = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id == 1)
                .Select(p => p.Id)
                .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 5).Select(n => n + 1));

            // The same command executed twice: cache miss then hit, both from the recursive body.
            var command = ctx.From(nums).Limit(20).Select(n => n);

            var first = command.ToList();
            var second = command.ToList();

            first.OrderBy(n => n).Should().Equal(1L, 2L, 3L, 4L, 5L);
            second.OrderBy(n => n).Should().Equal(1L, 2L, 3L, 4L, 5L);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void RecursiveCacheHit_ShouldNotBypassShapeValidation()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Warm the plan cache under the name 'nums' with a valid recursive definition.
            var good = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id == 1)
                .Select(p => p.Id)
                .AsRecursiveCte("nums", self => ctx.From(self).Where(n => n < 3).Select(n => n + 1));
            ctx.From(good).Limit(20).ToList().Should().HaveCount(3);

            // A distinct definition under the same name with a one-column step must still be rejected
            // during preparation: a cached plan for the name may not bypass the anchor/step contract.
            var bad = ctx.From<ITypedCtePerson>()
                .Select(p => new TypedCteInit { Id = p.Id, Total = p.Total })
                .AsRecursiveCte("nums", self => ctx.From(self).Select(p => new TypedCteInit { Id = p.Id }));

            // The consumer must project through a supported form: a whole-DTO read of the CTE would hit
            // the unsupported whole-row projection before the CTE body is validated, masking the shape
            // diagnostic this test pins.
            var act = () => ctx.From(bad).Limit(20).Select(r => new { r.Id, r.Total }).ToList();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*'nums'*anchor projects 2 column(s)*step projects 1*");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // #159: a Cte<T> descriptor passed straight to the seven join operators must render exactly like
    // converting it with the receiving context first (ctx.From(cte)): same SQL, bound parameters and
    // materialized shape. The descriptor carries its own dependency/filter state across the boundary.
    // ---------------------------------------------------------------------------------------------

    private static (string Sql, object?[] Parameters) Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
    {
        var prepared = (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);
        var parameters = prepared.DbCommand.Parameters.Cast<DbParameter>().Select(p => p.Value).ToArray();
        return (Normalize(prepared.DbCommand.CommandText), parameters);
    }

    [Theory]
    [InlineData("Join")]
    [InlineData("LeftJoin")]
    [InlineData("RightJoin")]
    [InlineData("FullJoin")]
    public void DirectTypedCte_ConditionalOperators_ShouldMatchConvertedSqlAndParameters(string operation)
    {
        var (ctx, path) = CreateDb();
        try
        {
            // A captured value makes both forms bind the same parameter inside the CTE body.
            var minId = 1L;
            var left = ctx.From<ITypedCtePerson>()
                .Where(p => p.Id > minId)
                .Select(p => new { p.Id })
                .AsCte("l");
            var right = ctx.From<ITypedCtePerson>()
                .Select(p => new { p.Id, p.Total })
                .AsCte("r");

            QueryCommand<long> Direct() => operation switch
            {
                "Join" => ctx.From(left).Join(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                "LeftJoin" => ctx.From(left).LeftJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                "RightJoin" => ctx.From(left).RightJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                "FullJoin" => ctx.From(left).FullJoin(right, (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                _ => throw new NotSupportedException(),
            };
            QueryCommand<long> Converted() => operation switch
            {
                "Join" => ctx.From(left).Join(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                "LeftJoin" => ctx.From(left).LeftJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                "RightJoin" => ctx.From(left).RightJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                "FullJoin" => ctx.From(left).FullJoin(ctx.From(right), (a, b) => a.Id == b.Id).Select(p => p.Item1.Id),
                _ => throw new NotSupportedException(),
            };

            var direct = Prepare(ctx, Direct());
            var converted = Prepare(ctx, Converted());

            direct.Sql.Should().Be(converted.Sql);
            direct.Parameters.Should().Equal(converted.Parameters);
            direct.Sql.Should().Contain("with l as (").And.Contain(", r as (").And.Contain("join r as");
            // The body predicate stays inside the CTE declaration; the outer read never re-applies it.
            direct.Sql.Should().NotContain("t2.Id >");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DirectTypedCte_CrossJoin_ShouldMatchConvertedSqlAndParameters()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var left = ctx.From<ITypedCtePerson>().Select(p => new { p.Id }).AsCte("l");
            var right = ctx.From<ITypedCtePerson>().Select(p => new { p.Id }).AsCte("r");

            var direct = Prepare(ctx, ctx.From(left).CrossJoin(right).Select(p => p.Item1.Id));
            var converted = Prepare(ctx, ctx.From(left).CrossJoin(ctx.From(right)).Select(p => p.Item1.Id));

            direct.Sql.Should().Be(converted.Sql);
            direct.Parameters.Should().Equal(converted.Parameters);
            direct.Sql.Should().Contain("cross join r as");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Theory]
    [InlineData("CrossApply")]
    [InlineData("OuterApply")]
    public void DirectTypedCte_Apply_UnsupportedDialect_ShouldMatchConvertedRejection(string operation)
    {
        var (ctx, path) = CreateDb();
        try
        {
            var left = ctx.From<ITypedCtePerson>().Select(p => new { p.Id }).AsCte("l");
            var right = ctx.From<ITypedCtePerson>().Select(p => new { p.Id }).AsCte("r");

            QueryCommand<long> Direct = operation == "CrossApply"
                ? ctx.From(left).CrossApply(right).Select(p => p.Item1.Id)
                : ctx.From(left).OuterApply(right).Select(p => p.Item1.Id);
            QueryCommand<long> Converted = operation == "CrossApply"
                ? ctx.From(left).CrossApply(ctx.From(right)).Select(p => p.Item1.Id)
                : ctx.From(left).OuterApply(ctx.From(right)).Select(p => p.Item1.Id);

            // SQLite has no lateral source: the direct form must keep the exact full-form rejection.
            var directEx = Record.Exception(() => Prepare(ctx, Direct));
            var convertedEx = Record.Exception(() => Prepare(ctx, Converted));

            directEx.Should().BeOfType<NotSupportedException>();
            convertedEx.Should().BeOfType<NotSupportedException>();
            directEx!.Message.Should().Be(convertedEx!.Message);
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DirectTypedCte_Join_ShouldMaterializeSameShapeAsConverted()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var left = ctx.From<ITypedCtePerson>()
                .Where(p => p.Total >= 20)
                .Select(p => new { p.Id, p.Total })
                .AsCte("l");
            var right = ctx.From<ITypedCtePerson>()
                .Select(p => new { p.Id, p.Name })
                .AsCte("r");

            var direct = ctx.From(left)
                .Join(right, (a, b) => a.Id == b.Id)
                .Select(p => new { p.Item1.Id, p.Item1.Total, p.Item2.Name })
                .ToList();
            var converted = ctx.From(left)
                .Join(ctx.From(right), (a, b) => a.Id == b.Id)
                .Select(p => new { p.Item1.Id, p.Item1.Total, p.Item2.Name })
                .ToList();

            direct.Should().BeEquivalentTo(converted);
            // 'l' keeps ids 2 and 3; binding by source slot (not shared CLR type) preserves each side.
            direct.Select(r => r.Id).OrderBy(id => id).Should().Equal(2, 3);
            direct.Single(r => r.Id == 2).Total.Should().Be(20);
            direct.Single(r => r.Id == 3).Name.Should().Be("c");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public void DirectTypedCte_FilteredBody_ShouldKeepFilterOnlyInsideBody()
    {
        var (ctx, path) = CreateDb();
        try
        {
            var scoped = ctx.From<TypedCteScopedPerson>(b => b.HasQueryFilter(e => e.TenantId == 7))
                .ToCommand()
                .AsCte("scoped");
            var plain = ctx.From<TypedCteScopedPerson>().Select(p => new { p.Id, p.TenantId }).AsCte("plain");

            var direct = Prepare(ctx, ctx.From(plain)
                .Join(scoped, (a, b) => a.Id == b.Id)
                .Select(p => p.Item1.Id));
            var converted = Prepare(ctx, ctx.From(plain)
                .Join(ctx.From(scoped), (a, b) => a.Id == b.Id)
                .Select(p => p.Item1.Id));

            direct.Sql.Should().Be(converted.Sql);
            direct.Sql.Should().Contain("where tenant_id = 7");
            // The entity filter is not re-injected through the join continuation.
            direct.Sql.Should().NotContain("t2.tenant_id = 7");
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    // ---------------------------------------------------------------------------------------------
    // E159-20: a TableAlias receiver join carrying an explicit join source resolves that source
    // through JoinSourceResolver (EntityBuilder.cs:4478). The resolved `From` only shapes the join
    // SQL / column binding (QueryCommand.QueryPreparer.cs:1153 PrepareFrom, :1384-1422
    // InjectJoinFilters); the terminal path does not branch on it. These tests pin that the streaming
    // terminals over the same prepared command materialize exactly the same rows as the buffered
    // ToList materialization. r=2 is evidence/test-only: it changes no product behaviour.
    // ---------------------------------------------------------------------------------------------

    [Fact]
    public async Task TableAliasReceiver_JoinWithTypedCte_StreamingTerminalsMatchBuffered()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Explicit join source: a typed CTE, so JoinSourceResolver installs the CTE source instead
            // of the joined type's entity metadata.
            var cte = ctx.From<ITypedCtePerson>()
                .Where(p => p.Total >= 20)
                .Select(p => new { p.Id, p.Total })
                .AsCte("stream_joined_cte");

            var command = ctx.From("typed_cte_person")
                .Join(cte, (t, c) => t.GetInt64("id") == c.Id)
                .Select(p => new { LeftId = p.Item1.GetInt64("id"), RightTotal = p.Item2.Total });

            var buffered = command.ToList();

            // QueryCommandExtensions.ToDataReader<TResult> over the same prepared QueryCommand.
            var fromReader = new List<(long LeftId, int RightTotal)>();
            using (var reader = command.ToDataReader())
            {
                while (reader.Read())
                    fromReader.Add((reader.GetInt64(0), reader.GetInt32(1)));
            }

            // QueryCommand<TResult>.ToAsyncEnumerable over the same command.
            var fromAsync = new List<(long LeftId, int RightTotal)>();
            await foreach (var row in command.ToAsyncEnumerable())
                fromAsync.Add((row.LeftId, row.RightTotal));

            buffered.Should().NotBeEmpty();
            buffered.Select(r => r.LeftId).OrderBy(id => id).Should().Equal(2, 3);
            fromReader.Should().Equal(buffered.Select(r => (r.LeftId, r.RightTotal)));
            fromAsync.Should().Equal(buffered.Select(r => (r.LeftId, r.RightTotal)));
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

    [Fact]
    public async Task TableAliasReceiver_JoinWithDerivedSource_AsyncEnumerableMatchesBuffered()
    {
        var (ctx, path) = CreateDb();
        try
        {
            // Explicit join source: a derived query, so JoinSourceResolver installs a sub-query source.
            var derived = ctx.From(ctx.From<ITypedCtePerson>()
                .Where(p => p.Total >= 20)
                .Select(p => new { p.Id, p.Total }));

            var command = ctx.From("typed_cte_person")
                .Join(derived, (t, d) => t.GetInt64("id") == d.Id)
                .Select(p => new { LeftId = p.Item1.GetInt64("id"), RightTotal = p.Item2.Total });

            var buffered = command.ToList();

            var streamed = new List<(long LeftId, int RightTotal)>();
            await foreach (var row in command.ToAsyncEnumerable())
                streamed.Add((row.LeftId, row.RightTotal));

            buffered.Should().NotBeEmpty();
            buffered.Select(r => r.LeftId).OrderBy(id => id).Should().Equal(2, 3);
            streamed.Should().Equal(buffered.Select(r => (r.LeftId, r.RightTotal)));
        }
        finally { ctx.Dispose(); File.Delete(path); }
    }

}

