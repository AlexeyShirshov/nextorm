using FluentAssertions;
using NextORM.Core;
using NextORM.Generated.nextorm_alias_tests;

namespace NextORM.AliasTests;

/// <summary>
/// The shared plan cache must not mix the two alias slots when a buyer query and an approver query
/// alternate on one context, and it must not be disabled on the alias command path.
/// </summary>
public class AliasPlanCacheTests
{
    [Fact]
    public void Alternating_buyer_and_approver_queries_keep_slots_and_the_plan_cache()
    {
        var (path, ctx, _) = AliasSqliteDatabase.CreateContext();
        try
        {
            var people = ctx.From<Person>(b => b.Table("person"));
            var chained = ctx.From<Order>(b => b.Table("orders"))
                .Join<Person>(people, (a, b) => a.BuyerId == b.Id, Alias.Buyer)
                .Join<Person>(people, (a, b) => a.Item1.ApproverId == b.Id, Alias.Approver);

            var commands = new List<QueryCommand<int>>();
            for (var i = 0; i < 5; i++)
            {
                // Same projection type, same join chain; only the projected member (slot 2 vs slot 3)
                // differs. A cached plan keyed without the slot would return the wrong column here.
                var buyerCommand = chained.Select(p => p.Buyer.Id);
                var approverCommand = chained.Select(p => p.Approver.Id);
                commands.Add(buyerCommand);
                commands.Add(approverCommand);

                buyerCommand.ToList().Should().Equal(AliasSqliteDatabase.BuyerId);
                approverCommand.ToList().Should().Equal(AliasSqliteDatabase.ApproverId);
            }

            commands.Should().OnlyContain(command => command.Cache);

            // The shared plan-cache path stays enabled for alias commands: a second preparation reuses
            // the cached command instead of rebuilding it.
            var first = ctx.GetPreparedQueryCommand(commands[0], createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            var second = ctx.GetPreparedQueryCommand(commands[0], createEnumerator: false, storeInCache: true, TestContext.Current.CancellationToken);
            second.Should().BeSameAs(first);
        }
        finally
        {
            ctx.Dispose();
            File.Delete(path);
        }
    }
}
