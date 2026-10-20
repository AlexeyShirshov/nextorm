using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.SqlServer.Tests;

/// <summary>
/// r=2 D209-NL (#209, R209-05) SQL Server portability: a non-list terminal rejects a per-parent top-N child
/// (<c>Limit</c>/<c>Offset</c>/<c>Page</c>) or an explicit <c>LoadWith(..., Correlated)</c> before any
/// command is built, independently of the dialect. The tests never open a connection.
/// </summary>
public class EagerTerminalGuardTests
{
    private const string ConnectionString =
        "Server=localhost,1433;Database=nextorm;User Id=sa;Password=nextorm!Passw0rd;TrustServerCertificate=True";

    public enum Shape
    {
        Limit,
        Offset,
        Page,
        Correlated,
        JoinIntoLimit,
    }

    private static IDataContext Context() => new SqlServerDataContext(ConnectionString, new DataContextBuilder());

    private static EntityBuilder<TerminalGuardParent> Parents(IDataContext ctx)
        => ctx.From<TerminalGuardParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));

    private static EntityBuilder<TerminalGuardParent> WithShape(IDataContext ctx, Shape shape, Action? onFactory = null)
    {
        Action factory = onFactory ?? (() => { });
        var builder = Parents(ctx);
        if (shape == Shape.JoinIntoLimit)
            builder = builder.JoinInto(ctx.From<TerminalGuardChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        return shape switch
        {
            Shape.Limit or Shape.JoinIntoLimit
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>().Limit(1); }, p => p.Id, n => n.ParentId),
            Shape.Offset
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>().Offset(1); }, p => p.Id, n => n.ParentId),
            Shape.Page
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>().Page(1, 1); }, p => p.Id, n => n.ParentId),
            Shape.Correlated
                => builder.LoadWith(p => p.Notes, c => { factory(); return c.From<TerminalGuardNote>(); }, p => p.Id, n => n.ParentId, EagerLoadMode.Correlated),
            _ => builder,
        };
    }

    private static string Expected(Shape shape) => shape == Shape.Correlated ? "*Correlated*" : "*Limit/Offset/Page*";

    public static IEnumerable<object[]> Cases() => Enum.GetValues<Shape>().Select(s => new object[] { s });

    [Theory]
    [MemberData(nameof(Cases))]
    public void EnumerableTerminal_RejectsUnsupportedLoad(Shape shape)
    {
        using var ctx = Context();
        Action act = () => WithShape(ctx, shape).ToEnumerable();
        act.Should().Throw<NotSupportedException>().WithMessage(Expected(shape));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public void ScalarTerminals_RejectUnsupportedLoad(Shape shape)
    {
        using var ctx = Context();
        Action count = () => WithShape(ctx, shape).Count();
        count.Should().Throw<NotSupportedException>().WithMessage(Expected(shape));
        Action any = () => WithShape(ctx, shape).Any();
        any.Should().Throw<NotSupportedException>().WithMessage(Expected(shape));
    }

    [Fact]
    public void PlainLoadWith_IsNotRejectedAtTheTerminalBoundary()
    {
        using var ctx = Context();
        var builder = Parents(ctx)
            .LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>(), p => p.Id, n => n.ParentId);

        Action act = () => builder.ToParentCommand();
        act.Should().NotThrow();
    }

    /// <summary>r=3 (#209, R209-01 wording): the factory is invoked at most once per (spec, context).</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public void RejectedLoad_InvokesTheChildFactoryAtMostOnce(Shape shape)
    {
        using var ctx = Context();
        var calls = 0;
        var builder = WithShape(ctx, shape, () => calls++);

        for (var i = 0; i < 3; i++)
        {
            Action act = () => builder.ToParentCommand();
            act.Should().Throw<NotSupportedException>().WithMessage(Expected(shape));
        }

        if (shape == Shape.Correlated)
            calls.Should().Be(0, "an explicit Correlated is rejected before the child factory is invoked");
        else
            calls.Should().Be(1, "the factory is invoked at most once per (spec, context)");
    }

    /// <summary>r=3 (#209, R209-01 wording): a valid non-paged LoadWith builds the child once and reuses it.</summary>
    [Fact]
    public void ValidNonPagedLoadWith_ReusesTheChildMemo()
    {
        using var ctx = Context();
        var calls = 0;
        var builder = Parents(ctx)
            .LoadWith(p => p.Notes, c => { calls++; return c.From<TerminalGuardNote>(); }, p => p.Id, n => n.ParentId);

        builder.ToParentCommand();
        builder.ToParentCommand();
        calls.Should().Be(1, "the memoized child is reused across terminal validations");
    }

    /// <summary>r=3 (#209, V25 wording): memoization is strictly per (spec, context), with no static state.</summary>
    [Fact]
    public void Factory_MemoizedPerContext_NoCrossContextReuse()
    {
        using var first = Context();
        using var second = Context();
        var calls = 0;

        EntityBuilder<TerminalGuardParent> Build(IDataContext ctx) => Parents(ctx)
            .LoadWith(p => p.Notes, c => { calls++; return c.From<TerminalGuardNote>(); }, p => p.Id, n => n.ParentId);

        Build(first).ToParentCommand();
        calls.Should().Be(1, "the first context resolves its own memoized child");

        Build(second).ToParentCommand();
        calls.Should().Be(2, "each (spec, context) resolves independently; no static/global reuse");
    }

    /// <summary>r=3 (#209, R209-01 wording): a child-factory exception propagates unchanged.</summary>
    [Fact]
    public void FactoryException_PropagatesUnchanged()
    {
        using var ctx = Context();
        var marker = new InvalidOperationException("child-factory-boom");
        var builder = Parents(ctx)
            .LoadWith(p => p.Notes, c => throw marker, p => p.Id, n => n.ParentId);

        Action act = () => builder.ToParentCommand();
        act.Should().Throw<InvalidOperationException>().Which.Should().BeSameAs(marker);
    }
}

[SqlTable("terminal_guard_parent")]
public sealed class TerminalGuardParent
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    public ICollection<TerminalGuardChild> Children { get; set; } = new List<TerminalGuardChild>();
    public ICollection<TerminalGuardNote> Notes { get; set; } = new List<TerminalGuardNote>();
}

[SqlTable("terminal_guard_child")]
public sealed class TerminalGuardChild
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}

[SqlTable("terminal_guard_note")]
public sealed class TerminalGuardNote
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Column("parent_id")]
    public int ParentId { get; set; }
}
