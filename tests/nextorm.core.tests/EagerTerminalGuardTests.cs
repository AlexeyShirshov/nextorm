using FluentAssertions;
using NextORM.Core;

namespace NextORM.Core.Tests;

/// <summary>
/// r=2 D209-NL (#209, R209-05): a <c>LoadWith</c> per-parent top-N (child <c>Limit</c>/<c>Offset</c>/
/// <c>Page</c>) or an explicit <c>LoadWith(..., Correlated)</c> is rejected at the non-list terminal
/// boundary — <see cref="EntityBuilder{TEntity}.ToParentCommand"/> and
/// <see cref="EntityBuilder{TEntity}.SelectParent{TResult}"/> — before any parent command is built. Without
/// this guard a non-list terminal (which ignores eager loads) silently dropped the unsupported request:
/// the parent ran and the collection's paging/strategy was never honored. The guard is the shared,
/// side-effect-free <c>EntityBuilderEagerLoading.ValidateLoadWithForJoinInto</c>; it only rejects an
/// explicit mode (never a context default) and never materializes a collection.
/// </summary>
public class EagerTerminalGuardTests
{
    public sealed class TgParent
    {
        public int Id { get; set; }
        public ICollection<TgChild> Children { get; set; } = new List<TgChild>();
        public ICollection<TgNote> Notes { get; set; } = new List<TgNote>();
    }

    public sealed class TgChild
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    public sealed class TgNote
    {
        public int Id { get; set; }
        public int ParentId { get; set; }
    }

    /// <summary>The unsupported eager-load request shapes (V19–V23).</summary>
    public enum Shape
    {
        Limit,
        Offset,
        Page,
        Correlated,
        JoinIntoLimit,
    }

    private static InMemoryDataContext CreateContext()
    {
        var context = new InMemoryDataContext();
        context.From<TgParent>(b => b
            .HasMany(p => p.Children, c => c.ParentId)
            .HasMany(p => p.Notes, n => n.ParentId));
        context.From<TgChild>().WithData(new[]
        {
            new TgChild { Id = 10, ParentId = 1 },
            new TgChild { Id = 11, ParentId = 1 },
            new TgChild { Id = 12, ParentId = 2 },
        });
        context.From<TgNote>().WithData(new[]
        {
            new TgNote { Id = 100, ParentId = 1 },
            new TgNote { Id = 101, ParentId = 2 },
        });
        context.From<TgParent>().WithData(new[]
        {
            new TgParent { Id = 1 },
            new TgParent { Id = 2 },
        });
        return context;
    }

    private static EntityBuilder<TgParent> WithShape(EntityBuilder<TgParent> builder, Shape shape)
    {
        if (shape == Shape.JoinIntoLimit)
            builder = builder.JoinInto(builder.DataProvider.From<TgChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        return shape switch
        {
            Shape.Limit or Shape.JoinIntoLimit
                => builder.LoadWith(p => p.Notes, c => c.From<TgNote>().Limit(1), p => p.Id, n => n.ParentId),
            Shape.Offset
                => builder.LoadWith(p => p.Notes, c => c.From<TgNote>().Offset(1), p => p.Id, n => n.ParentId),
            Shape.Page
                => builder.LoadWith(p => p.Notes, c => c.From<TgNote>().Page(1, 1), p => p.Id, n => n.ParentId),
            Shape.Correlated
                => builder.LoadWith(p => p.Notes, c => c.From<TgNote>(), p => p.Id, n => n.ParentId, EagerLoadMode.Correlated),
            _ => builder,
        };
    }

    private static string ExpectedMessage(Shape shape)
        => shape == Shape.Correlated ? "*Correlated*" : "*Limit/Offset/Page*";

    private static readonly string[] SyncTerminals =
    [
        "ToEnumerable", "ToHashSet", "ToDictionary",
        "First", "FirstOrDefault", "Single", "SingleOrDefault", "Last", "LastOrDefault",
        "Any", "Count", "LongCount", "Min", "Max", "Sum", "Avg", "Prepare",
    ];

    private static readonly string[] AsyncTerminals =
    [
        "ToAsyncEnumerable", "ToHashSetAsync", "ToDictionaryAsync",
        "FirstAsync", "FirstOrDefaultAsync", "SingleAsync", "SingleOrDefaultAsync",
        "LastAsync", "LastOrDefaultAsync", "AnyAsync", "CountAsync", "LongCountAsync", "MinAsync",
    ];

    private static void InvokeSync(string terminal, EntityBuilder<TgParent> builder)
    {
        switch (terminal)
        {
            case "ToEnumerable": builder.ToEnumerable().ToList(); break;
            case "ToHashSet": builder.ToHashSet(); break;
            case "ToDictionary": builder.ToDictionary(p => p.Id); break;
            case "First": builder.First(); break;
            case "FirstOrDefault": builder.FirstOrDefault(); break;
            case "Single": builder.Single(); break;
            case "SingleOrDefault": builder.SingleOrDefault(); break;
            case "Last": builder.Last(); break;
            case "LastOrDefault": builder.LastOrDefault(); break;
            case "Any": builder.Any(); break;
            case "Count": builder.Count(); break;
            case "LongCount": builder.LongCount(); break;
            case "Min": builder.Min(p => p.Id); break;
            case "Max": builder.Max(p => p.Id); break;
            case "Sum": builder.Sum(p => p.Id); break;
            case "Avg": builder.Avg(p => p.Id); break;
            case "Prepare": builder.Prepare(cancellationToken: CancellationToken.None); break;
            default: throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null);
        }
    }

