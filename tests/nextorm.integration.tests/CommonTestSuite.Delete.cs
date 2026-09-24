using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

public abstract partial class CommonTestSuite
{
    private static int DeleteKey() => Random.Shared.Next(1_000_000, int.MaxValue);

    [Fact]
    public void Delete_ByPredicate_ShouldRemoveMatchingRows()
    {
        var ctx = _sut.DataProvider;
        var keep = DeleteKey();
        var remove = keep + 1;

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = keep, Name = "keep", Age = 1 },
            new DeleteEntity { Id = remove, Name = "remove", Age = 2 },
        ]).Insert();

        var affected = ctx.DeleteFrom<IDeleteEntity>().Where(x => x.Id == remove).Delete();

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == keep).Select(x => x.Id).ToList().Should().ContainSingle();
        ctx.From<IDeleteEntity>().Where(x => x.Id == remove).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void Delete_ByEntity_ShouldRemoveByKey()
    {
        var ctx = _sut.DataProvider;
        var id = DeleteKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "e", Age = 3 })
            .Insert();

        var affected = ctx.Delete(new DeleteEntity { Id = id });

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void Delete_NoMatch_ShouldReturnZero()
    {
        var ctx = _sut.DataProvider;

        ctx.DeleteFrom<IDeleteEntity>().Where(x => x.Id == DeleteKey()).Delete().Should().Be(0);
    }

    [Fact]
    public void Delete_SecondCall_ShouldReturnZero()
    {
        var ctx = _sut.DataProvider;
        var id = DeleteKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "once", Age = 4 })
            .Insert();

        ctx.Delete(new DeleteEntity { Id = id }).Should().Be(1);
        ctx.Delete(new DeleteEntity { Id = id }).Should().Be(0);
    }

    [Fact]
    public void Delete_All_ShouldRemoveEveryRow()
    {
        var ctx = _sut.DataProvider;

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = DeleteKey(), Name = "a", Age = 1 },
            new DeleteEntity { Id = DeleteKey(), Name = "b", Age = 2 },
        ]).Insert();

        ctx.DeleteFrom<IDeleteEntity>().All().Delete();

        ctx.From<IDeleteEntity>().Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void Delete_Returning_ShouldReturnRemovedRows()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return removed rows.");
        var ctx = _sut.DataProvider;
        var id = DeleteKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "ret", Age = 7 })
            .Insert();

        var removed = ctx.DeleteFrom<IDeleteEntity>()
            .Where(x => x.Id == id)
            .Returning(x => new { x.Id, x.Name })
            .ToList();

        removed.Should().ContainSingle();
        removed[0].Id.Should().Be(id);
        removed[0].Name.Should().Be("ret");
    }

    [Fact]
    public async Task Delete_ReturningAsync_ShouldReturnRemovedRows()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return removed rows.");
        var ctx = _sut.DataProvider;
        var id = DeleteKey();

        await ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "ret2", Age = 8 })
            .InsertAsync(TestContext.Current.CancellationToken);

        var removed = await ctx.DeleteFrom<IDeleteEntity>()
            .Where(x => x.Id == id)
            .Returning(x => x.Name)
            .ToListAsync(TestContext.Current.CancellationToken);

        removed.Should().ContainSingle();
        removed[0].Should().Be("ret2");
    }

    [Fact]
    public void Truncate_ShouldRemoveEveryRow()
    {
        Assert.SkipUnless(Provider.SupportsTruncate, "This provider has no TRUNCATE.");
        var ctx = _sut.DataProvider;

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = DeleteKey(), Name = "t1", Age = 1 },
            new DeleteEntity { Id = DeleteKey(), Name = "t2", Age = 2 },
        ]).Insert();

        ctx.Truncate<IDeleteEntity>().Execute();

        ctx.From<IDeleteEntity>().Select(x => x.Id).ToList().Should().BeEmpty();
    }

    [Fact]
    public void DeleteJoin_ShouldRemoveRowsMatchingAnotherTable()
    {
        Assert.SkipUnless(Provider.SupportsDeleteJoin, "This provider has no native multi-table DELETE.");
        var ctx = _sut.DataProvider;
        var marker = "delete-join-" + Guid.NewGuid().ToString("N");
        var remove = DeleteKey();
        var keep = remove + 1;

        ctx.InsertInto<IMergeEntity>()
            .Values(new MergeEntity { Id = remove, Name = marker, Age = 1 })
            .Insert();

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = remove, Name = "remove", Age = 1 },
            new DeleteEntity { Id = keep, Name = "keep", Age = 2 },
        ]).Insert();

        var affected = ctx.From<IDeleteEntity>()
            .Join(ctx.From<IMergeEntity>(), (d, m) => d.Id == m.Id)
            .Where(p => p.Item2.Name == marker)
            .Delete();

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == remove).Select(x => x.Id).ToList().Should().BeEmpty();
        ctx.From<IDeleteEntity>().Where(x => x.Id == keep).Select(x => x.Id).ToList().Should().ContainSingle();
    }

    [Fact]
    public void DeleteJoin_FromCte_ShouldRemoveRowsFromCte()
    {
        Assert.SkipUnless(Provider.SupportsDeleteJoin, "This provider has no native multi-table DELETE.");
        var ctx = _sut.DataProvider;
        var remove = DeleteKey();
        var keep = remove + 1;

        ctx.InsertInto<IDeleteEntity>().Values([
            new DeleteEntity { Id = remove, Name = "remove", Age = 1 },
            new DeleteEntity { Id = keep, Name = "keep", Age = 2 },
        ]).Insert();

        var e = ctx.From<IDeleteEntity>();
        var scope = ctx.With("c", e.Where(x => x.Id == remove).Select(x => new { x.Id }));

        var affected = e
            .Join(scope.From("c"), (t, c) => t.Id == c["id"].AsInt)
            .Delete();

        affected.Should().Be(1);
        ctx.From<IDeleteEntity>().Where(x => x.Id == remove).Select(x => x.Id).ToList().Should().BeEmpty();
        ctx.From<IDeleteEntity>().Where(x => x.Id == keep).Select(x => x.Id).ToList().Should().ContainSingle();
    }

    [Fact]
    public void Delete_ToSql_ShouldRenderDelete()
    {
        var sql = _sut.DataProvider.DeleteFrom<IDeleteEntity>().Where(x => x.Id == 1).ToSql();

        sql.Should().ContainEquivalentOf("delete");
    }

    [Fact]
    public async Task DeleteAsync_Builder_ShouldRemoveRow()
    {
        var ctx = _sut.DataProvider;
        var id = DeleteKey();

        await ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "async", Age = 1 })
            .InsertAsync(TestContext.Current.CancellationToken);

        var affected = await ctx.DeleteFrom<IDeleteEntity>()
            .Where(x => x.Id == id)
            .DeleteAsync(TestContext.Current.CancellationToken);

        affected.Should().Be(1);
    }

    [Fact]
    public void Delete_ReturningWholeEntity_ShouldReturnRow()
    {
        Assert.SkipUnless(Provider.SupportsInsertReturning, "This provider cannot return removed rows.");

        var ctx = _sut.DataProvider;
        var id = DeleteKey();

        ctx.InsertInto<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "whole", Age = 5 })
            .Insert();

        var rows = ctx.DeleteFrom<DeleteEntity>().Where(x => x.Id == id).Returning().ToList();

        rows.Should().ContainSingle();
        rows[0].Id.Should().Be(id);
        rows[0].Name.Should().Be("whole");
    }

    [Fact]
    public void Delete_WithoutFilter_ShouldThrow()
    {
        var act = () => _sut.DataProvider.DeleteFrom<IDeleteEntity>().Delete();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_AllThenWhere_ShouldThrow()
    {
        var act = () => _sut.DataProvider.DeleteFrom<IDeleteEntity>().All().Where(x => x.Id == 1).Delete();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Delete_WhereThenAll_ShouldThrow()
    {
        var act = () => _sut.DataProvider.DeleteFrom<IDeleteEntity>().Where(x => x.Id == 1).All().Delete();

        act.Should().Throw<InvalidOperationException>();
    }
}
