using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FluentAssertions;
using NextORM.Core;

namespace NextORM.Postgres.Tests;

/// <summary>
/// r=2 D209-NL (#209, R209-05) PostgreSQL portability: a non-list terminal rejects a per-parent top-N child
/// (<c>Limit</c>/<c>Offset</c>/<c>Page</c>) or an explicit <c>LoadWith(..., Correlated)</c> before any
/// command is built, independently of the dialect. The tests never open a connection.
/// </summary>
public class EagerTerminalGuardTests
{
    private const string ConnectionString =
        "Host=localhost;Port=5432;Database=nextorm;Username=nextorm;Password=nextorm";

    public enum Shape
    {
        Limit,
        Offset,
        Page,
        Correlated,
        JoinIntoLimit,
    }

    private static IDataContext Context() => new PostgresDataContext(ConnectionString, new DataContextBuilder());

    private static EntityBuilder<TerminalGuardParent> Parents(IDataContext ctx)
        => ctx.From<TerminalGuardParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));

    private static EntityBuilder<TerminalGuardParent> WithShape(IDataContext ctx, Shape shape)
    {
        var builder = Parents(ctx);
        if (shape == Shape.JoinIntoLimit)
            builder = builder.JoinInto(ctx.From<TerminalGuardChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        return shape switch
        {
            Shape.Limit or Shape.JoinIntoLimit
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>().Limit(1), p => p.Id, n => n.ParentId),
            Shape.Offset
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>().Offset(1), p => p.Id, n => n.ParentId),
            Shape.Page
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>().Page(1, 1), p => p.Id, n => n.ParentId),
            Shape.Correlated
                => builder.LoadWith(p => p.Notes, c => c.From<TerminalGuardNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.Correlated),
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
