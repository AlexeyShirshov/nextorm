using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// T307 (#307) in-memory compatibility control. The in-memory provider has
/// <c>NeedMapping=false</c> and evaluates projections client-side, so the SQL row-materializer
/// defect is not its contract. The relational guard is gated on <c>NeedMapping</c>; this class
/// freezes the measured lane behaviour before the production edit and asserts it again after it:
/// a top-level factory keeps the raw constructor-arity <see cref="ArgumentException"/>, the
/// explicit constructor keeps its existing pinned refusal, and every working shape keeps working.
/// </summary>
public class T307InMemoryProjectionControlsTests
{
    private const string PinnedMessage =
        "The ValueTuple projection 'System.ValueTuple`2[System.Int64,System.String]' at Select position 0 is not supported; project an explicit type or use Tuple.Create.";

    private static InMemoryDataContext CreateContext()
    {
        var ctx = new InMemoryDataContext();
        ctx.From<T307MemoryEntity>().WithData(
        [
            new T307MemoryEntity { Id = 1, Name = "alpha", Score = 10 },
            new T307MemoryEntity { Id = 2, Name = "beta", Score = 20 },
        ]);
        return ctx;
    }

    /// <summary>
    /// Measured baseline: the top-level factory fails in the shared preparation/materializer path
    /// with the raw BCL exception for arities 1, 2 and the 8-element Rest form; the explicit
    /// constructor already refuses with the pinned diagnostic; <c>Tuple.Create</c>, inline
    /// <c>.ItemN</c> and an ordinary predicate keep their working client-side results.
    /// </summary>
    [Fact]
    public void FactoriesAndConstructors_PreserveBaselineBehavior()
    {
        using var ctx = CreateContext();
        var entity = ctx.From<T307MemoryEntity>();

        foreach (var act in new Action[]
        {
            () => entity.OrderBy(x => x.Id).Select(x => ValueTuple.Create(x.Id)).ToList(),
            () => entity.OrderBy(x => x.Id).Select(x => ValueTuple.Create(x.Id, x.Name)).ToList(),
            () => entity.OrderBy(x => x.Id)
                .Select(x => ValueTuple.Create(x.Id, x.Name, x.Score, x.Id, x.Name!, x.Score, x.Id, x.Score))
                .ToList(),
        })
        {
            act.Should().Throw<ArgumentException>()
                .WithMessage("Incorrect number of arguments for constructor");
        }

        FluentActions.Invoking(() => entity.Select(x => new ValueTuple<long, string?>(x.Id, x.Name)).ToList())
            .Should().Throw<NotSupportedException>().WithMessage(PinnedMessage);

        entity.OrderBy(x => x.Id).Select(x => Tuple.Create(x.Id, x.Name)).ToList()
            .Should().HaveCount(2);
        entity.OrderBy(x => x.Id).Select(x => Tuple.Create(x.Id, x.Name).Item1).ToList()
            .Should().Equal(1L, 2L);
        entity.Where(x => Tuple.Create(x.Id, x.Name).Item1 > 1).Select(x => x.Id).ToList()
            .Should().Equal(2L);
    }

    /// <summary>
    /// Measured baseline: anonymous/DTO/entity projections and a nested anonymous factory member
    /// all materialise in memory. The nested shape is the deferred D-N case; the top-level guard
    /// must not recursively reject it.
    /// </summary>
    [Fact]
    public void AnonymousDtoAndEntity_PreserveBaselineBehavior()
    {
        using var ctx = CreateContext();
        var entity = ctx.From<T307MemoryEntity>();

        entity.OrderBy(x => x.Id).Select(x => new { x.Id, x.Name }).ToList()
            .Should().HaveCount(2);
        entity.OrderBy(x => x.Id).Select(x => new T307MemoryDto { Id = x.Id, Name = x.Name }).ToList()
            .Should().HaveCount(2);
        entity.OrderBy(x => x.Id).Select(x => x).ToList()
            .Should().HaveCount(2);

        var nested = entity.OrderBy(x => x.Id)
            .Select(x => new { Pair = ValueTuple.Create(x.Id, x.Name) })
            .ToList();
        nested.Should().HaveCount(2);
        nested[0].Pair.Should().Be((1L, "alpha"));
    }
}

/// <summary>Plain in-memory row for the T307 compatibility controls.</summary>
public sealed class T307MemoryEntity
{
    public long Id { get; set; }

    public string? Name { get; set; }

    public int Score { get; set; }
}

/// <summary>DTO control for the in-memory compatibility controls.</summary>
public sealed class T307MemoryDto
{
    public long Id { get; set; }

    public string? Name { get; set; }
}
