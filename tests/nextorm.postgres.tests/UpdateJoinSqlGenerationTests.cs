using FluentAssertions;
using NextORM.Core;
using System.Data.Common;

namespace NextORM.Postgres.Tests;

/// <summary>
/// SQL generation of the multi-table <c>UPDATE ... FROM</c> builder on PostgreSQL: the target stays out
/// of the <c>FROM</c> list, the joined tables are listed there and the join conditions are folded into
/// the <c>WHERE</c>.
/// </summary>
public class UpdateJoinSqlGenerationTests
{
    [Fact]
    public void UpdateJoin_SetConstant_ShouldRenderFromAndFoldJoin()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .ToSql()
            .Should().Be("update merge_entity as \"t1\" set name = @p0 from merge_entity as \"t2\" where t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_FromCte_ShouldHoistWith()
    {
        using var ctx = PostgresTestContext.Create();

        var e = ctx.From<ISimpleEntity>();
        var scope = ctx.With("c", e.Where(x => x.Id > 0).Select(x => new { x.Id }));

        var sql = e
            .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().StartWith("with c as (select id from simple_entity");
        sql.Should().Contain("update simple_entity as \"t1\" set id = @p0 from c as \"t2\" where t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_FromNestedCte_ShouldHoistOneFlatWith()
    {
        using var ctx = PostgresTestContext.Create();

        // The joined side is a CTE whose body itself carries a nested declaration; the multi-table
        // UPDATE has to hoist the whole tree (QueryPlanner.RenderUpdateJoin at line 362).
        var nested = ctx.With("i", ctx.From<ISimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }))
            .From("i")
            .Select(t => new { id = t["id"].AsInt });
        var scope = ctx.With("o", nested);

        var sql = ctx.From<ISimpleEntity>()
            .Join(scope.From("o"), (t, c) => t.Id == c["id"].AsInt)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().StartWith("with i as (select id from simple_entity");
        sql.Should().Contain("), o as (select id from i)");
        sql.Should().Contain("update simple_entity as \"t1\" set id = @p0 from o as \"t2\" where t1.id = t2.id");
        sql.Should().NotContain("with i as (with");
    }

    [Fact]
    public void UpdateJoin_SetJoinedColumn_ShouldQualifyBothSides()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, p => p.Item2.Id)
            .Where(p => p.Item1.Id == 1)
            .ToSql()
            .Should().Be("update simple_entity as \"t1\" set id = t2.id from simple_entity as \"t2\" where t1.id = t2.id and t1.id = 1");
    }

    [Fact]
    public void UpdateJoin_Expression_ShouldRenderArithmetic()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Age, p => p.Item1.Age + 1)
            .ToSql()
            .Should().Be("update merge_entity as \"t1\" set age = (t1.age + 1) from merge_entity as \"t2\" where t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_LeftJoin_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.From<IMergeEntity>()
            .LeftJoin(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .ToSql();

        act.Should().Throw<NotSupportedException>().WithMessage("*only supports INNER joins*");
    }

    [Fact]
    public void UpdateJoin_WithoutAssignment_ShouldThrow()
    {
        using var ctx = PostgresTestContext.Create();

        var act = () => ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .ToSql();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateJoin_ThreeTables_ShouldChainJoins()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Join(ctx.From<IArrayEntity>(), (p, a) => p.Item2.Id == a.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.String, "x")
            .ToSql()
            .Should().Be("update complex_entity as \"t1\" set somestring = @p0 from simple_entity as \"t2\", array_entity as \"t3\" where t1.id = cast(t2.id as bigint) and t2.id = t3.id");
    }

    [Fact]
    public void UpdateJoin_TwoJoinsToSameType_ShouldBindSecondConditionToSecondAlias()
    {
        using var ctx = PostgresTestContext.Create();

        // Two joins to the same entity type: the second join's right-hand parameter must bind to the
        // second source (t3), not the earlier sibling (t2). The join targets bind positionally via
        // MemberTranslator.AliasProvider.FindAlias, so the ON condition reads "t2.id = t3.id".
        ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .Join(ctx.From<ISimpleEntity>(), (p, s) => p.Item2.Id == s.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.String, "x")
            .ToSql()
            .Should().Be("update complex_entity as \"t1\" set somestring = @p0 from simple_entity as \"t2\", simple_entity as \"t3\" where t1.id = cast(t2.id as bigint) and t2.id = t3.id");
    }

    [Fact]
    public void UpdateJoin_RepeatedSet_ShouldReplaceEarlierAssignment()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "first")
            .Set(p => p.Item1.Name, "second")
            .ToSql()
            .Should().Be("update merge_entity as \"t1\" set name = @p0 from merge_entity as \"t2\" where t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_ExpressionWithCapturedValue_ShouldParameterise()
    {
        using var ctx = PostgresTestContext.Create();
        var increment = 2;

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Age, p => p.Item2.Age + increment)
            .ToSql()
            .Should().Be("update merge_entity as \"t1\" set age = (t2.age + @increment) from merge_entity as \"t2\" where t1.id = t2.id");
    }

    [Fact]
    public void DataModifyingCte_UpdateJoinReturningBody_ShouldRenderUpdateFromReturningInsideCte()
    {
        using var ctx = PostgresTestContext.Create();

        var update = ctx.From<IComplexEntity>()
            .Join(ctx.From<ISimpleEntity>(), (c, s) => c.Id == s.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.String, "x")
            .Returning(p => new { TargetId = p.Item1.Id, JoinedId = p.Item2.Id });

        var sql = SqlOf(ctx, ctx.With("upd", update).From("upd").Select(r => new { r.TargetId, r.JoinedId }));

        sql.Should().StartWith("with upd as (update complex_entity as \"t1\" set somestring = @p0 from simple_entity as \"t2\"");
        sql.Should().Contain("returning t1.id as \"TargetId\", t2.id as \"JoinedId\"");
        sql.Split("with ").Should().HaveCount(2);
    }

    [Fact]
    public void DataModifyingCte_UpdateJoinBodyReferencingReadCte_ShouldHoistReadCteFirst()
    {
        using var ctx = PostgresTestContext.Create();

        // The data-modifying UPDATE ... FROM body joins a read CTE declared through the scope; the
        // hoister must order the read CTE before the mutation that consumes it.
        var scope = ctx.With("src", ctx.From<ISimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id }));

        var update = ctx.From<IComplexEntity>()
            .Join(scope.From("src"), (c, s) => c.Id == s["id"].AsInt)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.String, "x")
            .Returning(p => new { TargetId = p.Item1.Id });

        var sql = SqlOf(ctx, ctx.With("upd", update).From("upd").Select(r => new { r.TargetId }));

        sql.Should().StartWith("with src as (select id from simple_entity");
        sql.Should().Contain("), upd as (update complex_entity as \"t1\" set somestring = @p0 from src as \"t2\"");
        sql.Should().NotContain("with src as (with");
    }

    [Fact]
    public void DataModifyingCte_UpdateJoinReturningReferencingDerivedShapeSlot_ShouldResolveShapeSlot()
    {
        using var ctx = PostgresTestContext.Create();

        // The joined slot is a derived-table projection (a shape) with no registered entity metadata;
        // the selector reads its shape member, which must resolve against the shape columns rather than
        // fail as an unmapped entity.
        var source = ctx.From<ISimpleEntity>().Where(x => x.Id > 0).Select(x => new { x.Id });

        var act = () => ctx.From<IComplexEntity>()
            .Join(source, (c, s) => c.Id == s.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.String, "x")
            .Returning(p => new { TargetId = p.Item1.Id, SourceId = p.Item2.Id });

        act.Should().NotThrow();
    }

    [Fact]
    public void DataModifyingCte_UpdateJoinUnnamedReturningMember_ShouldReadBackUnderPhysicalColumnName()
    {
        using var ctx = PostgresTestContext.Create();

        // An anonymous RETURNING member keeps the source member's name (p.Item1.Id -> "Id"), whose
        // mapped column is "id". The CTE read shape resolves it under the physical column name, so the
        // body must not alias it to the CLR member name or the outer read references a missing "Id".
        var scope = ctx.With("upd", ctx.From<IMergeEntity>()
                .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
                .CreateUpdateJoinBuilder()
                .Set(p => p.Item1.Name, p => p.Item2.Name)
                .Returning(p => new { p.Item1.Id }))
            .From("upd")
            .Select(r => new { r.Id });

        SqlOf(ctx, scope).Should().Be(
            "with upd as (update merge_entity as \"t1\" set name = t2.name from merge_entity as \"t2\" where t1.id = t2.id returning t1.id) select id from upd as \"t1\"");
    }

    [Fact]
    public void UpdateJoinReturning_DirectRoute_ToSql_ShouldRenderReturningWithoutCte()
    {
        using var ctx = PostgresTestContext.Create();

        // The standalone terminal (no With(...) scope) renders a plain UPDATE ... FROM ... RETURNING;
        // BuildReturningSql routes the UpdateJoinCommand arm to the same renderer as the CTE body.
        var sql = ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .Returning(p => new { TargetId = p.Item1.Id, JoinedId = p.Item2.Id })
            .ToSql();

        sql.Should().Be("update merge_entity as \"t1\" set name = @p0 from merge_entity as \"t2\" where t1.id = t2.id returning t1.id as \"TargetId\", t2.id as \"JoinedId\"");
        sql.Should().NotContain("with ");
    }

    [Fact]
    public void UpdateJoinReturning_Scalar_ToSql_ShouldRenderSingleColumn()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Name, "x")
            .Returning(p => p.Item1.Id)
            .ToSql()
            .Should().Be("update merge_entity as \"t1\" set name = @p0 from merge_entity as \"t2\" where t1.id = t2.id returning t1.id");
    }

    private static string Normalize(string sql) => sql.Replace("\r\n", "\n");

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => Normalize(((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText);
}
