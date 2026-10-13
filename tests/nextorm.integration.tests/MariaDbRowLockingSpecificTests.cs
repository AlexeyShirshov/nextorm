using FluentAssertions;
using NextORM.MariaDb;

namespace NextORM.Integration.Tests;

/// <summary>
/// MariaDB is not part of the shared provider matrix: its row-locking grammar has no
/// <c>FOR UPDATE OF</c>/<c>FOR SHARE OF</c>, so the source-level surface is fail-closed while the
/// whole-query grammar stays MariaDB-native. The fail-closed assertions do not need a server: the
/// rejection happens while SQL is assembled (before execution), so a placeholder connection is enough.
/// </summary>
public sealed class MariaDbRowLockingSpecificTests
{
    private const string PlaceholderConnectionString =
        "Server=localhost;Port=3306;Database=nextorm;User Id=nextorm;Password=nextorm";

    private static MariaDbDataContext CreateContext()
        => new(PlaceholderConnectionString, new DataContextBuilder());

    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None))
            .DbCommand.CommandText.Replace("\r\n", "\n");

    // scenario labels: mariadb/source-fail-closed, mariadb/no-inherited-of (V-25/V-26).
    [Fact]
    [Trait("PDCA", "T216")]
    public void SourceLock_OnPrimary_ShouldFailClosed()
    {
        using var ctx = CreateContext();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>(o => o.ForUpdate()).Select(x => x.Id));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE OF*");
    }

    [Fact]
    [Trait("PDCA", "T216")]
    public void SourceLock_OnJoin_ShouldFailClosed()
    {
        using var ctx = CreateContext();
        var simple = ctx.From<ISimpleEntity>();
        var complex = ctx.From<IComplexEntity>();

        var act = () => SqlOf(ctx, simple
            .Join(complex, (s, c) => s.Id == c.Id, j => j.ForShare())
            .Select(p => new { p.Item1.Id }));

        act.Should().Throw<NotSupportedException>().WithMessage("*FOR UPDATE OF*");
    }

    // scenario label: mariadb/whole (V-27): the whole-query grammar is preserved and never inherits OF.
    [Fact]
    [Trait("PDCA", "T216")]
    public void WholeLock_ShouldPreserveMariaDbNativeGrammar()
    {
        using var ctx = CreateContext();
        var e = ctx.From<ISimpleEntity>();

        SqlOf(ctx, e.ForUpdate().Select(x => x.Id)).Should().EndWith("for update");
        SqlOf(ctx, e.ForShare().Select(x => x.Id)).Should().EndWith("lock in share mode");
        SqlOf(ctx, e.ForShare(LockWaitMode.SkipLocked).Select(x => x.Id)).Should().EndWith("lock in share mode skip locked");
    }
}
