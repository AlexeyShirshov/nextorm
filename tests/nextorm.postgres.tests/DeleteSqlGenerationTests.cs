using FluentAssertions;
using NextORM.Core;
using System.Data.Common;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of the delete builder on PostgreSQL (no database connection): the predicate form
/// renders <c>DELETE FROM ... WHERE ...</c> and <c>All()</c> renders an unfiltered delete.
/// </summary>
public class DeleteSqlGenerationTests
{
    [Fact]
    public void Delete_WhereLiteral_ShouldRenderPredicate()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1");
    }

    [Fact]
    public void Delete_WhereCapturedValue_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var id = 5L;

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == id)
            .ToSql()
            .Should().Be("delete from merge_entity where id = @id");
    }

    [Fact]
    public void Delete_All_ShouldRenderNoWhere()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .ToSql()
            .Should().Be("delete from merge_entity");
    }

    [Fact]
    public void Delete_QuotedIdentifiers_ShouldQuoteTableAndColumns()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .ToSql()
            .Should().Be("delete from \"merge_entity\" where \"id\" = 1");
    }

    [Fact]
    public void Delete_WithoutFilter_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.DeleteFrom<IMergeEntity>().ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_ReturningEntity_ShouldRenderReturning()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning()
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1 returning id, name, age, total");
    }

    [Fact]
    public void Delete_ReturningProjection_ShouldRenderSelectedColumns()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .Where(x => x.Id == 1)
            .Returning(x => new { x.Id, x.Name })
            .ToSql()
            .Should().Be("delete from merge_entity where id = 1 returning id, name");
    }

    [Fact]
    public void Delete_AllReturning_ShouldRenderReturningWithoutWhere()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.DeleteFrom<IMergeEntity>()
            .All()
            .Returning(x => x.Id)
            .ToSql()
            .Should().Be("delete from merge_entity returning id");
    }

    [Fact]
    public void Truncate_ShouldRenderTruncateTable()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.Truncate<IMergeEntity>().ToSql().Should().Be("truncate table merge_entity");
    }

    [Fact]
    public void Truncate_QuotedIdentifiers_ShouldQuoteTable()
    {
        using var ctx = PostgresTestContext.CreateQuoted();

        ctx.Truncate<IMergeEntity>().ToSql().Should().Be("truncate table \"merge_entity\"");
    }

    [Fact]
    public void DeleteJoin_Where_ShouldRenderUsing()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Where(p => p.Item1.String == "x")
            .ToSql()
            .Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint) and t1.somestring = 'x'");
    }

    [Fact]
    public void DeleteJoin_NoFilter_ShouldRenderUsingWithoutWhere()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .ToSql()
            .Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint)");
    }
    [Fact]
    public void DeleteJoin_CapturedValue_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var text = "x";

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Where(p => p.Item1.String == text)
            .ToSql()
            .Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint) and t1.somestring = @text");
    }

    [Fact]
    public void DeleteJoin_FromCte_ShouldHoistWith()
    {
        using var ctx = PostgresTestContext.Create();

        var e = ctx.From<IComplexEntity>();
        var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));

        var sql = e
            .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
            .ToSql();

        sql.Should().StartWith("with c as (select id from complex_entity");
        sql.Should().Contain("delete from complex_entity as \"t1\" using c as \"t2\" where t1.id = cast(t2.id as bigint)");
    }

    [Fact]
    public void DeleteJoin_FromNestedCte_ShouldHoistOneFlatWith()
    {
        using var ctx = PostgresTestContext.Create();

        // The joined side is a CTE whose body itself carries a nested declaration; the multi-table
        // DELETE has to hoist the whole tree (QueryPlanner.RenderDeleteJoin at line 324).
        var nested = ctx.With("i", ctx.From<IComplexEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });
        var scope = ctx.With("o", nested);

        var sql = ctx.From<IComplexEntity>()
            .Join(scope.From("o"), (t, c) => t.Id == c["id"].AsInt)
            .ToSql();

        sql.Should().StartWith("with i as (select id from complex_entity");
        sql.Should().Contain("), o as (select id from i)");
        sql.Should().Contain("delete from complex_entity as \"t1\" using o as \"t2\" where t1.id = cast(t2.id as bigint)");
        sql.Should().NotContain("with i as (with");
    }

    [Fact]
    public void DeleteJoin_ThreeTables_ShouldChainJoins()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Join(ctx.From<IArrayEntity>(), (p, a) => p.Item2.Id == a.Id)
            .ToSql();
        sql.Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\", array_entity as \"t3\" where t1.id = cast(t2.id as bigint) and t2.id = t3.id");
    }

    [Fact]
    public void DataModifyingCte_DeleteReturningBody_ShouldRenderDeleteReturningInsideCte()
    {
        using var ctx = PostgresTestContext.Create();

        SqlOf(ctx, ctx.With("del", ctx.DeleteFrom<IMergeEntity>()
                .Where(x => x.Id == 1)
                .Returning(x => new { x.Id, x.Name }))
            .From("del")
            .Select(r => new { r.Id, r.Name }))
            .Should().Be("with del as (delete from merge_entity where id = 1 returning id, name) select id, name from del as \"t1\"");
    }

    [Fact]
    public void DataModifyingCte_DeleteJoinReturningBody_ShouldRenderDeleteUsingReturningInsideCte()
    {
        using var ctx = PostgresTestContext.Create();

        var delete = ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .CreateDeleteJoinBuilder()
            .Returning(p => new { TargetId = p.Item1.Id, JoinedId = p.Item2.Id });

        var sql = SqlOf(ctx, ctx.With("del", delete).From("del").Select(r => new { r.TargetId, r.JoinedId }));

        sql.Should().StartWith("with del as (delete from complex_entity as \"t1\" using simple_entity as \"t2\"");
        sql.Should().Contain("returning t1.id as \"TargetId\", t2.id as \"JoinedId\"");
        sql.Split("with ").Should().HaveCount(2);
    }

    [Fact]
    public void DataModifyingCte_DeleteJoinBodyReferencingReadCte_ShouldHoistReadCteFirst()
    {
        using var ctx = PostgresTestContext.Create();

        // The data-modifying DELETE ... USING body joins a read CTE declared through the scope; the
        // hoister must order the read CTE before the mutation that consumes it.
        var scope = ctx.With("src", ctx.From<ISimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }));

        var delete = ctx.From<IComplexEntity>()
            .Join(scope.From("src"), (c, s) => c.Id == s["id"].AsInt)
            .CreateDeleteJoinBuilder()
            .Returning(p => new { TargetId = p.Item1.Id });

        var sql = SqlOf(ctx, ctx.With("del", delete).From("del").Select(r => new { r.TargetId }));

        sql.Should().StartWith("with src as (select id from simple_entity");
        sql.Should().Contain("), del as (delete from complex_entity as \"t1\" using src as \"t2\"");
        sql.Should().NotContain("with src as (with");
    }

    [Fact]
    public void DataModifyingCte_DeleteJoinUnnamedReturningMember_ShouldReadBackUnderPhysicalColumnName()
    {
        using var ctx = PostgresTestContext.Create();

        // An anonymous RETURNING member keeps the source member's name (p.Item1.Id -> "Id"), whose
        // mapped column is "id". The CTE read shape resolves it under the physical column name, so the
        // body must not alias it to the CLR member name or the outer read references a missing "Id".
        var scope = ctx.With("del", ctx.From<IComplexEntity>()
                .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
                .CreateDeleteJoinBuilder()
                .Returning(p => new { p.Item1.Id }))
            .From("del")
            .Select(r => new { r.Id });

        SqlOf(ctx, scope).Should().Be(
            "with del as (delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint) returning t1.id) select id from del as \"t1\"");
    }

    [Fact]
    public void DeleteJoinReturning_DirectRoute_ToSql_ShouldRenderUsingReturningWithoutCte()
    {
        using var ctx = PostgresTestContext.Create();

        // The standalone terminal (no With(...) scope) renders DELETE ... USING ... RETURNING; the
        // DataContext.BuildReturningSql DeleteJoinCommand arm feeds the same renderer.
        var sql = ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .CreateDeleteJoinBuilder()
            .Returning(p => new { TargetId = p.Item1.Id, JoinedId = p.Item2.Id })
            .ToSql();

        sql.Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint) returning t1.id as \"TargetId\", t2.id as \"JoinedId\"");
        sql.Should().NotContain("with ");
    }

    [Fact]
    public void DeleteJoinReturning_Scalar_ToSql_ShouldRenderSingleColumn()
    {
        using var ctx = PostgresTestContext.Create();

        var sql = ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .CreateDeleteJoinBuilder()
            .Returning(p => p.Item1.Id)
            .ToSql();

        sql.Should().Be("delete from complex_entity as \"t1\" using simple_entity as \"t2\" where t1.id = cast(t2.id as bigint) returning t1.id");
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);
}
