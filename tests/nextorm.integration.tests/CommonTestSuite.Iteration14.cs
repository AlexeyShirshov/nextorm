using FluentAssertions;
using NextORM.Core;

namespace NextORM.Integration.Tests;

/// <summary>
/// Iteration 14 proposal 2 (CTE lookup allocation): the data-modifying CTE detection must stay correct
/// after the warm lookup stopped allocating a visited set. A prepared DML CTE may never be served from
/// the plan cache (it would not re-execute), a DML CTE sitting beside a recursive read CTE must still
/// fire on every execution, and a read-only CTE reuse must not disable later plan caching.
/// </summary>
public abstract partial class CommonTestSuite
{
    private const string DataModifyingCteSkipReason =
        "Data-modifying CTE bodies are only supported by PostgreSQL.";

    [Fact]
    public void Iteration14_PreparedDml_ExecutesEveryTime()
    {
        Assert.SkipUnless(((DataContext)_sut.DataProvider).Dialect.SupportsDataModifyingCtes, DataModifyingCteSkipReason);

        var ctx = _sut.DataProvider;
        var id = UpdateKey();
        ctx.CreateInsertBuilder<IDeleteEntity>()
            .Values(new DeleteEntity { Id = id, Name = "iter14", Age = 10 })
            .Insert();

        // The command is built once and executed three times: a data-modifying CTE must never be
        // served from the plan cache, so each execution re-runs the UPDATE.
        var command = ctx.With("upd", ctx.CreateUpdateBuilder<IDeleteEntity>()
                .Set(x => x.Age, x => x.Age + 1)
                .Where(x => x.Id == id)
                .Returning(x => new { x.Id, x.Age }))
            .From("upd")
            .Select(r => new { r.Id, r.Age });

        var first = command.ToList();
        var second = command.ToList();
        var third = command.ToList();

        first.Should().ContainSingle().Which.Age.Should().Be(11);
        second.Should().ContainSingle().Which.Age.Should().Be(12);
        third.Should().ContainSingle().Which.Age.Should().Be(13);

        ctx.From<IDeleteEntity>().Where(x => x.Id == id).Select(x => x.Age).Single().Should().Be(13);
    }

    [Fact]
    public void Iteration14_MutationBesideRecursiveRead_ExecutesEveryTime()
    {
        Assert.SkipUnless(((DataContext)_sut.DataProvider).Dialect.SupportsDataModifyingCtes, DataModifyingCteSkipReason);

        var ctx = _sut.DataProvider;
        var anchorId = UpdateKey();
        var targetId = UpdateKey();
        ctx.CreateInsertBuilder<IDeleteEntity>()
            .Values(new DeleteEntity { Id = anchorId, Name = "anchor", Age = 1 })
            .Insert();
        ctx.CreateInsertBuilder<IDeleteEntity>()
            .Values(new DeleteEntity { Id = targetId, Name = "target", Age = 10 })
            .Insert();

        var anchor = ctx.From<IDeleteEntity>()
            .Where(x => x.Id == anchorId)
            .Select(x => new CteNumberRow { n = x.Age });
        var step = ctx.From("nums")
            .Where(t => t["n"].AsInt < 3)
            .Select(t => new CteNumberRow { n = t["n"].AsInt + 1 });

        var scope = ctx.With("upd", ctx.CreateUpdateBuilder<IDeleteEntity>()
                .Set(x => x.Age, x => x.Age + 1)
                .Where(x => x.Id == targetId)
                .Returning(x => new { x.Id, x.Age }))
            .WithRecursive("nums", anchor.UnionAll(step));

        // The outer read selects the recursive CTE; the sibling data-modifying CTE must still run on
        // each execution of the same command.
        var readCommand = scope.FromTable("nums").Select(t => new CteNumberRow { n = t["n"].AsInt });

        var first = readCommand.ToList();
        var second = readCommand.ToList();

        first.Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3);
        second.Select(r => r.n).OrderBy(n => n).Should().Equal(1, 2, 3);

        // Two executions of the mutation incremented the target twice: 10 -> 11 -> 12.
        ctx.From<IDeleteEntity>().Where(x => x.Id == targetId).Select(x => x.Age).Single().Should().Be(12);
    }

    [Fact]
    public void Iteration14_ReadCteReuse_DoesNotDisableLaterCache()
    {
        var ctx = _sut.DataProvider;

        var readCommand = ctx.With("recent", _sut.ComplexEntity.Where(c => c.Id > 1).Select(c => new { c.Id }))
            .From("recent")
            .Select(t => new { id = t["id"].AsInt });

        var dataContext = (DataContext)ctx;
        var firstPrepared = dataContext.GetPreparedQueryCommand(readCommand, false, true, CancellationToken.None);
        var secondPrepared = dataContext.GetPreparedQueryCommand(readCommand, false, true, CancellationToken.None);

        // A read-only CTE graph is cacheable: the second lookup returns the stored plan by reference.
        ReferenceEquals(firstPrepared, secondPrepared).Should()
            .BeTrue("a read-only CTE command must remain plan-cacheable");
        readCommand.Cache.Should().BeTrue("reusing a read CTE must not clear the sticky Cache flag");

        readCommand.ToList().Select(r => r.id).OrderBy(x => x).Should().Equal(2, 3);
        readCommand.ToList().Select(r => r.id).OrderBy(x => x).Should().Equal(2, 3);

        // A later query on the same context still returns correct results with caching enabled.
        var later = _sut.ComplexEntity.Where(c => c.Id > 0).Select(c => c.Id);
        later.Cache.Should().BeTrue("a read CTE reuse must not disable caching for later commands");
        later.ToList().OrderBy(x => x).Should().Equal(1, 2, 3);
    }
}
