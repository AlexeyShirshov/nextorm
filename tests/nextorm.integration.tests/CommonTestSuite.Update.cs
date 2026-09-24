using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static int UpdateKey() => Random.Shared.Next(1_000_000, int.MaxValue);

    [Fact]
    public void Update_ByPredicate_ShouldChangeMatchingRows()
    {
        var ctx = _sut.DataProvider;
        var keep = UpdateKey();
        var change = keep + 1;

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = keep, Name = "keep", Age = 1 },
            new DeleteEntity { Id = change, Name = "change", Age = 2 },
        ]).Insert();

        var affected = ctx.Update<IDeleteEntity>()
            .Set(x => x.Name, "updated")
            .Where(x => x.Id == change)
            .Update();

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == keep).Select(x => x.Name).Single().Should().Be("keep");
        ctx.From<IDeleteEntity>().Where(x => x.Id == change).Select(x => x.Name).Single().Should().Be("updated");
    }

    [Fact]
    public void Update_Expression_ShouldIncrementColumn()
    {
        var ctx = _sut.DataProvider;
        var id = UpdateKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "inc", Age = 10 })
            .Insert();

        ctx.Update<IDeleteEntity>()
            .Set(x => x.Age, x => x.Age + 1)
            .Where(x => x.Id == id)
            .Update();

        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Age).Single().Should().Be(11);
    }

    [Fact]
    public void Update_ByEntity_ShouldChangeByKey()
    {
        var ctx = _sut.DataProvider;
        var id = UpdateKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "before", Age = 3 })
            .Insert();

        var affected = ctx.Update(new DeleteEntity { Id = id, Name = "after", Age = 9 });

        affected.Should().Be(1);
        var row = ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => new { x.Name, x.Age }).Single();
        row.Name.Should().Be("after");
        row.Age.Should().Be(9);
    }

    [Fact]
    public void Update_NoMatch_ShouldReturnZero()
    {
        var ctx = _sut.DataProvider;

        ctx.Update<IDeleteEntity>()
            .Set(x => x.Name, "nobody")
            .Where(x => x.Id == UpdateKey())
            .Update()
            .Should().Be(0);
    }

    [Fact]
    public async Task UpdateAsync_ByEntity_ShouldChangeByKey()
    {
        var ctx = _sut.DataProvider;
        var id = UpdateKey();

        await ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "async", Age = 1 })
            .InsertAsync(TestContext.Current.CancellationToken);

        var affected = await ctx.UpdateAsync(
            new DeleteEntity { Id = id, Name = "async-updated", Age = 2 },
            TestContext.Current.CancellationToken);

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Name).Single().Should().Be("async-updated");
    }

    [Fact]
    public void Update_Returning_ShouldReturnUpdatedRows()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return updated rows.");
        var ctx = _sut.DataProvider;
        var id = UpdateKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "before", Age = 1 })
            .Insert();

        var updated = ctx.Update<IDeleteEntity>()
            .Set(x => x.Name, "after")
            .Where(x => x.Id == id)
            .Returning(x => new { x.Id, x.Name })
            .ToList();

        updated.Should().ContainSingle();
        updated[0].Id.Should().Be(id);
        updated[0].Name.Should().Be("after");
    }

    [Fact]
    public async Task Update_ReturningAsync_ShouldReturnUpdatedRows()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return updated rows.");
        var ctx = _sut.DataProvider;
        var id = UpdateKey();

        await ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "ret2", Age = 2 })
            .InsertAsync(TestContext.Current.CancellationToken);

        var updated = await ctx.Update<IDeleteEntity>()
            .Set(x => x.Name, "ret2-updated")
            .Where(x => x.Id == id)
            .Returning(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        updated.Should().ContainSingle();
        updated[0].Should().Be("ret2-updated");
    }

    [Fact]
    public void UpdateJoin_ShouldChangeTargetFromJoinedRow()
    {
        var ctx = _sut.DataProvider;
        var source = UpdateKey();
        var target = source + 1;

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = source, Name = "src", Age = 1 },
            new DeleteEntity { Id = target, Name = "tgt", Age = 1 },
        ]).Insert();

        var affected = ctx.From<IDeleteEntity>()
            .Join(ctx.From<IDeleteEntity>(), (t, s) => t.Id == target && s.Id == source)
            .UpdateJoin()
            .Set(p => p.Item1.Name, p => p.Item2.Name)
            .Update();

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == target).Select(x => x.Name).Single().Should().Be("src");
    }

    [Fact]
    public void UpdateJoin_FromCte_ShouldChangeTargetFromCte()
    {
        var ctx = _sut.DataProvider;
        var key = UpdateKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = key, Name = "before", Age = 1 })
            .Insert();

        var e = ctx.From<IDeleteEntity>();
        var scope = ctx.With("c", e.Where(x => x.Id == key).Select(x => new { x.Id }));

        var affected = e
            .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
            .UpdateJoin()
            .Set(p => p.Item1.Name, "after")
            .Update();

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == key).Select(x => x.Name).Single().Should().Be("after");
    }

    [Fact]
    public async Task UpdateJoinAsync_ShouldChangeTargetFromJoinedRow()
    {
        var ctx = _sut.DataProvider;
        var source = UpdateKey();
        var target = source + 1;

        await ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = source, Name = "asrc", Age = 1 },
            new DeleteEntity { Id = target, Name = "atgt", Age = 1 },
        ]).InsertAsync(TestContext.Current.CancellationToken);

        var affected = await ctx.From<IDeleteEntity>()
            .Join(ctx.From<IDeleteEntity>(), (t, s) => t.Id == target && s.Id == source)
            .UpdateJoin()
            .Set(p => p.Item1.Name, p => p.Item2.Name)
            .UpdateAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == target).Select(x => x.Name).Single().Should().Be("asrc");
    }

    [Fact]
    public void Update_ToSql_ShouldRenderUpdate()
    {
        var sql = _sut.DataProvider
            .Update<IDeleteEntity>()
            .Set(x => x.Name, "x")
            .Where(x => x.Id == 1)
            .ToSql();

        sql.Should().ContainEquivalentOf("update");
    }

    [Fact]
    public async Task UpdateAsync_Builder_ShouldChangeRow()
    {
        var ctx = _sut.DataProvider;
        var id = UpdateKey();

        await ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "before", Age = 1 })
            .InsertAsync(TestContext.Current.CancellationToken);

        var affected = await ctx.Update<IDeleteEntity>()
            .Set(x => x.Name, "after")
            .Where(x => x.Id == id)
            .UpdateAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Name).Single().Should().Be("after");
    }

    [Fact]
    public void Update_ReturningWholeEntity_ShouldReturnRow()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return updated rows.");

        var ctx = _sut.DataProvider;
        var id = UpdateKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "before", Age = 1 })
            .Insert();

        var rows = ctx.Update<DeleteEntity>()
            .Set(x => x.Name, "after")
            .Where(x => x.Id == id)
            .Returning()
            .ToList();

        rows.Should().ContainSingle();
        rows[0].Name.Should().Be("after");
        rows[0].Id.Should().Be(id);
    }
}
