using FluentAssertions;
using NextORM.Core;

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
            .UpdateJoin()
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
            .UpdateJoin()
            .Set(p => p.Item1.Id, 0)
            .ToSql();

        sql.Should().StartWith("with c as (select id from simple_entity");
        sql.Should().Contain("update simple_entity as \"t1\" set id = @p0 from c as \"t2\" where t1.id = t2.id");
    }

    [Fact]
    public void UpdateJoin_SetJoinedColumn_ShouldQualifyBothSides()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
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
            .UpdateJoin()
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
            .UpdateJoin()
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
            .UpdateJoin()
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
            .UpdateJoin()
            .Set(p => p.Item1.String, "x")
            .ToSql()
            .Should().Be("update complex_entity as \"t1\" set somestring = @p0 from simple_entity as \"t2\", array_entity as \"t3\" where t1.id = cast(t2.id as bigint) and t2.id = t3.id");
    }

    [Fact]
    public void UpdateJoin_RepeatedSet_ShouldReplaceEarlierAssignment()
    {
        using var ctx = PostgresTestContext.Create();

        ctx.From<IMergeEntity>()
            .Join(ctx.From<IMergeEntity>(), (a, b) => a.Id == b.Id)
            .UpdateJoin()
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
            .UpdateJoin()
            .Set(p => p.Item1.Age, p => p.Item2.Age + increment)
            .ToSql()
            .Should().Be("update merge_entity as \"t1\" set age = (t2.age + @increment) from merge_entity as \"t2\" where t1.id = t2.id");
    }
}
