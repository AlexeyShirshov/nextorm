using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Data.Common;
using FluentAssertions;
using NextORM.Core;
using NextORM.Sqlite;

namespace NextORM.Sqlite.Tests;

[SqlTable("pair_entity")]
public interface IPairEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
    [Column("a")]
    int A { get; set; }
    [Column("b")]
    int B { get; set; }
}

[SqlTable("pair_inner")]
public interface IPairInnerEntity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

[SqlTable("pair_inner2")]
public interface IPairInner2Entity
{
    [Key]
    [Column("id")]
    int Id { get; set; }
}

/// <summary>
/// SQL generation for correlated subqueries: a subquery that references a column of the enclosing
/// query. These tests never open a database connection, so they run on every build/CI.
/// </summary>
public class CorrelatedQueryTests
{
    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static DbPreparedQueryCommand<T> Prepare<T>(IDataContext ctx, QueryCommand<T> cmd)
        => (DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None);

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd) => Normalize(Prepare(ctx, cmd).DbCommand.CommandText);

    [Fact]
    public void CorrelatedScalarInSelect_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            sid = inner.Where(s => s.Id == it.Id).Select(s => s.Id).First()
        }));

        sql.Should().Be("select t1.id, (select t2.id from simple_entity as 't2'\n"
            + " where cast(t2.id as bigint) = t1.id\n"
            + "limit 1) as 'sid' from complex_entity as 't1'");
    }

    [Fact]
    public void CorrelatedScalarInWhere_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer
            .Where(it => it.Id == inner.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id }));

        sql.Should().Be("select t1.id from complex_entity as 't1'\n"
            + " where t1.id = (select t2.id from simple_entity as 't2'\n"
            + " where cast(t2.id as bigint) = t1.id\n"
            + "limit 1)");
    }

    [Fact]
    public void CorrelatedScalarInOrderBy_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer
            .OrderBy(it => inner.Where(s => s.Id == it.Id).Select(s => s.Id).First())
            .Select(it => new { it.Id }));

        sql.Should().Be("select t1.id from complex_entity as 't1'\n"
            + " order by (select t2.id from simple_entity as 't2'\n"
            + " where cast(t2.id as bigint) = t1.id\n"
            + "limit 1)");
    }

    /// <summary>
    /// A correlated subquery may reference a column of a join-projection item
    /// (<c>p.Item1.Id</c>). The marker wraps the projection item, not a plain entity parameter, so
    /// the member has to be resolved through the item's position; before this was handled the
    /// translation evaluated the marker's unset <c>Ref</c> and threw.
    /// </summary>
    [Fact]
    public void CorrelatedScalarOnJoinProjection_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>().Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id);
        var inner = ctx.From<IPairInnerEntity>();

        var sql = SqlOf(ctx, outer.Select(p => new
        {
            p.Item1.Id,
            x = inner.Where(i => i.Id == p.Item1.Id).Select(i => i.Id).First()
        }));

        sql.Should().Be("select t1.id, (select t3.id from pair_inner as 't3'\n"
            + " where cast(t3.id as bigint) = t1.id\n"
            + "limit 1) as 'x' from complex_entity as 't1' join simple_entity as 't2' on t1.id = cast(t2.id as bigint)");
    }

    /// <summary>
    /// The projection position of the referenced item selects the table alias: an outer reference to
    /// the second join item must render the second source's alias, not the first one.
    /// </summary>
    [Fact]
    public void CorrelatedExistsOnJoinProjection_ShouldReferenceTheSecondItemAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>().Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id);
        var inner = ctx.From<IPairInnerEntity>();

        var sql = SqlOf(ctx, outer.Select(p => new
        {
            p.Item1.Id,
            has = SqlFunctions.Sql.exists(inner.Where(i => i.Id == p.Item2.Id))
        }));

        sql.Should().Contain("t3.id = t2.id");
        sql.Should().Contain("from pair_inner as 't3'");
    }

    /// <summary>
    /// A correlated reference may be wrapped in a function over the outer column (<c>upper(c.String)</c>):
    /// the member translator rewrites the outer column to its marker and the ordinary scalar translator
    /// renders the call around it.
    /// </summary>
    [Fact]
    public void CorrelatedFunctionOverOuterColumn_ShouldRenderAroundOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<IComplexEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            x = inner.Where(s => s.String == it.String!.ToUpper()).Select(s => s.Id).First()
        }));

        sql.Should().Contain("upper(t1.somestring)");
        sql.Should().Contain("t2.somestring");
    }

    [Fact]
    public void CorrelatedExistsInSelect_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            has = SqlFunctions.Sql.exists(inner.Where(s => s.Id == it.Id))
        }));

        sql.Should().Be("select t1.id, exists(select * from simple_entity as 't2'\n"
            + " where cast(t2.id as bigint) = t1.id) from complex_entity as 't1'");
    }

    [Fact]
    public void CorrelatedInInSelect_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            inc = SqlFunctions.Sql.@in((int)it.Id, inner.Where(s => s.Id == it.Id).Select(s => s.Id))
        }));

        sql.Should().Contain(" in (select ");
        sql.Should().Contain("cast(t2.id as bigint) = t1.id");
    }

    [Fact]
    public void CorrelatedExistsInOrderBy_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer
            .OrderBy(it => SqlFunctions.Sql.exists(inner.Where(s => s.Id == it.Id)))
            .Select(it => new { it.Id }));

        sql.Should().Contain("order by exists(");
        sql.Should().Contain("cast(t2.id as bigint) = t1.id");
    }

    /// <summary>
    /// An aggregate terminal inside a correlated subquery is rewritten to the equivalent aggregate
    /// projection, so it renders as a scalar subquery over <c>count(*)</c> instead of being rejected.
    /// </summary>
    [Fact]
    public void AggregateCountInsideSubquery_ShouldRenderAggregateSubquery()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).Count()
        }));

        sql.Should().Be("select t1.id, (select count(*) from simple_entity as 't2'\n"
            + " where cast(t2.id as bigint) = t1.id\n"
            + "limit 1) as 'c' from complex_entity as 't1'");
    }

    [Fact]
    public void AggregateOpsInsideSubquery_ShouldRenderAggregateFunctions()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var pairs = ctx.From<IPairEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            s = pairs.Where(p => p.Id == it.Id).Sum(p => p.A),
            mn = pairs.Where(p => p.Id == it.Id).Min(p => p.A),
            mx = pairs.Where(p => p.Id == it.Id).Max(p => p.A),
            av = pairs.Where(p => p.Id == it.Id).Avg(p => p.A)
        }));

        sql.Should().Contain("(select sum(t2.a) from pair_entity as 't2'");
        sql.Should().Contain("(select min(t3.a) from pair_entity as 't3'");
        sql.Should().Contain("(select max(t4.a) from pair_entity as 't4'");
        sql.Should().Contain("(select avg(t5.a) from pair_entity as 't5'");
        sql.Should().Contain("where cast(t2.id as bigint) = t1.id");
    }

    /// <summary>
    /// A non-correlated aggregate terminal in a projection is rewritten the same way and renders as a
    /// plain scalar aggregate subquery.
    /// </summary>
    [Fact]
    public void NonCorrelatedAggregateTerminalInsideSubquery_ShouldRenderAggregateSubquery()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == 1).Count()
        }));

        sql.Should().Contain("(select count(*) from simple_entity");
        sql.Should().Contain("limit 1) as 'c'");
    }

    /// <summary>
    /// SQLite does not enforce scalar-subquery cardinality, so a numeric <c>Single</c> scalar subquery
    /// is rendered with a count guard that raises on a second row instead of returning the first.
    /// </summary>
    [Fact]
    public void SingleScalar_ShouldRenderCardinalityGuard()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            sid = inner.Where(s => s.Id == it.Id).Select(s => s.Id).Single()
        }));

        sql.Should().Contain("case when (count(*) > 1) then cast(abs(-9223372036854775808) as integer) else t2.id end");
    }

    /// <summary>
    /// A fully non-correlated subquery that itself contains a subquery must resolve each referenced
    /// command against the root registry; it used to resolve the wrong (outer) command and recurse.
    /// </summary>
    [Fact]
    public void NestedNonCorrelatedSubquery_ShouldResolveItsOwnInnerCommand()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            x = inner.Where(s => s.Id > 0)
                   .Select(s => inner.Where(s2 => s2.Id == s.Id).Select(s2 => s2.Id).First())
                   .First()
        }));

        sql.Should().Be("select t1.id, (select (select t3.id from simple_entity as 't3'\n"
            + " where t3.id = t2.id\n"
            + "limit 1) from simple_entity as 't2'\n"
            + " where (t2.id > 0)\n"
            + "limit 1) as 'x' from complex_entity as 't1'");
    }

    /// <summary>
    /// Correlation depth greater than one: the innermost subquery references both the middle
    /// subquery's parameter (<c>s.Id</c>) and the outermost one (<c>it.Id</c>). Each outer reference is
    /// resolved to the alias of the scope that registered it.
    /// </summary>
    [Fact]
    public void NestedCorrelationDepth2_ShouldReferenceBothOuterLevels()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var mid = ctx.From<ISimpleEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            x = mid.Where(s => s.Id == it.Id)
                   .Select(s => inner.Where(s2 => s2.Id == s.Id && s2.Id == it.Id).Select(s2 => s2.Id).First())
                   .First()
        }));

        sql.Should().Be("select t1.id, (select (select t3.id from simple_entity as 't3'\n"
            + " where (t3.id = t2.id and cast(t3.id as bigint) = t1.id)\n"
            + "limit 1) from simple_entity as 't2'\n"
            + " where cast(t2.id as bigint) = t1.id\n"
            + "limit 1) as 'x' from complex_entity as 't1'");
    }

    private static string SqlOfCached<T>(SqliteDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).DbCommand.CommandText);

    /// <summary>
    /// A subquery in HAVING is prepared through the correlated-query visitor like WHERE, so a
    /// correlated scalar resolves against the grouped outer query instead of emitting a raw
    /// inner-command node (which previously forced an explicit <c>NotSupportedException</c>).
    /// </summary>
    [Fact]
    public void CorrelatedSubqueryInHaving_ShouldReferenceOuterAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer
            .GroupBy(it => new { it.Id })
            .Having(g => g.Id == inner.Where(s => s.Id == g.Id).Select(s => s.Id).First())
            .Select(g => new { g.Id }));

        sql.Should().Contain(" having ");
        sql.Should().Contain("(select t2.id from simple_entity as 't2'");
        sql.Should().Contain("cast(t2.id as bigint) = t1.id");
    }

    /// <summary>
    /// A non-correlated subquery in HAVING is prepared by the same path and renders as a plain scalar
    /// subquery over the inner source.
    /// </summary>
    [Fact]
    public void SubqueryInHaving_ShouldRenderScalarSubquery()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var sql = SqlOf(ctx, outer
            .GroupBy(it => new { it.Id })
            .Having(g => g.Id == inner.Select(s => s.Id).First())
            .Select(g => new { g.Id }));

        sql.Should().Contain(" having ");
        sql.Should().Contain("(select id from simple_entity");
        sql.Should().Contain("limit 1");
    }

    [Fact]
    public void CorrelatedExpressionsCache_ShouldNotReuseMarkerIndex()
    {
        DataContextCache.ExpressionsCache.Clear();
        var ctx = SqliteTestContext.CreateSqlite();
        try
        {
            ctx.EnsureConnectionOpen();
            ctx.PurgeQueryCache();

            // The first command registers two outer references: B -> index 0, A -> index 1, so the
            // cached delegate that builds the "A" subquery bakes OuterRefMarker(1) into its body.
            var sqlZ = SqlOfCached(ctx, ctx.From<IPairEntity>().Select(it => new
            {
                it.Id,
                b = ctx.From<IPairInnerEntity>().Where(s => s.Id == it.B).Select(s => s.Id).First(),
                a = ctx.From<IPairInner2Entity>().Where(s => s.Id == it.A).Select(s => s.Id).First(),
            }));

            // A second command binds A first (index 0). Reusing the cached delegate would resolve
            // marker 1 against this command's single-entry OuterReferences and read the wrong column
            // (or throw). The marker index must never be shared across outer commands.
            var sqlW = SqlOfCached(ctx, ctx.From<IPairEntity>().Select(it => new
            {
                it.Id,
                a = ctx.From<IPairInner2Entity>().Where(s => s.Id == it.A).Select(s => s.Id).First(),
            }));

            sqlZ.Should().Contain("= t1.b");
            sqlZ.Should().Contain("= t1.a");
            sqlW.Should().Contain("= t1.a");
            sqlW.Should().NotContain("t1.b");
        }
        finally
        {
            ctx.Dispose();
        }
    }

    /// <summary>
    /// Regression test for the plan cache: two correlated commands that differ only in the outer
    /// column they reference (both <c>int</c>, so the marker type/index and the referenced-query SQL
    /// are identical) must not share a plan. Before the outer references became part of the plan key
    /// the second command reused the first one's SQL and silently read the wrong column.
    /// </summary>
    [Fact]
    public void CorrelatedPlanCache_ShouldDistinguishOuterColumns()
    {
        var ctx = SqliteTestContext.CreateSqlite();
        try
        {
            ctx.EnsureConnectionOpen();
            ctx.PurgeQueryCache();

            var sqlA = SqlOfCached(ctx, ctx.From<IPairEntity>()
                .Where(it => it.Id == ctx.From<IPairInnerEntity>().Where(s => s.Id == it.A).Select(s => s.Id).First())
                .Select(it => it.Id));
            var sqlB = SqlOfCached(ctx, ctx.From<IPairEntity>()
                .Where(it => it.Id == ctx.From<IPairInnerEntity>().Where(s => s.Id == it.B).Select(s => s.Id).First())
                .Select(it => it.Id));

            sqlA.Should().NotBe(sqlB);
            sqlA.Should().Contain("= t1.a");
            sqlB.Should().Contain("= t1.b");
        }
        finally
        {
            ctx.Dispose();
        }
    }

    /// <summary>
    /// Two correlated subqueries over the same entity type must each resolve to their own table alias.
    /// Before the source scope was introduced the second subquery's columns resolved to the first
    /// subquery's alias (for example <c>from pair_inner as 't3' where t2.id = t1.a</c>), which is
    /// invalid SQL because <c>t2</c> is not in scope there.
    /// </summary>
    [Fact]
    public void CorrelatedSiblingSubqueriesOfSameType_ShouldUseTheirOwnAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IPairEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            b = ctx.From<IPairInnerEntity>().Where(s => s.Id == it.B).Select(s => s.Id).First(),
            a = ctx.From<IPairInnerEntity>().Where(s => s.Id == it.A).Select(s => s.Id).First(),
        }));

        sql.Should().Contain("t2.id = t1.b");
        sql.Should().NotContain("t2.id = t1.a");
        sql.Should().Contain("t3.id = t1.a");
    }

    /// <summary>
    /// Same as <see cref="CorrelatedSiblingSubqueriesOfSameType_ShouldUseTheirOwnAlias"/> for the
    /// pre-existing <c>EXISTS</c> form, which was affected by the same alias-resolution defect.
    /// </summary>
    [Fact]
    public void CorrelatedSiblingExistsOfSameType_ShouldUseTheirOwnAlias()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IPairEntity>();

        var sql = SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            eb = SqlFunctions.Sql.exists(ctx.From<IPairInnerEntity>().Where(s => s.Id == it.B)),
            ea = SqlFunctions.Sql.exists(ctx.From<IPairInnerEntity>().Where(s => s.Id == it.A)),
        }));

        sql.Should().Contain("t2.id = t1.b");
        sql.Should().NotContain("t2.id = t1.a");
        sql.Should().Contain("t3.id = t1.a");
    }
}
