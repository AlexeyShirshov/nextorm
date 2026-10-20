using FluentAssertions;
using NextORM.Core;

namespace NextORM.ClickHouse.Tests;

/// <summary>
/// Regression coverage for the release-gate finding 242: a source-level lock must never be silently
/// dropped. ClickHouse has no row locking, so every semi/anti join surface must fail closed instead of
/// emitting an unlocked read.
/// </summary>
public class JoinSourceLockPlacementTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    [Fact]
    public void SourceLock_OnSemiJoin_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple
            .SemiJoin(complex, (s, c) => s.Id == c.Id, o => o.ForUpdate())
            .Select(s => s.Id));

        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void SourceLock_OnAntiJoin_ShouldFailClosed()
    {
        using var ctx = ClickHouseTestContext.Create();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple
            .AntiJoin(complex, (s, c) => s.Id == c.Id, o => o.ForShare())
            .Select(s => s.Id));

        act.Should().Throw<NotSupportedException>();
    }
}