    private static Task InvokeAsync(string terminal, EntityBuilder<TgParent> builder)
    {
        var token = CancellationToken.None;
        return terminal switch
        {
            "ToAsyncEnumerable" => DrainAsync(builder, token),
            "ToHashSetAsync" => builder.ToHashSetAsync(token),
            "ToDictionaryAsync" => builder.ToDictionaryAsync(p => p.Id, token),
            "FirstAsync" => builder.FirstAsync(token),
            "FirstOrDefaultAsync" => builder.FirstOrDefaultAsync(token),
            "SingleAsync" => builder.SingleAsync(token),
            "SingleOrDefaultAsync" => builder.SingleOrDefaultAsync(token),
            "LastAsync" => builder.LastAsync(token),
            "LastOrDefaultAsync" => builder.LastOrDefaultAsync(token),
            "AnyAsync" => builder.AnyAsync(token),
            "CountAsync" => builder.CountAsync(token),
            "LongCountAsync" => builder.LongCountAsync(token),
            "MinAsync" => builder.MinAsync(p => p.Id, token),
            _ => throw new ArgumentOutOfRangeException(nameof(terminal), terminal, null),
        };
    }

    private static async Task DrainAsync(EntityBuilder<TgParent> builder, CancellationToken token)
    {
        await foreach (var _ in builder.ToAsyncEnumerable(token))
        {
        }
    }

    public static IEnumerable<object[]> NegativeCases()
    {
        foreach (var shape in Enum.GetValues<Shape>())
        {
            foreach (var terminal in SyncTerminals)
                yield return [terminal, shape, false];
            foreach (var terminal in AsyncTerminals)
                yield return [terminal, shape, true];
        }
    }

    [Theory]
    [MemberData(nameof(NegativeCases))]
    public async Task NonListTerminal_RejectsUnsupportedLoad(string terminal, Shape shape, bool async)
    {
        using var context = CreateContext();
        var builder = WithShape(context.From<TgParent>(), shape);

        if (async)
        {
            var act = () => InvokeAsync(terminal, builder);
            await act.Should().ThrowAsync<NotSupportedException>().WithMessage(ExpectedMessage(shape));
        }
        else
        {
            var act = () => InvokeSync(terminal, builder);
            act.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));
        }
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Validator_RejectsUnsupportedShapes(Shape shape)
    {
        using var context = CreateContext();
        var builder = WithShape(context.From<TgParent>(), shape);

        Action act = () => EntityBuilderEagerLoading.ValidateLoadWithForJoinInto(builder);
        act.Should().Throw<NotSupportedException>().WithMessage(ExpectedMessage(shape));
    }

    public static IEnumerable<object[]> Shapes() => Enum.GetValues<Shape>().Select(shape => new object[] { shape });

    [Fact]
    public void Validator_PlainLoadWith_DoesNotThrow()
    {
        using var context = CreateContext();
        var builder = context.From<TgParent>()
            .LoadWith(p => p.Notes, c => c.From<TgNote>(), p => p.Id, n => n.ParentId);

        Action act = () => EntityBuilderEagerLoading.ValidateLoadWithForJoinInto(builder);
        act.Should().NotThrow();
    }

    [Fact]
    public void PlainLoadWith_NonListTerminals_AreNotRejected()
    {
        using var context = CreateContext();
        var builder = context.From<TgParent>()
            .LoadWith(p => p.Notes, c => c.From<TgNote>(), p => p.Id, n => n.ParentId);

        builder.ToEnumerable().ToList().Should().HaveCount(2);
        builder.Any().Should().BeTrue();
        builder.Count().Should().Be(2);
        builder.First().Should().NotBeNull();
        builder.ToHashSet().Should().HaveCount(2);
        builder.Min(p => p.Id).Should().Be(1);
    }

    [Fact]
    public void JoinIntoWithoutUnsupportedLoad_NonListTerminals_AreNotRejected()
    {
        using var context = CreateContext();
        var builder = context.From<TgParent>()
            .JoinInto(context.From<TgChild>(), (p, c) => p.Id == c.ParentId, p => p.Children);

        builder.ToEnumerable().ToList().Should().HaveCount(2);
        builder.Any().Should().BeTrue();
        builder.Count().Should().Be(2);
    }

    [Fact]
    public void NoEagerLoads_LeavesNoEagerCommandState()
    {
        using var context = CreateContext();
        var command = context.From<TgParent>().ToCommand();

        command.EagerLoadMode.Should().BeNull("a command with no eager loads carries no strategy");
        command.EagerLoadShape.IsEmpty.Should().BeTrue("a command with no eager loads carries the empty shape");
        context.From<TgParent>().ToEnumerable().ToList().Should().HaveCount(2);
    }
}
