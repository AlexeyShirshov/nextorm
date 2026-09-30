using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// The plan-cache contract for a command whose <c>WITH</c> carries a data-modifying CTE. Such a
/// statement is side-effecting, so its plan must be neither looked up nor stored. The cache decision
/// (<c>QueryPlanner.GetPreparedQueryCommand</c>) is taken before <c>QueryPreparer.PrepareCtes</c> hoists
/// nested declarations, so a DML CTE that only becomes top-level after the hoist must still be detected;
/// a DML CTE nested in a derived table / join / set-operation branch, which the hoist rejects, fails
/// before it can reach the cache. All tests run against a placeholder connection string (no database).
/// </summary>
public class DataModifyingCtePlanCacheTests
{
    public sealed class DmlRow
    {
        public long Id { get; set; }
    }

    [Fact]
    public void DataModifyingCte_ShouldNotBeCached_AndReadPlanCacheStillWorks()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        var dml = ctx.With("del", ctx.DeleteFrom<IMergeEntity>()
                .Where(x => x.Id == 1)
                .Returning(x => new { x.Id }))
            .From("del")
            .Select(r => new DmlRow { Id = r.Id });

        // The same command prepared twice with storeInCache: true. A cached plan would be looked up on
        // the second call and returned by reference; the data-modifying CTE must keep the local
        // storeInCache false, so every preparation rebuilds.
        var first = ctx.GetPreparedQueryCommand(dml, false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(dml, false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should()
            .BeFalse("a data-modifying CTE must never be looked up or stored in the plan cache");
        ((DbPreparedQueryCommand<DmlRow>)first).DbCommand.CommandText.Should()
            .StartWith("with del as (delete from merge_entity");

        // The bypass must be call-local: the read plan cache on the same context still works.
        var read = ctx.From<ISimpleEntity>().Select(x => x.Id);
        var readFirst = ctx.GetPreparedQueryCommand(read, false, true, CancellationToken.None);
        var readSecond = ctx.GetPreparedQueryCommand(read, false, true, CancellationToken.None);

        ReferenceEquals(readFirst, readSecond).Should()
            .BeTrue("the DML bypass is local to the call and must not disable the plan cache for reads");
    }

    [Fact]
    public void DataModifyingCte_NestedInReadCteBody_ShouldStillBypassCache()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        // The DML CTE is only declared on the body of the outer read CTE; PrepareCtes hoists it to the
        // top-level WITH during preparation, i.e. after the cache decision. HasDataModifyingCte must
        // still see it through the nested declaration tree.
        var innerDml = ctx.With("del", ctx.DeleteFrom<IMergeEntity>()
                .Where(x => x.Id == 1)
                .Returning(x => new { x.Id }))
            .From("del")
            .Select(r => new { r.Id });
        var outer = ctx.With("o", innerDml).From("o").Select(t => t["Id"].AsInt);

        var first = ctx.GetPreparedQueryCommand(outer, false, true, CancellationToken.None);
        var second = ctx.GetPreparedQueryCommand(outer, false, true, CancellationToken.None);

        ReferenceEquals(first, second).Should()
            .BeFalse("a DML CTE hoisted out of a read CTE body must still bypass the plan cache");
        ((DbPreparedQueryCommand<int>)first).DbCommand.CommandText.Should()
            .StartWith("with del as (")
            .And.Contain("), o as (");
    }

    [Fact]
    public void DataModifyingCte_NestedInDerivedTable_ShouldThrowBeforeAnyCacheUse()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        // A DML CTE carried by a derived table cannot be hoisted to the top-level WITH
        // (CteHoister.EnsureNoUnhoistedCtes). HasDataModifyingCte only walks the CTE declaration tree,
        // so it misses this case; preparation then fails fast, before the lookup/store gates, so the
        // command is never cached. This proves the "missed detection" path is not a caching hole.
        var dml = ctx.With("del", ctx.DeleteFrom<IMergeEntity>()
                .Where(x => x.Id == 1)
                .Returning(x => new { x.Id }))
            .From("del")
            .Select(r => new { r.Id });
        var nested = ctx.From(dml).Select(x => new { x.Id });

        var act = () => ctx.GetPreparedQueryCommand(nested, false, true, CancellationToken.None);

        act.Should().Throw<InvalidOperationException>().WithMessage("*cannot be hoisted*");
    }

    [Fact]
    public void DataModifyingCte_NestedInSetOperationOrJoin_ShouldThrowBeforeAnyCacheUse()
    {
        using var ctx = PostgresTestContext.Create();
        ctx.PurgeQueryCache();

        var dml = ctx.With("del", ctx.DeleteFrom<IMergeEntity>()
                .Where(x => x.Id == 1)
                .Returning(x => new { x.Id }))
            .From("del")
            .Select(r => new { r.Id });

        // A set-operation branch carrying a DML CTE: the branch cannot contribute its declaration to
        // the outer statement's WITH, so preparation throws before the cache gates.
        var union = ctx.From<IMergeEntity>().Where(x => x.Id == 0).Select(x => new { x.Id }).Union(dml);
        var unionAct = () => ctx.GetPreparedQueryCommand(union, false, true, CancellationToken.None);
        unionAct.Should().Throw<InvalidOperationException>().WithMessage("*cannot be hoisted*");

        // A derived table on the right of a join carrying a DML CTE: same rejection path.
        var joined = ctx.From<ISimpleEntity>()
            .Join(ctx.From(dml).Select(x => new { x.Id }), (s, d) => s.Id == d.Id)
            .Select(p => new { p.Item1.Id });
        var joinAct = () => ctx.GetPreparedQueryCommand(joined, false, true, CancellationToken.None);
        joinAct.Should().Throw<InvalidOperationException>().WithMessage("*cannot be hoisted*");
    }
}
