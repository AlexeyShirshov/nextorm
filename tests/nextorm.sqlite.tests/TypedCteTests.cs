using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
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

}

