using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

public sealed class T405JoinBatchCaptureTests
{
    [Fact]
    public void Update_CapturesTargetColumnsBeforeFrom_AndReadsInSameBatch()
    {
        using var ctx = SqlServerTestContext.Create();
        var mutation = ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .CreateUpdateJoinBuilder()
            .Set(p => p.Item1.Id, p => p.Item2.Id)
            .Where(p => p.Item1.Id == 1)
            .Returning(p => new { p.Item1.Id });
        var batch = ctx.CreateBatchBuilder().OutputIntoTableVariable(mutation, "@t", "id int");

        batch.Query(batch.FromOutputTable("@t").Select(t => t["id"].AsInt)).ToSql()
            .Should().Be("declare @t table (id int); update [t1] set id = t2.id output inserted.id into @t (id)"
                + " from simple_entity as [t1] join simple_entity as [t2] on t1.id = t2.id where t1.id = 1; select id\n from @t");
    }

    [Fact]
    public void Delete_CapturesTargetColumnsBeforeFrom_AndReadsInSameBatch()
    {
        using var ctx = SqlServerTestContext.Create();
        var mutation = ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .Where(p => p.Item2.Id == 2)
            .CreateDeleteJoinBuilder()
            .Returning(p => new { p.Item1.Id });
        var batch = ctx.CreateBatchBuilder().OutputIntoTableVariable(mutation, "@t", "id int");

        batch.Query(batch.FromOutputTable("@t").Select(t => t["id"].AsInt)).ToSql()
            .Should().Be("declare @t table (id int); delete [t1] output deleted.id into @t (id)"
                + " from simple_entity as [t1] join simple_entity as [t2] on t1.id = t2.id where t2.id = 2; select id\n from @t");
    }

    [Fact]
    public void Delete_RejectsAProjectionFromTheJoinedSourceAtRegistration()
    {
        using var ctx = SqlServerTestContext.Create();
        var mutation = ctx.From<ISimpleEntity>()
            .Join(ctx.From<ISimpleEntity>(), (a, b) => a.Id == b.Id)
            .CreateDeleteJoinBuilder()
            .Returning(p => new { p.Item2.Id });

        FluentActions.Invoking(() => ctx.CreateBatchBuilder().OutputIntoTableVariable(mutation, "@t", "id int"))
            .Should().Throw<NotSupportedException>().WithMessage("*explicit projection of mapped target (Item1) columns*");
    }
}
