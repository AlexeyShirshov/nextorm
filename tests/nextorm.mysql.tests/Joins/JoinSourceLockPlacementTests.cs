using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.MySql.Tests;

/// <summary>
/// Regression coverage for the release-gate finding 242: a source-level lock passed through
/// <see cref="JoinOptions"/> must reach the joined source on every join surface, and where the source
/// cannot be locked it must fail closed instead of being silently ignored.
/// </summary>
[Collection("Query plan cache")]
public class JoinSourceLockPlacementTests
{
    private static string SqlOf<T>(IDataContext ctx, QueryCommand<T> cmd)
        => ((DbPreparedQueryCommand<T>)ctx.GetPreparedQueryCommand(cmd, false, false, CancellationToken.None)).DbCommand.CommandText;

    private static EntityBuilder<LockParent> Parents(IDataContext ctx)
        => ctx.From<LockParent>(b => b.HasMany(p => p.Children, c => c.ParentId));

    [Fact]
    public void SourceLock_OnJoinInto_ShouldTargetTheJoinedChild()
    {
        using var ctx = MySqlTestContext.Create();

        var command = (dynamic)Parents(ctx)
            .JoinInto(ctx.From<LockChild>(), (p, c) => p.Id == c.ParentId, p => p.Children, JoinType.Left, o => o.ForUpdate())
            .CreateJoinIntoPairCommand();
        var sql = (string)SqlOf(ctx, command);

        sql.Should().Contain("for update of", "the #216 lock must not be dropped on JoinInto");
    }

    [Fact]
    public void SourceLock_OnQuerySourceJoin_ShouldFailClosed()
    {
        using var ctx = MySqlTestContext.Create();
        var derived = ctx.From<IComplexEntity>()
            .Where(c => c.Id > 0)
            .Select(c => new { c.Id });

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>()
            .Join(derived, (s, d) => s.Id == d.Id, o => o.ForUpdate())
            .Select(p => p.Item1.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*can only target a physical table source*");
    }

    [Fact]
    public void SourceLock_OnApply_ShouldFailClosed()
    {
        using var ctx = MySqlTestContext.Create();

        var act = () => SqlOf(ctx, ctx.From<ISimpleEntity>()
            .CrossApply(x => ctx.From<IComplexEntity>().Where(c => c.Id == x.Id), o => o.ForShare())
            .Select(p => p.Item2.Id));

        act.Should().Throw<NotSupportedException>()
            .WithMessage("*can only target a physical table source*");
    }
}

[SqlTable("lock_parent")]
public sealed class LockParent
{
    [Key]
    public int Id { get; set; }

    public List<LockChild> Children { get; set; } = [];
}

[SqlTable("lock_child")]
public sealed class LockChild
{
    [Key]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}
