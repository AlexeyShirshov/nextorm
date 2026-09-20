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

    [Fact]
    public void AggregateTerminalInsideSubquery_ShouldThrowNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            c = inner.Where(s => s.Id == it.Id).Count()
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*aggregate terminal*");
    }

    [Fact]
    public void NestedCorrelationDepth2_ShouldThrowNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var mid = ctx.From<ISimpleEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, outer.Select(it => new
        {
            it.Id,
            x = mid.Where(s => s.Id == it.Id)
                   .Select(s => inner.Where(s2 => s2.Id == s.Id && s2.Id == it.Id).Select(s2 => s2.Id).First())
                   .First()
        }));

        act.Should().Throw<NotSupportedException>().WithMessage("*Nested correlated*");
    }

    private static string SqlOfCached<T>(SqliteDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, true, CancellationToken.None)).DbCommand.CommandText);

    [Fact]
    public void CorrelatedSubqueryInHaving_ShouldThrowNotSupported()
    {
        using var ctx = SqliteTestContext.Create();
        var outer = ctx.From<IComplexEntity>();
        var inner = ctx.From<ISimpleEntity>();

        var act = () => SqlOf(ctx, outer
            .GroupBy(it => new { it.Id })
            .Having(g => g.Id == inner.Where(s => s.Id == g.Id).Select(s => s.Id).First())
            .Select(g => new { g.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*HAVING*");
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
